using System;
using System.Security.Cryptography;
using System.Text;

namespace CloudflareTray.Services;

/// <summary>
/// Encrypts individual values with AES-GCM. Format: "enc:v1:" + Base64(nonce | tag | ciphertext).
/// </summary>
public class SecretProtector(IKeyStore keyStore)
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Lazy<byte[]> _key = new(keyStore.GetOrCreateKey);

    public static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var buffer = new byte[NonceSize + TagSize + plainBytes.Length];
        var nonce = buffer.AsSpan(0, NonceSize);
        var tag = buffer.AsSpan(NonceSize, TagSize);
        var cipher = buffer.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key.Value, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return Prefix + Convert.ToBase64String(buffer);
    }

    public string Unprotect(string value)
    {
        if (!IsProtected(value))
            throw new FormatException("Value is not encrypted.");

        var buffer = Convert.FromBase64String(value[Prefix.Length..]);
        if (buffer.Length < NonceSize + TagSize)
            throw new FormatException("Encrypted value is too short.");

        var nonce = buffer.AsSpan(0, NonceSize);
        var tag = buffer.AsSpan(NonceSize, TagSize);
        var cipher = buffer.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key.Value, TagSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
