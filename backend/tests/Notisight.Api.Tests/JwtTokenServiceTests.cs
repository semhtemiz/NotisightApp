using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Auth.Services;
using JwtOptionsModel = Notisight.Api.Options.JwtOptions;

namespace Notisight.Api.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateTokenSet_ReturnsAccessAndRefreshTokens()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptionsModel
        {
            Issuer = "tests",
            Audience = "tests-client",
            SigningKey = "tests-signing-key-with-sufficient-length-123456",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 7
        });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "test-user",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var service = new JwtTokenService(options);

        var tokenSet = service.CreateTokenSet(user);

        Assert.NotEmpty(tokenSet.AccessToken);
        Assert.True(tokenSet.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.NotEmpty(tokenSet.RefreshToken.Token);
        Assert.Equal(user.Id, tokenSet.RefreshToken.UserId);
        Assert.True(tokenSet.RefreshToken.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }
}
