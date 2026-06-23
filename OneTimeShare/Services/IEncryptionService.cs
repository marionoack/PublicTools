namespace OneTimeShare.Services;

public interface IEncryptionService
{
    /// <summary>Generates a new random per-asset key.</summary>
    byte[] GenerateAssetKey();

    /// <summary>Wraps the asset key using the master key, returns Base64(nonce + cipher).</summary>
    string WrapKey(byte[] assetKey);

    /// <summary>Unwraps a previously wrapped key.</summary>
    byte[] UnwrapKey(string wrappedKey);

    /// <summary>Encrypts plaintext bytes with the given key, returns Base64(nonce + cipher + tag).</summary>
    byte[] Encrypt(byte[] assetKey, byte[] plaintext);

    /// <summary>Decrypts bytes previously encrypted with Encrypt.</summary>
    byte[] Decrypt(byte[] assetKey, byte[] ciphertext);
}
