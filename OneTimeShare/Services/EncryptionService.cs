using System.Security.Cryptography;

namespace OneTimeShare.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;

    public EncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:MasterKey"]
            ?? throw new InvalidOperationException("Encryption:MasterKey is not configured.");
        _masterKey = Convert.FromBase64String(keyBase64);
        if (_masterKey.Length != 32)
            throw new InvalidOperationException("Encryption:MasterKey must be 32 bytes (256-bit), Base64-encoded.");
    }

    public byte[] GenerateAssetKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    public string WrapKey(byte[] assetKey)
    {
        return Convert.ToBase64String(EncryptWithKey(_masterKey, assetKey));
    }

    public byte[] UnwrapKey(string wrappedKey)
    {
        return DecryptWithKey(_masterKey, Convert.FromBase64String(wrappedKey));
    }

    public byte[] Encrypt(byte[] assetKey, byte[] plaintext)
    {
        return EncryptWithKey(assetKey, plaintext);
    }

    public byte[] Decrypt(byte[] assetKey, byte[] ciphertext)
    {
        return DecryptWithKey(assetKey, ciphertext);
    }

    private static byte[] EncryptWithKey(byte[] key, byte[] plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Layout: [12 nonce][16 tag][ciphertext]
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        return result;
    }

    private static byte[] DecryptWithKey(byte[] key, byte[] data)
    {
        const int nonceLen = 12;
        const int tagLen = 16;

        if (data.Length < nonceLen + tagLen)
            throw new CryptographicException("Invalid encrypted data.");

        var nonce = data[..nonceLen];
        var tag = data[nonceLen..(nonceLen + tagLen)];
        var ciphertext = data[(nonceLen + tagLen)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagLen);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
