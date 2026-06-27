using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Notes.Contracts;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class NoteVectorLifecycleTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public NoteVectorLifecycleTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.VectorStore.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Note_Create_Update_And_Delete_Sync_Vector_Store()
    {
        var auth = await _client.RegisterAsync("vectors@example.com", "P@ssw0rd123!", "Vector User");
        _client.SetBearer(auth.AccessToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/notes",
            new NoteRequest(
                "Lifecycle",
                "Original semantic retrieval content for indexing.",
                null,
                []));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<NoteResponse>())!;

        await WaitUntilAsync(
            () => _factory.VectorStore.DeletedNoteIds.Contains(created.Id) &&
                  _factory.VectorStore.Upserts.Count == 1,
            "Expected create vector job to delete stale chunks and upsert fresh chunks.");

        Assert.Contains(created.Id, _factory.VectorStore.DeletedNoteIds);
        var createUpsert = Assert.Single(_factory.VectorStore.Upserts);
        Assert.All(createUpsert, chunk =>
        {
            Assert.Equal(created.Id, chunk.NoteId);
            Assert.Equal("Lifecycle", chunk.Title);
            Assert.NotEqual(Guid.Empty, chunk.UserId);
        });

        var updateResponse = await _client.PutAsJsonAsync(
            $"/notes/{created.Id}",
            new NoteRequest(
                "Lifecycle Updated",
                "Updated vector content replaces stale chunks.",
                null,
                []));
        updateResponse.EnsureSuccessStatusCode();

        await WaitUntilAsync(
            () => _factory.VectorStore.DeletedNoteIds.Count(x => x == created.Id) == 2 &&
                  _factory.VectorStore.Upserts.Count == 2,
            "Expected update vector job to replace previous chunks.");

        Assert.Equal(2, _factory.VectorStore.DeletedNoteIds.Count(x => x == created.Id));
        Assert.Equal(2, _factory.VectorStore.Upserts.Count);
        var updateUpsert = _factory.VectorStore.Upserts[^1];
        Assert.All(updateUpsert, chunk =>
        {
            Assert.Equal(created.Id, chunk.NoteId);
            Assert.Equal("Lifecycle Updated", chunk.Title);
            Assert.Contains("Updated vector content", chunk.Content);
        });

        var deleteResponse = await _client.DeleteAsync($"/notes/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await WaitUntilAsync(
            () => _factory.VectorStore.DeletedNoteIds.Count(x => x == created.Id) == 3,
            "Expected delete vector job to remove note chunks.");

        Assert.Equal(3, _factory.VectorStore.DeletedNoteIds.Count(x => x == created.Id));
        Assert.Equal(2, _factory.VectorStore.Upserts.Count);
    }

    [Fact]
    public async Task Note_Create_Persists_When_Vector_Upsert_Fails_AndMarksSyncFailed()
    {
        _factory.VectorStore.ThrowOnUpsert = true;

        var auth = await _client.RegisterAsync("vector-failure@example.com", "P@ssw0rd123!", "Vector Failure");
        _client.SetBearer(auth.AccessToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/notes",
            new NoteRequest(
                "Failure Still Saves",
                "This note remains in MSSQL even when vector indexing fails.",
                null,
                []));

        var body = await createResponse.Content.ReadAsStringAsync();
        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {(int)createResponse.StatusCode}: {body}");
        var created = (await createResponse.Content.ReadFromJsonAsync<NoteResponse>())!;

        await WaitUntilAsync(
            () => GetNoteVectorStatus(created.Id).Status == VectorSyncStatus.Failed,
            "Expected background vector job to mark the note as failed.");

        var note = GetNoteVectorStatus(created.Id);

        Assert.Equal(VectorSyncStatus.Failed, note.Status);
        Assert.Contains("Synthetic Qdrant upsert failure", note.Error);
        Assert.Null(note.SyncedAtUtc);
    }

    private (string Status, string? Error, DateTimeOffset? SyncedAtUtc) GetNoteVectorStatus(Guid noteId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var note = dbContext.Notes.AsNoTracking().Single(x => x.Id == noteId);
        return (note.VectorSyncStatus, note.VectorSyncError, note.VectorSyncedAtUtc);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), because);
    }
}
