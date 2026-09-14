using System;
using System.Security.Cryptography;
using System.Text;

namespace StreamDesk.Infrastructure.Storage;

/// <summary>
/// Protects provider API secrets at rest with Windows DPAPI (user scope).
/// Falls back to reversible obfuscation on platforms without DPAPI so the app
/// keeps working; secrets never leave the machine and are never logged.
/// </summary>
public static class SecretProtector
{
    private const string EntropyPrefix = "StreamDesk.Secret.v1:";

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        if (OperatingSystem.IsWindows())
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = ProtectedData.Protect(bytes, Entropy(), DataProtectionScope.CurrentUser);
            return "dpapi:" + Convert.ToBase64String(protectedBytes);
        }

        return "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return string.Empty;
        }

        if (stored.StartsWith("dpapi:", StringComparison.Ordinal) && OperatingSystem.IsWindows())
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored["dpapi:".Length..]), Entropy(), DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        if (stored.StartsWith("b64:", StringComparison.Ordinal))
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(stored["b64:".Length..]));
        }

        // Unknown format: return as-is so plain values written by older versions still load.
        return stored;
    }

    private static byte[] Entropy() => Encoding.UTF8.GetBytes(EntropyPrefix);
}
