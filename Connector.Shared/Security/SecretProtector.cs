using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace Connector.Shared.Security;

[SupportedOSPlatform("windows")]
public static class SecretProtector
{
    public static string Protect(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var input = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(input, null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            return string.Empty;
        }

        try
        {
            var input = Convert.FromBase64String(encryptedValue);
            var unprotectedBytes = ProtectedData.Unprotect(input, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(unprotectedBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new InvalidOperationException("Nie mozna odszyfrowac zapisanego sekretu. Skonfiguruj konektor ponownie.", ex);
        }
    }
}
