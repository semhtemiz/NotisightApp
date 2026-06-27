using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class RagRetrievalTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public RagRetrievalTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.VectorStore.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Hybrid_Search_Returns_Rrf_Final_Score_And_Filters_Low_Vector_Results()
    {
        var auth = await _client.RegisterAsync($"rrf-{Guid.NewGuid():N}@example.com", "P@ssw0rd123!", "RRF User");
        var userId = auth.User.Id;
        var highChunk = new ChunkedNote(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Vector winner",
            "Vector-only content.",
            0,
            "note")
        {
            UserId = userId
        };
        var lowChunk = new ChunkedNote(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Filtered vector",
            "This should be removed by MinVectorScore.",
            0,
            "note")
        {
            UserId = userId
        };

        _factory.VectorStore.SetSearchResults(
            new SearchChunkResult(highChunk, 0.92),
            new SearchChunkResult(lowChunk, 0.01));

        using var scope = _factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IChunkSearchService>();

        var results = await search.SearchAsync(
            userId,
            "no keyword overlap",
            new QueryIntent(),
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(highChunk.NoteId, result.Chunk.NoteId);
        Assert.Equal(1.0 / 61.0, result.Score, precision: 6);
        Assert.DoesNotContain(results, x => x.Chunk.NoteId == lowChunk.NoteId);
    }

    [Fact]
    public async Task Keyword_Search_Cleans_Html_And_Preserves_Source_Type()
    {
        var auth = await _client.RegisterAsync($"keyword-{Guid.NewGuid():N}@example.com", "P@ssw0rd123!", "Keyword User");
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = auth.User.Id,
            Title = "PDF kaynak",
            Content = "<p>Alpha <strong>semantic</strong> retrieval &amp; temiz metin.</p>",
            FileType = "pdf",
            VectorSyncStatus = VectorSyncStatus.Synced,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        using (var seedScope = _factory.Services.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Notes.Add(note);
            await dbContext.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IChunkSearchService>();

        var results = await search.SearchAsync(
            auth.User.Id,
            "semantic",
            new QueryIntent(),
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(note.Id, result.Chunk.NoteId);
        Assert.Equal("pdf", result.Chunk.SourceType);
        Assert.DoesNotContain("<strong>", result.Chunk.Content);
        Assert.Contains("semantic retrieval & temiz metin", result.Chunk.Content);
    }
}
