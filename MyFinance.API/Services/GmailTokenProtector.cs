using System.Security.Cryptography;
using System.Text;

namespace MyFinance.API.Services;

public interface IGmailTokenProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public sealed class AesGcmGmailTokenProtector : IGmailTokenProtector
{
    private readonly byte[]? _key;

    public AesGcmGmailTokenProtector(GmailIntegrationOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.TokenEncryptionKey)) return;
        try { _key = Convert.FromBase64String(options.TokenEncryptionKey); }
        catch (FormatException ex) { throw new InvalidOperationException("GmailIntegration:TokenEncryptionKey deve ser Base64.", ex); }
        if (_key.Length is not (16 or 24 or 32)) throw new InvalidOperationException("GmailIntegration:TokenEncryptionKey deve ter 16, 24 ou 32 bytes.");
    }

    public string Protect(string value)
    {
        EnsureConfigured();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        using var aes = new AesGcm(_key!, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    public string Unprotect(string value)
    {
        EnsureConfigured();
        var payload = Convert.FromBase64String(value);
        if (payload.Length < 28) throw new CryptographicException("Token criptografado inválido.");
        var plaintext = new byte[payload.Length - 28];
        using var aes = new AesGcm(_key!, 16);
        aes.Decrypt(payload[..12], payload[28..], payload[12..28], plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private void EnsureConfigured()
    {
        if (_key is null) throw new InvalidOperationException("GmailIntegration:TokenEncryptionKey não configurada.");
    }
}
