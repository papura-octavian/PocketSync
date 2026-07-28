namespace PocketSync.Server;

/// <summary>
/// Configurația serverului. Valorile null se rezolvă la pornire:
/// din appsettings.json dacă există, altfel default-uri (Desktop\PocketSync, port 5000).
/// </summary>
public sealed class ServerConfig
{
    public int? Port { get; set; }
    public string? BasePath { get; set; }

    public static string DefaultBasePath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, "PocketSync");
    }
}
