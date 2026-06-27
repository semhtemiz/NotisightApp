namespace Notisight.Api.Infrastructure.Auth;

public interface ICurrentUser
{
    Guid GetRequiredUserId();
}
