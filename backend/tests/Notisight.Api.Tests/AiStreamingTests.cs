using System.Net;
using System.Net.Http.Json;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.Notes.Contracts;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class AiStreamingTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public AiStreamingTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ask_Returns_ServerSentEvents_With_SourceReferences()
    {
        var auth = await _client.RegisterAsync("ai@example.com", "P@ssw0rd123!", "AI User");
        _client.SetBearer(auth.AccessToken);

        var noteResponse = await _client.PostAsJsonAsync(
            "/notes",
            new NoteRequest(
                "Retrieval Primer",
                "Semantic search retrieves the most relevant note chunks for a question.",
                null,
                []));
        var noteBody = await noteResponse.Content.ReadAsStringAsync();
        Assert.True(
            noteResponse.IsSuccessStatusCode,
            $"Expected note creation success but got {(int)noteResponse.StatusCode}: {noteBody}");

        var request = new HttpRequestMessage(HttpMethod.Post, "/ai/ask")
        {
            Content = JsonContent.Create(new AskRequest
            {
                Question = "semantic search question",
                Mode = ChatMode.Notisight
            })
        };

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {(int)response.StatusCode}: {body}");
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        Assert.Contains("event: chunk", body);
        Assert.Contains("event: complete", body);
        Assert.Contains("Retrieval Primer", body);
    }
}
