namespace Notisight.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    string Message,
    DateTimeOffset TimestampUtc);
