using Notisight.Api.Domain.Entities;

namespace Notisight.Api.Features.Auth.Services;

public sealed record TokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    RefreshToken RefreshToken);
