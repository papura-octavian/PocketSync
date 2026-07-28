using System.IO;
using System.Text.Json;

namespace PocketSync.TrayApp;

/// <summary>Setările tray app-ului, persistate în %APPDATA%\PocketSync\settings.json.</summary>
public sealed class TrayAppSettings
{
    public string? StorageFolder { get; set; }
    public int Port { get; set; } = 5000;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PocketSync", "settings.json");

    public static TrayAppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<TrayAppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* fișier corupt — pornim cu default-uri */ }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
