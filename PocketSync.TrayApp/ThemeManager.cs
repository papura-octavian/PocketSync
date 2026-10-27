using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace PocketSync.TrayApp;

/// <summary>
/// Tema light/dark după setarea din Windows (Settings → Personalization → Colors → "app mode").
/// Se schimbă din mers dacă utilizatorul schimbă tema cât timp aplicația rulează.
/// </summary>
public static class ThemeManager
{
    private static ResourceDictionary? _current;

    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    public static void Initialize()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
                System.Windows.Application.Current.Dispatcher.Invoke(Apply);
        };
    }

    private static void Apply()
    {
        var dark = ReadWindowsDarkMode();
        if (_current is not null && dark == IsDark) return;
        IsDark = dark;

        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml")
        };
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        merged.Insert(0, dict);
        _current = dict;

        Changed?.Invoke();
    }

    private static bool ReadWindowsDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Bara de titlu a ferestrei în tema curentă (altfel rămâne albă peste o fereastră dark).</summary>
    public static void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var value = IsDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref value, sizeof(int));
    }
}
