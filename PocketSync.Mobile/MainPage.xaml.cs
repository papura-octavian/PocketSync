using PocketSync.Mobile.Services;

namespace PocketSync.Mobile;

public partial class MainPage : ContentPage
{
    private readonly ApiService _api;
    private bool _uploading;

    public record FileRow(Guid Id, string Name, string Meta);

    public MainPage(ApiService api)
    {
        InitializeComponent();
        _api = api;

        ShareQueue.ItemsArrived += () => MainThread.BeginInvokeOnMainThread(ProcessShareQueue);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _api.LoadAsync();
        UpdatePanels();

        if (_api.IsPaired)
        {
            await RefreshAsync();
            ProcessShareQueue();
        }
    }

    private void UpdatePanels()
    {
        PairPanel.IsVisible = !_api.IsPaired;
        MainPanel.IsVisible = _api.IsPaired;
    }

    // ---------------- pairing ----------------

    private async void Pair_Clicked(object? sender, EventArgs e)
    {
        PairError.IsVisible = false;
        PairButton.IsEnabled = false;
        PairButton.Text = "Se conectează...";

        var ok = await _api.PairAsync(PairingUrlEntry.Text ?? "");

        PairButton.IsEnabled = true;
        PairButton.Text = "Conectează";

        if (!ok)
        {
            PairError.Text = "Nu m-am putut conecta. Verifică link-ul și că ești pe același WiFi cu PC-ul.";
            PairError.IsVisible = true;
            return;
        }

        UpdatePanels();
        await RefreshAsync();
        ProcessShareQueue();
    }

    private async void Unpair_Clicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("PocketSync", "Ștergi conexiunea cu PC-ul?", "Da", "Nu")) return;
        SecureStorage.Remove("server_url");
        SecureStorage.Remove("auth_token");
        await _api.LoadAsync();
        UpdatePanels();
    }

    // ---------------- listă + status ----------------

    private async Task RefreshAsync()
    {
        var online = await _api.CheckStatusAsync();
        StatusDot.Fill = new SolidColorBrush(online ? Colors.Green : Colors.Red);
        StatusLabel.Text = online ? "Conectat la PC" : "PC-ul nu răspunde (același WiFi?)";

        if (!online) { FilesList.ItemsSource = null; return; }

        try
        {
            var files = await _api.ListFilesAsync();
            FilesList.ItemsSource = files
                .Select(f => new FileRow(f.Id, f.Name,
                    $"{FormatSize(f.SizeBytes)} · {f.ModifiedUtc.ToLocalTime():d MMM}"))
                .ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Eroare", "Nu am putut lista fișierele: " + ex.Message, "OK");
        }
    }

    private async void Refresh_Clicked(object? sender, EventArgs e) => await RefreshAsync();

    private async void ListRefresh_Refreshing(object? sender, EventArgs e)
    {
        await RefreshAsync();
        ListRefresh.IsRefreshing = false;
    }

    private async void File_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not FileRow row) return;
        // browserul face download-ul: progres nativ, notificare, salvare în Downloads
        await Launcher.OpenAsync(_api.DownloadUrl(row.Id));
    }

    // ---------------- upload ----------------

    private async void Upload_Clicked(object? sender, EventArgs e)
    {
        var picked = await FilePicker.PickMultipleAsync();
        if (picked is null) return;

        var items = new List<SharedItem>();
        foreach (var p in picked)
        {
            var stream = await p.OpenReadAsync();
            if (stream is null) continue;
            var length = stream.CanSeek ? stream.Length : (long?)null;
            items.Add(new SharedItem(p.FileName, length, () => stream));
        }
        if (items.Count == 0) return;

        await UploadItemsAsync(items);
    }

    private void ProcessShareQueue()
    {
        var items = ShareQueue.TakeAll();
        if (items.Count == 0) return;

        if (!_api.IsPaired)
        {
            _ = DisplayAlertAsync("PocketSync", "Conectează-te întâi la PC, apoi dă share din nou.", "OK");
            return;
        }
        _ = UploadItemsAsync(items);
    }

    private async Task UploadItemsAsync(List<SharedItem> items)
    {
        if (_uploading) { await DisplayAlertAsync("PocketSync", "Așteaptă să se termine transferul curent.", "OK"); return; }
        _uploading = true;
        UploadButton.IsEnabled = false;
        UploadProgress.IsVisible = true;
        UploadStatus.IsVisible = true;

        var sent = 0;
        try
        {
            foreach (var item in items)
            {
                var label = items.Count > 1 ? $" ({sent + 1}/{items.Count})" : "";
                UploadStatus.Text = $"Se trimite{label}: {item.Name}";
                UploadProgress.Progress = 0;

                using var stream = item.OpenStream();
                if (stream is null) continue;

                var progress = new Progress<(long Sent, long Total)>(p =>
                {
                    if (p.Total > 0) UploadProgress.Progress = (double)p.Sent / p.Total;
                });

                await _api.UploadAsync(stream, item.Name, item.Length, progress);
                sent++;
            }

            UploadStatus.Text = sent == 1 ? "Trimis." : $"Trimise: {sent} fișiere.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            UploadStatus.Text = "Eroare: " + ex.Message;
            await DisplayAlertAsync("Eroare la trimitere",
                ex.Message + "\n\nVerifică WiFi-ul și încearcă din nou.", "OK");
        }
        finally
        {
            _uploading = false;
            UploadButton.IsEnabled = true;
            UploadProgress.IsVisible = false;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}
