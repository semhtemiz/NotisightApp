using System.Net;
using System.Net.Http.Json;
using Notisight.Api.Features.Folders.Contracts;
using Notisight.Api.Features.Notes.Contracts;
using Notisight.Api.Features.Tags.Contracts;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class CrudIsolationTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public CrudIsolationTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Authenticated_User_Can_Manage_Own_Content_And_Other_User_Cannot_See_It()
    {
        var firstUser = await _client.RegisterAsync("owner@example.com", "Owner123!", "Owner");
        _client.SetBearer(firstUser.AccessToken);

        var tagResponse = await _client.PostAsJsonAsync("/tags", new TagRequest("architecture"));
        tagResponse.EnsureSuccessStatusCode();
        var tag = (await tagResponse.Content.ReadFromJsonAsync<TagResponse>())!;

        var folderResponse = await _client.PostAsJsonAsync("/folders", new FolderRequest("Research", null));
        folderResponse.EnsureSuccessStatusCode();
        var folder = (await folderResponse.Content.ReadFromJsonAsync<FolderResponse>())!;

        var noteResponse = await _client.PostAsJsonAsync(
            "/notes",
            new NoteRequest("RAG Notes", "Semantic retrieval pipeline", folder.Id, [tag.Id]));
        noteResponse.EnsureSuccessStatusCode();
        var note = (await noteResponse.Content.ReadFromJsonAsync<NoteResponse>())!;

        Assert.Equal(folder.Id, note.FolderId);
        Assert.Single(note.Tags);
        Assert.Equal("architecture", note.Tags[0].Name);

        var getNoteResponse = await _client.GetAsync($"/notes/{note.Id}");
        getNoteResponse.EnsureSuccessStatusCode();

        var secondUser = await _client.RegisterAsync("guest@example.com", "Guest123!", "Guest");
        _client.SetBearer(secondUser.AccessToken);

        var folders = (await _client.GetFromJsonAsync<List<FolderResponse>>("/folders"))!;
        var tags = (await _client.GetFromJsonAsync<List<TagResponse>>("/tags"))!;
        var notesResponse = await _client.GetAsync("/notes");
        if (!notesResponse.IsSuccessStatusCode)
        {
            var body = await notesResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Notes lookup failed with {(int)notesResponse.StatusCode}: {body}");
        }

        var notes = (await notesResponse.Content.ReadFromJsonAsync<List<NoteResponse>>())!;

        Assert.Empty(folders);
        Assert.Empty(tags);
        Assert.Empty(notes);

        var forbiddenNoteLookup = await _client.GetAsync($"/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenNoteLookup.StatusCode);
    }

    [Fact]
    public async Task Folder_Update_Keeps_Parent_When_Parent_Is_Provided()
    {
        var auth = await _client.RegisterAsync("folder-rename@example.com", "Owner123!", "Folder User");
        _client.SetBearer(auth.AccessToken);

        var parent = await CreateFolderAsync("Parent", null);
        var child = await CreateFolderAsync("Child", parent.Id);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/folders/{child.Id}",
            new FolderRequest("Renamed Child", parent.Id));

        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<FolderResponse>())!;

        Assert.Equal("Renamed Child", updated.Name);
        Assert.Equal(parent.Id, updated.ParentFolderId);
    }

    [Fact]
    public async Task Folder_Update_Rejects_Other_Users_Parent()
    {
        var firstUser = await _client.RegisterAsync("folder-owner@example.com", "Owner123!", "Owner");
        _client.SetBearer(firstUser.AccessToken);
        var firstUsersFolder = await CreateFolderAsync("Owner Folder", null);

        var secondUser = await _client.RegisterAsync("folder-guest@example.com", "Guest123!", "Guest");
        _client.SetBearer(secondUser.AccessToken);
        var secondUsersFolder = await CreateFolderAsync("Guest Folder", null);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/folders/{secondUsersFolder.Id}",
            new FolderRequest("Guest Folder", firstUsersFolder.Id));

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Folder_Update_Rejects_Descendant_As_Parent()
    {
        var auth = await _client.RegisterAsync("folder-cycle@example.com", "Owner123!", "Folder User");
        _client.SetBearer(auth.AccessToken);

        var parent = await CreateFolderAsync("Parent", null);
        var child = await CreateFolderAsync("Child", parent.Id);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/folders/{parent.Id}",
            new FolderRequest("Parent", child.Id));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Folder_Delete_With_Child_Folders_Moves_Children_To_Root()
    {
        var auth = await _client.RegisterAsync("folder-delete@example.com", "Owner123!", "Folder User");
        _client.SetBearer(auth.AccessToken);

        var parent = await CreateFolderAsync("Parent", null);
        var child = await CreateFolderAsync("Child", parent.Id);

        var deleteResponse = await _client.DeleteAsync($"/folders/{parent.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var folders = (await _client.GetFromJsonAsync<List<FolderResponse>>("/folders"))!;
        Assert.DoesNotContain(folders, x => x.Id == parent.Id);

        var movedChild = Assert.Single(folders, x => x.Id == child.Id);
        Assert.Null(movedChild.ParentFolderId);
    }

    private async Task<FolderResponse> CreateFolderAsync(string name, Guid? parentFolderId)
    {
        var response = await _client.PostAsJsonAsync("/folders", new FolderRequest(name, parentFolderId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderResponse>())!;
    }
}
