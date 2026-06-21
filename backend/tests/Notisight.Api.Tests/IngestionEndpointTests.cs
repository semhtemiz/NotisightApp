using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Tests.Infrastructure;

namespace Notisight.Api.Tests;

public sealed class IngestionEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public IngestionEndpointTests(TestApiFactory factory)
    {
        _factory = factory;
        _factory.AudioTranscription.Reset();
        _factory.VectorStore.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadPdf_WithWrongExtension_ReturnsBadRequest()
    {
        var auth = await _client.RegisterAsync("pdf@example.com", "P@ssw0rd123!", "PDF User");
        _client.SetBearer(auth.AccessToken);

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent("hello world"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "file", "notes.txt");

        var response = await _client.PostAsync("/notes/upload-pdf", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only PDF files are supported.", body);
    }

    [Fact]
    public async Task UploadAudio_WithWrongExtension_ReturnsBadRequest()
    {
        var auth = await _client.RegisterAsync("audio@example.com", "P@ssw0rd123!", "Audio User");
        _client.SetBearer(auth.AccessToken);

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent([0x01, 0x02, 0x03]);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/aac");
        form.Add(content, "file", "voice.aac");

        var response = await _client.PostAsync("/notes/upload-audio", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Only WAV, WEBM, M4A, and MP3 audio files are supported.", body);
    }

    [Fact]
    public async Task UploadAudio_WithWav_CreatesNote_AndIndexesTranscript()
    {
        var auth = await _client.RegisterAsync("audio-success@example.com", "P@ssw0rd123!", "Audio User");
        _client.SetBearer(auth.AccessToken);

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(CreateMinimalWav());
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(content, "file", "meeting.wav");

        var response = await _client.PostAsync("/notes/upload-audio", form);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {(int)response.StatusCode}: {body}");

        var uploaded = (await response.Content.ReadFromJsonAsync<UploadedNoteResponse>())!;
        Assert.Equal("meeting", uploaded.Title);
        Assert.Equal("audio", uploaded.SourceType);
        Assert.Equal("pending", uploaded.VectorSyncStatus);
        Assert.Contains("meeting.wav", _factory.AudioTranscription.FileNames);

        await WaitUntilAsync(
            () => _factory.VectorStore.DeletedNoteIds.Contains(uploaded.NoteId) &&
                  _factory.VectorStore.Upserts.Count == 1,
            "Expected upload vector job to index the transcript.");

        Assert.Contains(uploaded.NoteId, _factory.VectorStore.DeletedNoteIds);

        var upsert = Assert.Single(_factory.VectorStore.Upserts);
        Assert.All(upsert, chunk =>
        {
            Assert.Equal(uploaded.NoteId, chunk.NoteId);
            Assert.Equal("meeting", chunk.Title);
            Assert.Contains("Recorded transcript", chunk.Content);
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

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

    private static byte[] CreateMinimalWav() =>
    [
        0x52, 0x49, 0x46, 0x46,
        0x24, 0x00, 0x00, 0x00,
        0x57, 0x41, 0x56, 0x45,
        0x66, 0x6d, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00,
        0x01, 0x00,
        0x01, 0x00,
        0x40, 0x1f, 0x00, 0x00,
        0x80, 0x3e, 0x00, 0x00,
        0x02, 0x00,
        0x10, 0x00,
        0x64, 0x61, 0x74, 0x61,
        0x00, 0x00, 0x00, 0x00
    ];
}
