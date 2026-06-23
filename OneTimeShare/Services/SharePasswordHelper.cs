using Microsoft.AspNetCore.Identity;

namespace OneTimeShare.Services;

/// <summary>Hashes and verifies optional share passwords using ASP.NET Core's PasswordHasher.</summary>
public static class SharePasswordHelper
{
    private static readonly PasswordHasher<object> Hasher = new();

    public static string Hash(string password) =>
        Hasher.HashPassword(new object(), password);

    public static bool Verify(string password, string hash) =>
        Hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
}
