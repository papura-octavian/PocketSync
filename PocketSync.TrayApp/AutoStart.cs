using Microsoft.Win32;

namespace PocketSync.TrayApp;

/// <summary>
/// Pornire cu Windows prin HKCU\...\Run — per-user, fără drepturi de admin.
/// Default OFF: utilizatorul activează conștient din Setări.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PocketSync";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public static void Enable()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{exe}\" --minimized");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
