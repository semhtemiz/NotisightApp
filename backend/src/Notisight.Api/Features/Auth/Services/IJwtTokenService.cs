using Notisight.Api.Domain.Entities;

namespace Notisight.Api.Features.Auth.Services;

public interface IJwtTokenService
{
    TokenResult CreateTokenSet(User user);
}
