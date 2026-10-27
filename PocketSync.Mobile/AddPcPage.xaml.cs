using PocketSync.Mobile.Services;
#if ANDROID
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
#endif

namespace PocketSync.Mobile;

/// <summary>
/// Adăugarea unui PC: scanezi QR-ul din fereastra PocketSync (sau lipești link-ul).
/// Dacă PC-ul e deja în listă (același ID), doar i se actualizează adresa și tokenul.
/// </summary>
public partial class AddPcPage : ContentPage
{
    private readonly ApiService _api;
    private readonly PcStore _store;
    private bool _busy;

#if ANDROID
    private CameraBarcodeReaderView? _scanner;
#endif

    public AddPcPage(ApiService api, PcStore store)
    {
        InitializeComponent();
        _api = api;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _store.LoadAsync();
        await StartCameraAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopCamera();
    }

    // ---------------- camera ----------------

    private async Task StartCameraAsync()
    {
#if ANDROID
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            ShowCameraOff("PocketSync are nevoie de cameră ca să scaneze codul QR. Poți și să lipești link-ul mai jos.", canRetry: true);
            return;
        }

        CameraOff.IsVisible = false;
        ScanFrame.IsVisible = true;

        // vizorul se creează la fiecare intrare pe pagină și se distruge la ieșire —
        // altfel camera rămâne deschisă în fundal și consumă baterie
        _scanner = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormat.QrCode,
                AutoRotate = true,
                Multiple = false
            },
            IsDetecting = true
        };
        _scanner.BarcodesDetected += Scanner_BarcodesDetected;
        CameraHost.Content = _scanner;
#else
        await Task.CompletedTask;
        ShowCameraOff("Scanarea QR merge doar pe telefon. Lipește link-ul mai jos.", canRetry: false);
#endif
    }

    private void StopCamera()
    {
#if ANDROID
        if (_scanner is null) return;
        _scanner.IsDetecting = false;
        _scanner.BarcodesDetected -= Scanner_BarcodesDetected;
        CameraHost.Content = null;
        _scanner.Handler?.DisconnectHandler();
        _scanner = null;
#endif
    }

#if ANDROID
    private void Scanner_BarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(value)) return;

        // detecția vine de pe thread-ul camerei, de mai multe ori pe secundă
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_busy || _scanner is null) return;
            if (!ApiService.TryParsePairingUrl(value, out _, out _))
            {
                ShowError("Codul ăsta nu e de la PocketSync. Scanează codul QR din fereastra PocketSync de pe PC.");
                return;
            }
            _scanner.IsDetecting = false;
            if (!await ConnectAsync(value) && _scanner is not null)
                _scanner.IsDetecting = true;
        });
    }
#endif

    private void ShowCameraOff(string text, bool canRetry)
    {
        CameraOffText.Text = text;
        CameraRetryButton.IsVisible = canRetry;
        CameraOff.IsVisible = true;
        ScanFrame.IsVisible = false;
    }

    private async void CameraRetry_Clicked(object? sender, EventArgs e)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        // după un refuz definitiv, Android nu mai arată dialogul — doar din setări se poate permite
        if (status == PermissionStatus.Denied && !Permissions.ShouldShowRationale<Permissions.Camera>())
        {
            AppInfo.ShowSettingsUI();
            return;
        }
        await StartCameraAsync();
    }

    // ---------------- conectare ----------------

    private async void Connect_Clicked(object? sender, EventArgs e)
        => await ConnectAsync(LinkEntry.Text ?? "");

    private async void Back_Tapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async Task<bool> ConnectAsync(string link)
    {
        if (_busy) return false;
        ErrorPanel.IsVisible = false;

        if (!ApiService.TryParsePairingUrl(link, out var baseUrl, out var token))
        {
            ShowError("Link-ul nu e valid. Trebuie să arate așa: http://192.168.x.x:5000/?token=...");
            return false;
        }

        _busy = true;
        BusyOverlay.IsVisible = true;
        ConnectButton.IsEnabled = false;
        try
        {
            PcInfo info;
            try
            {
                info = await _api.GetDeviceAsync(baseUrl, token);
            }
            catch (UnauthorizedPcException)
            {
                ShowError("Codul ăsta e vechi — tokenul s-a regenerat pe PC. Scanează codul afișat acum în fereastra PocketSync.");
                return false;
            }
            catch
            {
                ShowError($"Nu găsesc PC-ul la {new Uri(baseUrl).Authority}. Verifică să fie pornit PocketSync și să fiți pe același WiFi.");
                return false;
            }

            var (pc, isNew) = _store.Find(info.Id, baseUrl);
            pc.PcId ??= info.Id;
            pc.BaseUrl = baseUrl;
            pc.Token = token;

            if (isNew)
            {
                BusyOverlay.IsVisible = false;
                var name = await DisplayPromptAsync("PC nou găsit",
                    "Cum vrei să se numească? (ex: PC acasă, Laptop muncă)",
                    "Salvează", "Anulează", initialValue: info.Name, maxLength: 40);
                if (name is null) return false; // a renunțat

                pc.Name = string.IsNullOrWhiteSpace(name) ? info.Name : name.Trim();
                await _store.AddAsync(pc);
            }
            else
            {
                await _store.SaveAsync();
            }

            _store.SetActive(pc);
            await Shell.Current.GoToAsync("//" + nameof(MainPage));
            if (!isNew)
                await Shell.Current.CurrentPage.DisplayAlertAsync("PocketSync",
                    $"„{pc.DisplayName}” e deja în listă — i-am actualizat adresa.", "OK");
            return true;
        }
        finally
        {
            _busy = false;
            BusyOverlay.IsVisible = false;
            ConnectButton.IsEnabled = true;
        }
    }

    private void ShowError(string text)
    {
        ErrorLabel.Text = text;
        ErrorPanel.IsVisible = true;
    }
}
