using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Notisight.Api.Infrastructure.Errors;
using Notisight.Api.Infrastructure.Http;
using Notisight.Api.Options;

namespace Notisight.Api.Features.Ingestion.Services;

public sealed class AudioTranscriptionService(
    HttpClient httpClient,
    IOptions<DeepgramOptions> deepgramOptions,
    ILogger<AudioTranscriptionService> logger) : IAudioTranscriptionService
{
    private const long MaxAudioBytes = 25_000_000;
    private readonly DeepgramOptions _options = deepgramOptions.Value;

    public async Task<string> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Audio transcription requires Deepgram:ApiKey to be configured.");
        }

        await using var memory = new MemoryStream();
        await audioStream.CopyToAsync(memory, cancellationToken);

        if (memory.Length == 0)
        {
            throw new InvalidOperationException("Uploaded audio file is empty.");
        }

        if (memory.Length > MaxAudioBytes)
        {
            throw new InvalidOperationException("Audio file is too large for transcription. Use a file smaller than 25 MB.");
        }

        var mimeType = GetMimeType(fileName);
        var endpoint = BuildListenEndpoint();

        logger.LogInformation(
            "Deepgram audio transcription request prepared. Model: {Model}. Language: {Language}. ApiKeyConfigured: {ApiKeyConfigured}. Host: {Host}. AudioBytes: {AudioBytes}. MimeType: {MimeType}.",
            _options.Model,
            _options.Language,
            !string.IsNullOrWhiteSpace(_options.ApiKey),
            GetHostForMessage(endpoint),
            memory.Length,
            mimeType);

        HttpResponseMessage response;
        try
        {
            response = await RetryableHttp.SendAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Token", _options.ApiKey);

                    var content = new ByteArrayContent(memory.ToArray());
                    content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                    request.Content = content;
                    return request;
                },
                httpClient.SendAsync,
                logger,
                "Deepgram audio transcription",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Deepgram audio transcription endpoint could not be reached.");
            throw new ApiHttpException(
                StatusCodes.Status503ServiceUnavailable,
                "Deepgram ses transkripsiyon servisine ulaşılamıyor. İnternet/DNS bağlantısını ve api.deepgram.com erişimini kontrol edip tekrar deneyin.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await ReadSafeResponseBodyAsync(response, cancellationToken);
                var errorSummary = ExtractDeepgramErrorSummary(responseBody);

                logger.LogWarning(
                    "Deepgram audio transcription failed. StatusCode: {StatusCode}. Model: {Model}. Language: {Language}. Host: {Host}. RetryAfter: {RetryAfter}. AudioBytes: {AudioBytes}. MimeType: {MimeType}. ErrorSummary: {ErrorSummary}.",
                    (int)response.StatusCode,
                    _options.Model,
                    _options.Language,
                    GetHostForMessage(endpoint),
                    FormatRetryAfter(response),
                    memory.Length,
                    mimeType,
                    errorSummary);

                throw BuildDeepgramFailureException(response, errorSummary);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var transcript = ExtractTranscript(payload.RootElement)?.Trim();

            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new InvalidOperationException("Deepgram did not return an audio transcript.");
            }

            return transcript;
        }
    }

    private string BuildListenEndpoint()
    {
        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
            ? "https://api.deepgram.com/v1/listen"
            : _options.Endpoint.Trim();

        var query = new Dictionary<string, string?>
        {
            ["model"] = string.IsNullOrWhiteSpace(_options.Model) ? "nova-3" : _options.Model.Trim(),
            ["language"] = string.IsNullOrWhiteSpace(_options.Language) ? "tr" : _options.Language.Trim(),
            ["smart_format"] = _options.SmartFormat ? "true" : "false"
        };

        return QueryHelpers.AddQueryString(endpoint, query);
    }

    private static string? ExtractTranscript(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results) ||
            !results.TryGetProperty("channels", out var channels) ||
            channels.ValueKind != JsonValueKind.Array ||
            channels.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChannel = channels[0];
        if (!firstChannel.TryGetProperty("alternatives", out var alternatives) ||
            alternatives.ValueKind != JsonValueKind.Array ||
            alternatives.GetArrayLength() == 0)
        {
            return null;
        }

        var firstAlternative = alternatives[0];
        return firstAlternative.TryGetProperty("transcript", out var transcript) &&
               transcript.ValueKind == JsonValueKind.String
            ? transcript.GetString()
            : null;
    }

    private static ApiHttpException BuildDeepgramFailureException(HttpResponseMessage response, string errorSummary)
    {
        var safeDetail = string.IsNullOrWhiteSpace(errorSummary)
            ? "Deepgram ek hata detayı döndürmedi."
            : errorSummary;

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    $"Deepgram API anahtarı doğrulanamadı veya yetkisiz. Detay: {safeDetail}"),

            System.Net.HttpStatusCode.TooManyRequests =>
                new ApiHttpException(
                    StatusCodes.Status429TooManyRequests,
                    $"{BuildRateLimitMessage(response)} Detay: {safeDetail}"),

            System.Net.HttpStatusCode.BadRequest =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    $"Deepgram transkripsiyon isteğini geçersiz buldu. Ses formatı, model veya language ayarını kontrol edin. Detay: {safeDetail}"),

            _ =>
                new ApiHttpException(
                    StatusCodes.Status502BadGateway,
                    $"Ses dosyası Deepgram transkripsiyon servisi tarafından işlenemedi (HTTP {(int)response.StatusCode}). Detay: {safeDetail}")
        };
    }

    private static async Task<string> ReadSafeResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return TruncateForDiagnostics(body);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractDeepgramErrorSummary(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            foreach (var propertyName in new[] { "err_msg", "message", "error", "detail" })
            {
                if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return TruncateForDiagnostics(value.GetString() ?? string.Empty);
                }
            }
        }
        catch (JsonException)
        {
            // Fall through and use the raw sanitized body.
        }

        return TruncateForDiagnostics(responseBody);
    }

    private static string GetMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".aiff" => "audio/aiff",
            ".aif" => "audio/aiff",
            _ => "application/octet-stream"
        };

    private static string BuildRateLimitMessage(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return $"Deepgram ses transkripsiyon limiti doldu. Lütfen yaklaşık {FormatRetryDelay(delta)} sonra tekrar deneyin.";
        }

        if (retryAfter?.Date is { } date)
        {
            var fromNow = date - DateTimeOffset.UtcNow;
            if (fromNow > TimeSpan.Zero)
            {
                return $"Deepgram ses transkripsiyon limiti doldu. Lütfen yaklaşık {FormatRetryDelay(fromNow)} sonra tekrar deneyin.";
            }
        }

        return "Deepgram ses transkripsiyon limiti doldu. Lütfen biraz bekleyip tekrar deneyin.";
    }

    private static string FormatRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return FormatRetryDelay(delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var fromNow = date - DateTimeOffset.UtcNow;
            if (fromNow > TimeSpan.Zero)
            {
                return FormatRetryDelay(fromNow);
            }
        }

        return "none";
    }

    private static string FormatRetryDelay(TimeSpan delay)
    {
        if (delay.TotalMinutes >= 1)
        {
            return $"{Math.Ceiling(delay.TotalMinutes)} dakika";
        }

        return $"{Math.Max(1, Math.Ceiling(delay.TotalSeconds))} saniye";
    }

    private static string GetHostForMessage(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri.Host
            : endpoint;
    }

    private static string TruncateForDiagnostics(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 700 ? normalized : normalized[..700];
    }
}
