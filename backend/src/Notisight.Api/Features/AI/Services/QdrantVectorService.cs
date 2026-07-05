using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Infrastructure.Errors;
using Notisight.Api.Infrastructure.Http;
using Notisight.Api.Options;

namespace Notisight.Api.Features.AI.Services;

public sealed class QdrantVectorService(
    HttpClient httpClient,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<QdrantVectorService> logger) : IQdrantVectorService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, byte> PreparedCollections = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CollectionSetupLocks = new();
    private readonly QdrantOptions _options = qdrantOptions.Value;

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var cacheKey = CollectionSetupCacheKey;
        if (PreparedCollections.ContainsKey(cacheKey))
        {
            return;
        }

        var setupLock = CollectionSetupLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await setupLock.WaitAsync(cancellationToken);
        try
        {
            if (PreparedCollections.ContainsKey(cacheKey))
            {
                return;
            }

            await EnsureCollectionCoreAsync(cancellationToken);
            PreparedCollections[cacheKey] = 0;
        }
        finally
        {
            setupLock.Release();
        }
    }

    private async Task EnsureCollectionCoreAsync(CancellationToken cancellationToken)
    {
        using var check = await SendAsync(
            HttpMethod.Get,
            $"/collections/{Uri.EscapeDataString(_options.CollectionName)}",
            cancellationToken);

        if (check.IsSuccessStatusCode)
        {
            await EnsurePayloadIndexesAsync(cancellationToken);
            return;
        }

        if (check.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            await EnsureQdrantSuccessAsync(check, "check collection", cancellationToken);
        }

        using var response = await SendAsync(
            HttpMethod.Put,
            $"/collections/{Uri.EscapeDataString(_options.CollectionName)}",
            new CreateCollectionRequest(new VectorConfig(_options.VectorSize, "Cosine")),
            cancellationToken);

        await EnsureQdrantSuccessAsync(response, "create collection", cancellationToken);
        await EnsurePayloadIndexesAsync(cancellationToken);
    }

    public async Task UpsertChunksAsync(
        IReadOnlyList<ChunkedNote> chunks,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || chunks.Count == 0)
        {
            return;
        }

        if (chunks.Count != vectors.Count)
        {
            throw new InvalidOperationException("Chunk and vector counts must match.");
        }

        await EnsureCollectionAsync(cancellationToken);

        var points = chunks.Select((chunk, index) => new Point(
            chunk.ChunkId,
            vectors[index],
            new PointPayload(
                chunk.UserId,
                chunk.NoteId,
                chunk.Title,
                chunk.Content,
                chunk.Index,
                chunk.SourceType,
                chunk.SourceLabel))).ToArray();

        using var response = await SendAsync(
            HttpMethod.Put,
            $"/collections/{Uri.EscapeDataString(_options.CollectionName)}/points?wait=true",
            new UpsertPointsRequest(points),
            cancellationToken);

        await EnsureQdrantSuccessAsync(response, "upsert points", cancellationToken);
    }

    public async Task DeleteByNoteIdAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        await EnsureCollectionAsync(cancellationToken);

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/collections/{Uri.EscapeDataString(_options.CollectionName)}/points/delete?wait=true",
            new DeletePointsRequest(new Filter([
                new FieldCondition("noteId", new MatchValue(noteId.ToString()))
            ])),
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureQdrantSuccessAsync(response, "delete points", cancellationToken);
    }

    public async Task<IReadOnlyList<SearchChunkResult>> SearchAsync(
        Guid userId,
        IReadOnlyList<float> queryVector,
        int topK,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || queryVector.Count == 0)
        {
            return [];
        }

        await EnsureCollectionAsync(cancellationToken);

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/collections/{Uri.EscapeDataString(_options.CollectionName)}/points/search",
            new SearchPointsRequest(
                queryVector,
                Math.Max(1, topK),
                true,
                new Filter([
                    new FieldCondition("userId", new MatchValue(userId.ToString()))
            ])),
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureQdrantSuccessAsync(response, "search points", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(
            cancellationToken: cancellationToken);

        return payload?.Result?
            .Select(ToSearchResult)
            .Where(x => x is not null)
            .Cast<SearchChunkResult>()
            .ToList() ?? [];
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.EffectiveUrl) &&
        !string.IsNullOrWhiteSpace(_options.CollectionName);

    private string CollectionSetupCacheKey =>
        $"{_options.EffectiveUrl.TrimEnd('/')}/{_options.CollectionName}/{_options.VectorSize}";

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        return await RetryableHttp.SendAsync(
            () => CreateRequest(method, path, body),
            httpClient.SendAsync,
            logger,
            $"Qdrant {method} {path}",
            cancellationToken);
    }

    private async Task EnsurePayloadIndexesAsync(CancellationToken cancellationToken)
    {
        foreach (var fieldName in new[] { "noteId", "userId" })
        {
            using var response = await SendAsync(
                HttpMethod.Put,
                $"/collections/{Uri.EscapeDataString(_options.CollectionName)}/index?wait=true",
                new CreatePayloadIndexRequest(fieldName, "keyword"),
                cancellationToken);

            if (!response.IsSuccessStatusCode &&
                response.StatusCode != System.Net.HttpStatusCode.Conflict)
            {
                await EnsureQdrantSuccessAsync(response, $"create payload index {fieldName}", cancellationToken);
            }
        }
    }

    private async Task EnsureQdrantSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await ReadSafeBodyAsync(response, cancellationToken);
        logger.LogWarning(
            "Qdrant {Operation} failed. StatusCode: {StatusCode}. Collection: {CollectionName}. Body: {Body}",
            operation,
            (int)response.StatusCode,
            _options.CollectionName,
            body);

        throw BuildQdrantException(response);
    }

    private static ApiHttpException BuildQdrantException(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Qdrant API anahtari kabul edilmedi. Azure Qdrant ayarlarini kontrol edin."),

            System.Net.HttpStatusCode.NotFound =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Qdrant koleksiyonu bulunamadi veya endpoint yanlis. Qdrant URL ve koleksiyon ayarlarini kontrol edin."),

            System.Net.HttpStatusCode.TooManyRequests =>
                new ApiHttpException(
                    StatusCodes.Status429TooManyRequests,
                    "Qdrant kullanimi limite takildi. Biraz bekleyip tekrar deneyin."),

            System.Net.HttpStatusCode.BadRequest =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Qdrant istegi gecersiz bulundu. Vektor boyutu ve koleksiyon ayarlarini kontrol edin."),

            _ =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    $"Qdrant servisi yanit veremedi. HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).")
        };
    }

    private static async Task<string> ReadSafeBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return "<empty>";
            }

            return body.Length <= 1000 ? body : body[..1000];
        }
        catch
        {
            return "<unreadable>";
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        return await RetryableHttp.SendAsync(
            () => CreateRequest(method, path),
            httpClient.SendAsync,
            logger,
            $"Qdrant {method} {path}",
            cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object body)
    {
        var request = CreateRequest(method, path);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUrl = _options.EffectiveUrl.TrimEnd('/');
        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("api-key", _options.ApiKey);
        }

        return request;
    }

    private static SearchChunkResult? ToSearchResult(QdrantPoint point)
    {
        if (point.Payload is null ||
            !TryGetString(point.Payload, "noteId", out var noteIdText) ||
            !Guid.TryParse(noteIdText, out var noteId) ||
            !TryGetString(point.Payload, "title", out var title) ||
            !TryGetString(point.Payload, "content", out var content))
        {
            return null;
        }

        var index = TryGetInt(point.Payload, "index", out var payloadIndex) ? payloadIndex : 0;
        TryGetString(point.Payload, "userId", out var userIdText);
        var userId = Guid.TryParse(userIdText, out var parsedUserId) ? parsedUserId : Guid.Empty;
        TryGetString(point.Payload, "sourceType", out var sourceType);
        TryGetString(point.Payload, "sourceLabel", out var sourceLabel);
        var chunkId = TryGetGuid(point.Id, out var parsedChunkId) ? parsedChunkId : Guid.NewGuid();
        return new SearchChunkResult(
            new ChunkedNote(chunkId, noteId, title, content, index, 
                string.IsNullOrEmpty(sourceType) ? "note" : sourceType,
                sourceLabel ?? "")
            {
                UserId = userId
            },
            point.Score);
    }

    private static bool TryGetGuid(JsonElement element, out Guid value)
    {
        value = Guid.Empty;
        if (element.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(element.GetString(), out value);
        }

        return Guid.TryParse(element.ToString(), out value);
    }

    private static bool TryGetString(
        IReadOnlyDictionary<string, JsonElement> payload,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!payload.TryGetValue(key, out var element))
        {
            return false;
        }

        value = element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt(
        IReadOnlyDictionary<string, JsonElement> payload,
        string key,
        out int value)
    {
        value = 0;
        return payload.TryGetValue(key, out var element) && element.TryGetInt32(out value);
    }

    private sealed record CreateCollectionRequest(VectorConfig Vectors);

    private sealed record VectorConfig(int Size, string Distance);

    private sealed record CreatePayloadIndexRequest(
        [property: JsonPropertyName("field_name")] string FieldName,
        [property: JsonPropertyName("field_schema")] string FieldSchema);

    private sealed record UpsertPointsRequest(Point[] Points);

    private sealed record Point(Guid Id, IReadOnlyList<float> Vector, PointPayload Payload);

    private sealed record PointPayload(
        Guid UserId,
        Guid NoteId,
        string Title,
        string Content,
        int Index,
        string SourceType,
        string SourceLabel);

    private sealed record DeletePointsRequest(Filter Filter);

    private sealed record SearchPointsRequest(
        IReadOnlyList<float> Vector,
        int Limit,
        [property: JsonPropertyName("with_payload")] bool WithPayload,
        Filter Filter);

    private sealed record Filter(FieldCondition[] Must);

    private sealed record FieldCondition(string Key, MatchValue Match);

    private sealed record MatchValue(string Value);

    private sealed record SearchResponse(
        [property: JsonPropertyName("result")] IReadOnlyList<QdrantPoint>? Result);

    private sealed record QdrantPoint(
        [property: JsonPropertyName("id")] JsonElement Id,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("payload")] IReadOnlyDictionary<string, JsonElement>? Payload);
}
