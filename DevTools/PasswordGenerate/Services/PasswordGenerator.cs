using System.Security.Cryptography;

namespace PasswordGenerate.Services;

public enum CharsetType
{
    Digits,
    Hex,
    LowerAndDigits,
    MixedAndDigits,
    MixedAndDigitsAndSpecial
}

public static class PasswordGeneratorService
{
    private const string Digits = "0123456789";
    private const string HexChars = "0123456789abcdef";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Generate(int length, CharsetType type, string excludeChars, string specialChars)
    {
        var pool = BuildPool(type, excludeChars, specialChars);
        if (pool.Length == 0)
            return new string('?', length);

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }
        return new string(result);
    }

    private static string BuildPool(CharsetType type, string excludeChars, string specialChars)
    {
        var raw = type switch
        {
            CharsetType.Digits => Digits,
            CharsetType.Hex => HexChars,
            CharsetType.LowerAndDigits => Lower + Digits,
            CharsetType.MixedAndDigits => Lower + Upper + Digits,
            CharsetType.MixedAndDigitsAndSpecial => Lower + Upper + Digits + specialChars,
            _ => string.Empty
        };

        if (type is CharsetType.Digits or CharsetType.Hex)
            return raw;

        return string.Concat(raw.Where(c => !excludeChars.Contains(c)));
    }
}
