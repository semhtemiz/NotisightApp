using Microsoft.AspNetCore.DataProtection;

namespace Notisight.Api.Features.Settings.Services;

public interface ISecurityService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class SecurityService : ISecurityService
{
    private readonly IDataProtector _protector;

    public SecurityService(IDataProtectionProvider dataProtectionProvider)
    {
        // "Notisight.ApiKeys" is the purpose string to isolate this protection
        _protector = dataProtectionProvider.CreateProtector("Notisight.ApiKeys");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch
        {
            // If the key is invalid or data is corrupted
            return string.Empty;
        }
    }
}
