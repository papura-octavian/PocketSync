using System.Security.Cryptography;
using System.Text;

namespace PocketSync.Server;

/// <summary>
/// Tokenul de pairing: generat cu RandomNumberGenerator (nu Random, nu Guid),
/// salvat criptat cu DPAPI (CurrentUser) în %APPDATA%\PocketSync\token.bin.
/// </summary>
public sealed class TokenStore
{
    private readonly string _filePath;

    public string Token { get; private set; } = "";

    public TokenStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PocketSync");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "token.bin");

        Token = LoadOrCreate();
    }

    private string LoadOrCreate()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var encrypted = File.ReadAllBytes(_filePath);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var token = Encoding.UTF8.GetString(plain);
                if (!string.IsNullOrWhiteSpace(token))
                    return token;
            }
        }
        catch
        {
            // fișier corupt sau copiat de pe alt PC — DPAPI nu-l poate decripta; generăm unul nou
        }

        return Regenerate();
    }

    /// <summary>Generează un token nou și invalidează implicit toate pairing-urile vechi.</summary>
    public string Regenerate()
    {
        // hex, nu base64: fără caractere speciale, sigur de pus într-un URL
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);

        Token = token;
        return token;
    }

    /// <summary>Comparație în timp constant — durata nu dezvăluie câte caractere s-au potrivit.</summary>
    public bool IsValid(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;

        var a = Encoding.UTF8.GetBytes(candidate);
        var b = Encoding.UTF8.GetBytes(Token);
        if (a.Length != b.Length) return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
