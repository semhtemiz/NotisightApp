using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.AI.Services;

public sealed class VectorSyncWorker(
    IVectorSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<VectorSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromMinutes(5);
    private static readonly string[] StartupRecoveryStatuses =
    [
        VectorSyncStatus.Pending,
        VectorSyncStatus.Indexing,
        VectorSyncStatus.Failed
    ];
    private static readonly string[] PeriodicRecoveryStatuses =
    [
        VectorSyncStatus.Pending,
        VectorSyncStatus.Failed
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await EnqueueBacklogAsync(StartupRecoveryStatuses, "startup", stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Vector sync startup backlog scan failed.");
        }

        var queueConsumer = ConsumeQueueAsync(stoppingToken);
        var recoveryScanner = ScanBacklogPeriodicallyAsync(stoppingToken);

        await Task.WhenAll(queueConsumer, recoveryScanner);
    }

    private async Task ConsumeQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            VectorSyncJob job;
            try
            {
                job = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Vector sync job failed for note {NoteId}", job.NoteId);
            }
        }
    }

    private async Task ScanBacklogPeriodicallyAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RecoveryInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EnqueueBacklogAsync(PeriodicRecoveryStatuses, "periodic", stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Vector sync backlog scan failed.");
            }
        }
    }

    private async Task ProcessJobAsync(VectorSyncJob job, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var vectorSyncService = scope.ServiceProvider.GetRequiredService<INoteVectorSyncService>();

        if (job.Type == VectorSyncJobType.Delete)
        {
            await vectorSyncService.DeleteNoteAsync(job.NoteId, cancellationToken);
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var note = await dbContext.Notes
            .SingleOrDefaultAsync(x => x.Id == job.NoteId, cancellationToken);

        if (note is null)
        {
            return;
        }

        await vectorSyncService.UpsertNoteAsync(note, cancellationToken);
    }

    private async Task EnqueueBacklogAsync(
        IReadOnlyCollection<string> statuses,
        string reason,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString) || IsPlaceholderConnectionString(connectionString))
        {
            return;
        }

        var noteIds = await dbContext.Notes
            .AsNoTracking()
            .Where(x => statuses.Contains(x.VectorSyncStatus))
            .OrderBy(x => x.UpdatedAtUtc)
            .Select(x => x.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var noteId in noteIds)
        {
            queue.EnqueueUpsert(noteId);
        }

        if (noteIds.Count > 0)
        {
            logger.LogInformation(
                "Enqueued {Count} vector sync backlog notes during {Reason} recovery.",
                noteIds.Count,
                reason);
        }
    }

    private static bool IsPlaceholderConnectionString(string connectionString) =>
        connectionString.Equals("sql_server_connection_string", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("connection_string", StringComparison.OrdinalIgnoreCase);
}
