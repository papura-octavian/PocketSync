namespace PocketSync.Server;

/// <summary>
/// ID-ul permanent al PC-ului, salvat în %APPDATA%\PocketSync\pc-id.txt.
/// Telefonul îl folosește ca să recunoască același PC când i se schimbă IP-ul sau tokenul —
/// re-scanezi QR-ul și se actualizează intrarea existentă, nu apare un PC duplicat.
/// Nu e secret: e un nume, nu o parolă.
/// </summary>
public static class PcIdentity
{
    private static string? _id;

    public static string Id => _id ??= LoadOrCreate();

    private static string LoadOrCreate()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PocketSync");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "pc-id.txt");

        try
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (Guid.TryParse(existing, out _)) return existing;
            }
        }
        catch { /* fișier corupt — generăm altul */ }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, id);
        return id;
    }
}
