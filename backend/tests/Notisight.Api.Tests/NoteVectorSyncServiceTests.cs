using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class NoteVectorSyncServiceTests
{
    [Fact]
    public async Task UpsertNoteAsync_MarksFailed_When_Embedding_Vector_Size_Does_Not_Match_Qdrant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "embedding-size@example.com",
            Username = "embedding-size",
            DisplayName = "Embedding Size",
            PasswordHash = "hash",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Dimension mismatch",
            Content = "This note should produce at least one chunk.",
            VectorSyncStatus = VectorSyncStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.Users.Add(user);
        dbContext.Notes.Add(note);
        await dbContext.SaveChangesAsync();

        var vectorStore = new RecordingQdrantVectorService();
        var service = new NoteVectorSyncService(
            new TextChunkingService(Microsoft.Extensions.Options.Options.Create(new RagOptions())),
            new WrongSizeEmbeddingService(),
            vectorStore,
            new NoopPdfIngestionService(),
            new NoopFileStorageService(),
            dbContext,
            Microsoft.Extensions.Options.Options.Create(new QdrantOptions { VectorSize = 4 }),
            NullLogger<NoteVectorSyncService>.Instance);

        await service.UpsertNoteAsync(note, CancellationToken.None);

        var saved = await dbContext.Notes.AsNoTracking().SingleAsync(x => x.Id == note.Id);
        Assert.Equal(VectorSyncStatus.Failed, saved.VectorSyncStatus);
        Assert.Contains("Embedding vector size mismatch", saved.VectorSyncError);
        Assert.Empty(vectorStore.Upserts);
    }

    private sealed class WrongSizeEmbeddingService : IEmbeddingService
    {
        public Task<IReadOnlyList<float>> EmbedDocumentAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<float>>([1, 2, 3]);

        public Task<IReadOnlyList<float>> EmbedQueryAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<float>>([1, 2, 3]);
    }

    private sealed class NoopPdfIngestionService : IPdfIngestionService
    {
        public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<IReadOnlyList<(int PageNumber, string Text)>> ExtractPageTextsAsync(
            Stream pdfStream,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(int PageNumber, string Text)>>([]);
    }

    private sealed class NoopFileStorageService : IFileStorageService
    {
        public Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<(Stream Stream, string ContentType)> GetFileStreamAsync(
            string fileUrl,
            CancellationToken cancellationToken) =>
            Task.FromResult<(Stream Stream, string ContentType)>((new MemoryStream(), "application/octet-stream"));

        public Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());
    }
}
