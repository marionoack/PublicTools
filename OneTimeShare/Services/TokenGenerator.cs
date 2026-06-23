using System.Security.Cryptography;

namespace OneTimeShare.Services;

public static class TokenGenerator
{
    /// <summary>Generates a cryptographically secure URL-safe token (24 chars from 18 random bytes).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
