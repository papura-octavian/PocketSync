using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using PocketSync.Mobile.Services;

namespace PocketSync.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// share sheet: motivul întregii aplicații — "Share → PocketSync" din orice app
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault], DataMimeType = "*/*")]
[IntentFilter([Intent.ActionSendMultiple], Categories = [Intent.CategoryDefault], DataMimeType = "*/*")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarStyle();
        HandleShareIntent(Intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplySystemBarStyle();
    }

    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBarStyle(); // s-a schimbat tema light/dark din setările telefonului
    }

    /// <summary>
    /// Bara de sus (ceas, baterie) și cea de jos în culoarea fundalului aplicației,
    /// cu iconițe închise pe tema light și deschise pe dark — altfel ies alb pe alb.
    /// </summary>
    private void ApplySystemBarStyle()
    {
        if (Window is null) return;
        var dark = (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask)
                   == Android.Content.Res.UiMode.NightYes;

#pragma warning disable CA1422 // ignorate de la Android 15 (edge-to-edge), dar necesare pe versiunile vechi
        var background = Android.Graphics.Color.ParseColor(dark ? "#0E1015" : "#F4F5FA");
        Window.SetStatusBarColor(background);
        Window.SetNavigationBarColor(background);
#pragma warning restore CA1422

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is null) return;
        controller.AppearanceLightStatusBars = !dark;
        controller.AppearanceLightNavigationBars = !dark;
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleShareIntent(intent);
    }

    private void HandleShareIntent(Intent? intent)
    {
        if (intent?.Action is not (Intent.ActionSend or Intent.ActionSendMultiple)) return;

        var uris = new List<Android.Net.Uri>();

        if (intent.Action == Intent.ActionSend)
        {
            if (GetStreamUri(intent) is { } uri) uris.Add(uri);
        }
        else
        {
            var list = GetStreamUris(intent);
            if (list is not null)
                uris.AddRange(list.OfType<Android.Net.Uri>());
        }

        if (uris.Count == 0) return;

        var items = new List<SharedItem>();
        foreach (var uri in uris)
        {
            // primești un content:// URI, nu un fișier pe disc — se citește prin ContentResolver
            var name = QueryDisplayName(uri) ?? "shared_" + DateTime.Now.Ticks;
            var length = QuerySize(uri);
            var resolver = ContentResolver!;
            items.Add(new SharedItem(name, length, () => resolver.OpenInputStream(uri)));
        }

        ShareQueue.Add(items);
    }

#pragma warning disable CA1422 // GetParcelableExtra(string) e deprecat din API 33, dar varianta nouă cere API 33+
    private Android.Net.Uri? GetStreamUri(Intent intent)
        => intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;

    private System.Collections.IList? GetStreamUris(Intent intent)
        => intent.GetParcelableArrayListExtra(Intent.ExtraStream);
#pragma warning restore CA1422

    private string? QueryDisplayName(Android.Net.Uri uri)
    {
        try
        {
            using var cursor = ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            if (cursor?.MoveToFirst() == true)
                return cursor.GetString(0);
        }
        catch { }
        return uri.LastPathSegment;
    }

    private long? QuerySize(Android.Net.Uri uri)
    {
        try
        {
            using var cursor = ContentResolver?.Query(uri, [IOpenableColumns.Size], null, null, null);
            if (cursor?.MoveToFirst() == true && !cursor.IsNull(0))
                return cursor.GetLong(0);
        }
        catch { }
        return null;
    }
}
