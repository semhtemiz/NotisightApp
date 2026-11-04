using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Auth.Contracts;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Auth.Services;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IJwtTokenService jwtTokenService) : IAuthService
{
    private const string InvalidCredentialsMessage = "Giriş bilgileri geçersiz.";

    // Keeps the cost of an unknown-account login equal to a real one, so response time
    // cannot be used to enumerate registered users.
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()));

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
            _ = BCrypt.Net.BCrypt.Verify(request.Password, DummyPasswordHash.Value);
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var tokenSet = jwtTokenService.CreateTokenSet(user);
        dbContext.RefreshTokens.Add(tokenSet.RefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, tokenSet);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var currentToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Token == tokenHash, cancellationToken);

        if (currentToken is null)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        if (!currentToken.IsActive)
        {
            // A revoked or already-rotated token was presented again, which means an earlier
            // copy leaked. Sign the whole token family out.
            await RevokeSessionsAsync(currentToken.UserId, cancellationToken);
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
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = RefreshTokenHasher.Hash(request.RefreshToken);
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.Token == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return;
        }

        await RevokeSessionsAsync(refreshToken.UserId, cancellationToken);
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

        // A stolen refresh token must not outlive the password that was changed to evict it.
        await RevokeSessionsAsync(userId, cancellationToken);
    }

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthResponse CreateResponse(User user, TokenResult tokenSet)
    {
        return new AuthResponse(
            tokenSet.AccessToken,
            tokenSet.AccessTokenExpiresAtUtc,
            tokenSet.RefreshTokenValue,
            tokenSet.RefreshToken.ExpiresAtUtc,
            CreateUserResponse(user));
    }

    private static AuthUserResponse CreateUserResponse(User user)
    {
        return new AuthUserResponse(user.Id, user.Username, user.DisplayName, user.Email);
    }
}
