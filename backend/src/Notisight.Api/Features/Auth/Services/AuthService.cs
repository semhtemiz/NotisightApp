using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Auth.Contracts;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Auth.Services;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
        }

        if (await dbContext.Users.AnyAsync(x => x.Username == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = normalizedUsername,
            DisplayName = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var tokenSet = jwtTokenService.CreateTokenSet(user);
        user.RefreshTokens.Add(tokenSet.RefreshToken);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, tokenSet);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = request.Identifier.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(x => x.RefreshTokens)
            .SingleOrDefaultAsync(
                x => x.Email == normalizedIdentifier || x.Username == normalizedIdentifier,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("E-posta veya kullanıcı adı bulunamadı.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Şifre hatalı.");
        }

        var tokenSet = jwtTokenService.CreateTokenSet(user);
        dbContext.RefreshTokens.Add(tokenSet.RefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, tokenSet);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var currentToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (currentToken is null || !currentToken.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var tokenSet = jwtTokenService.CreateTokenSet(currentToken.User);
        currentToken.RevokedAtUtc = DateTimeOffset.UtcNow;
        currentToken.ReplacedByToken = tokenSet.RefreshToken.Token;

        dbContext.RefreshTokens.Add(tokenSet.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse(currentToken.User, tokenSet);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null)
        {
            return;
        }

        refreshToken.RevokedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        }

        return CreateUserResponse(user);
    }

    public async Task<AuthUserResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        if (await dbContext.Users.AnyAsync(x => x.Id != userId && x.Email == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
        }

        if (await dbContext.Users.AnyAsync(x => x.Id != userId && x.Username == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");
        }

        user.DisplayName = displayName;
        user.Username = normalizedUsername;
        user.Email = normalizedEmail;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateUserResponse(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Mevcut şifre hatalı.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthResponse CreateResponse(User user, TokenResult tokenSet)
    {
        return new AuthResponse(
            tokenSet.AccessToken,
            tokenSet.AccessTokenExpiresAtUtc,
            tokenSet.RefreshToken.Token,
            tokenSet.RefreshToken.ExpiresAtUtc,
            CreateUserResponse(user));
    }

    private static AuthUserResponse CreateUserResponse(User user)
    {
        return new AuthUserResponse(user.Id, user.Username, user.DisplayName, user.Email);
    }
}
