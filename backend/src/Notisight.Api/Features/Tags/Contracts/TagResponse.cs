namespace Notisight.Api.Features.Tags.Contracts;

public sealed record TagResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc);
