namespace Notisight.Api.Features.Auth.Contracts;

public sealed record AuthUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Email);
