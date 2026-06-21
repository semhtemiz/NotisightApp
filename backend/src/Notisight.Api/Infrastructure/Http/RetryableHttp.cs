namespace Notisight.Api.Infrastructure.Http;

public static class RetryableHttp
{
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5)
    ];

    public static async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync,
        ILogger logger,
        string operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            HttpResponseMessage response;
            try
            {
                response = await sendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < Delays.Length)
            {
                logger.LogWarning(
                    ex,
                    "Transient network error during {Operation}. Retrying attempt {Attempt}.",
                    operation,
                    attempt + 1);

                await Task.Delay(Delays[attempt], cancellationToken);
                continue;
            }

            if (!IsTransient(response.StatusCode) || attempt >= Delays.Length)
            {
                return response;
            }

            logger.LogWarning(
                "Transient HTTP {StatusCode} during {Operation}. Retrying attempt {Attempt}.",
                (int)response.StatusCode,
                operation,
                attempt + 1);

            var delay = GetDelay(response, attempt);
            if (delay > TimeSpan.FromSeconds(15))
            {
                logger.LogWarning(
                    "Retry-After for {Operation} is {DelaySeconds} seconds; returning response without blocking the request.",
                    operation,
                    delay.TotalSeconds);
                return response;
            }

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode is
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;

    private static TimeSpan GetDelay(HttpResponseMessage response, int attempt)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests &&
            response.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
            {
                return delta;
            }

            if (retryAfter.Date is { } date)
            {
                var fromNow = date - DateTimeOffset.UtcNow;
                if (fromNow > TimeSpan.Zero)
                {
                    return fromNow;
                }
            }
        }

        return Delays[attempt];
    }
}
