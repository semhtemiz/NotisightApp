using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notisight.Api.Infrastructure.Errors;
using Notisight.Api.Infrastructure.Http;
using Notisight.Api.Options;

namespace Notisight.Api.Features.AI.Services;

public sealed class GeminiEmbeddingService(
    HttpClient httpClient,
    IOptions<GeminiOptions> geminiOptions,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<GeminiEmbeddingService> logger) : IEmbeddingService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly GeminiOptions _geminiOptions = geminiOptions.Value;
    private readonly QdrantOptions _qdrantOptions = qdrantOptions.Value;

    public Task<IReadOnlyList<float>> EmbedDocumentAsync(string text, CancellationToken cancellationToken) =>
        EmbedAsync(text, "RETRIEVAL_DOCUMENT", cancellationToken);

    public Task<IReadOnlyList<float>> EmbedQueryAsync(string text, CancellationToken cancellationToken) =>
        EmbedAsync(text, "RETRIEVAL_QUERY", cancellationToken);

    private async Task<IReadOnlyList<float>> EmbedAsync(
        string text,
        string taskType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            return CreateDeterministicEmbedding(text, _qdrantOptions.VectorSize);
        }

        using var response = await RetryableHttp.SendAsync(
            () =>
            {
                var modelName = NormalizeModelName(_geminiOptions.EmbeddingModel);
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:embedContent");
                request.Headers.Add("x-goog-api-key", _geminiOptions.ApiKey);
                request.Content = JsonContent.Create(
                    new GeminiEmbeddingRequest(
                        $"models/{modelName}",
                        new GeminiContent([new GeminiPart(text)]),
                        taskType,
                        _qdrantOptions.VectorSize),
                    options: JsonOptions);
                return request;
            },
            httpClient.SendAsync,
            logger,
            "Gemini embedding",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadSafeBodyAsync(response, cancellationToken);
            logger.LogWarning(
                "Gemini embedding failed. StatusCode: {StatusCode}. Model: {Model}. Body: {Body}",
                (int)response.StatusCode,
                _geminiOptions.EmbeddingModel,
                body);

            throw BuildEmbeddingException(response);
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbeddingResponse>(
            cancellationToken: cancellationToken);

        return payload?.Embedding?.Values is { Count: > 0 } values
            ? values
            : throw new InvalidOperationException("Gemini embedding response did not include vector values.");
    }

    private static IReadOnlyList<float> CreateDeterministicEmbedding(string text, int vectorSize)
    {
        var size = Math.Max(1, vectorSize);
        var vector = new float[size];
        var tokens = text
            .Split((char[])null!, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()));
            var index = BitConverter.ToUInt32(bytes, 0) % (uint)size;
            vector[index] += 1f;
        }

        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        if (magnitude == 0)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }

        return vector;
    }

    private static string NormalizeModelName(string modelName)
    {
        var normalized = string.IsNullOrWhiteSpace(modelName)
            ? "gemini-embedding-001"
            : modelName.Trim();

        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalized["models/".Length..]
            : normalized;
    }

    private static ApiHttpException BuildEmbeddingException(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Gemini embedding API anahtari kabul edilmedi. Azure Gemini API anahtarini kontrol edin."),

            System.Net.HttpStatusCode.NotFound =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Gemini embedding modeli bulunamadi. Azure Gemini__EmbeddingModel ayarini gemini-embedding-001 yapin."),

            System.Net.HttpStatusCode.TooManyRequests =>
                new ApiHttpException(
                    StatusCodes.Status429TooManyRequests,
                    "Gemini embedding kullanimi limite takildi. Biraz bekleyip tekrar deneyin veya Google AI Studio kotasini kontrol edin."),

            System.Net.HttpStatusCode.BadRequest =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    "Gemini embedding istegi gecersiz bulundu. Embedding modeli ve vektor boyutu ayarlarini kontrol edin."),

            _ =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    $"Gemini embedding servisi yanit veremedi. HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).")
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

    private sealed record GeminiEmbeddingRequest(
        string Model,
        GeminiContent Content,
        string TaskType,
        [property: JsonPropertyName("output_dimensionality")] int OutputDimensionality);

    private sealed record GeminiContent(GeminiPart[] Parts);

    private sealed record GeminiPart(string Text);

    private sealed record GeminiEmbeddingResponse(
        [property: JsonPropertyName("embedding")] GeminiEmbedding? Embedding);

    private sealed record GeminiEmbedding(
        [property: JsonPropertyName("values")] IReadOnlyList<float> Values);
}
