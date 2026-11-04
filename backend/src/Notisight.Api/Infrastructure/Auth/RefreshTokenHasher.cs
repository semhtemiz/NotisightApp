using System.Security.Cryptography;
using System.Text;

namespace Notisight.Api.Infrastructure.Auth;

/// <summary>
/// Refresh tokens are stored as digests so a database read never yields a usable session.
/// Lowercase hex keeps the lookup correct under SQL Server's case-insensitive collation.
/// </summary>
public static class RefreshTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
