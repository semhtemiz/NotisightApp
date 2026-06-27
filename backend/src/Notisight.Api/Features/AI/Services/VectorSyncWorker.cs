using Microsoft.EntityFrameworkCore;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.AI.Services;

public sealed class VectorSyncWorker(
    IVectorSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<VectorSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                using var scope = scopeFactory.CreateScope();
                var vectorSyncService = scope.ServiceProvider.GetRequiredService<INoteVectorSyncService>();

                if (job.Type == VectorSyncJobType.Delete)
                {
                    await vectorSyncService.DeleteNoteAsync(job.NoteId, stoppingToken);
                    continue;
                }

                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var note = await dbContext.Notes
                    .SingleOrDefaultAsync(x => x.Id == job.NoteId, stoppingToken);

                if (note is null)
                {
                    continue;
                }

                await vectorSyncService.UpsertNoteAsync(note, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Vector sync job failed for note {NoteId}", job.NoteId);
            }
        }
    }
}
