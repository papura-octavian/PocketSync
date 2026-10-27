using PocketSync.Mobile.Services;

namespace PocketSync.Mobile;

public record FileRow(Guid Id, string Name, string Ext, string Meta);

public partial class MainPage : ContentPage
{
    private readonly ApiService _api;
    private readonly PcStore _store;
    private bool _uploading;
    private bool _choosingTarget;

    public MainPage(ApiService api, PcStore store)
    {
        InitializeComponent();
        _api = api;
        _store = store;

        ShareQueue.ItemsArrived += () => MainThread.BeginInvokeOnMainThread(ProcessShareQueue);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _store.LoadAsync();
        UpdatePanels();

        if (_store.Active is not null)
        {
            await RefreshAsync();
            ProcessShareQueue();
        }
    }

    private void UpdatePanels()
    {
        var active = _store.Active;
        WelcomePanel.IsVisible = active is null;
        MainPanel.IsVisible = active is not null;
        if (active is not null) PcNameLabel.Text = active.DisplayName;
    }

    // ---------------- PC-uri ----------------

    private async void AddPc_Clicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(AddPcPage));

    private async void Devices_Tapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(DevicesPage));

    /// <summary>Tap pe numele PC-ului: alegi rapid alt PC, fără să intri în listă.</summary>
    private async void PcPicker_Tapped(object? sender, TappedEventArgs e)
    {
        const string manage = "Gestionează PC-urile…";
        const string add = "＋ Adaugă un PC";

        var pcs = _store.All;
        var active = _store.Active;
        var names = pcs.Select(p => p == active ? "✓  " + p.DisplayName : p.DisplayName).ToList();
        names.Add(add);
        names.Add(manage);

        var choice = await DisplayActionSheetAsync("Cu ce PC lucrezi?", "Anulează", null, names.ToArray());
        if (choice is null or "Anulează") return;
        if (choice == manage) { await Shell.Current.GoToAsync(nameof(DevicesPage)); return; }
        if (choice == add) { await Shell.Current.GoToAsync(nameof(AddPcPage)); return; }

        var index = names.IndexOf(choice);
        if (index < 0 || index >= pcs.Count) return;

        _store.SetActive(pcs[index]);
        UpdatePanels();
        await RefreshAsync();
    }

    private async void Rescan_Clicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(AddPcPage));

    // ---------------- listă + status ----------------

    private async Task RefreshAsync()
    {
        var pc = _store.Active;
        if (pc is null) return;

        SetStatus(null, "Se verifică...");
        var (state, info) = await _api.CheckAsync(pc);
        if (_store.Active != pc) return; // între timp s-a ales alt PC

        // migrarea din v1.0 nu știe numele și ID-ul PC-ului — le completăm la prima conexiune
        if (info is not null && (pc.PcId is null && info.Id is not null || string.IsNullOrEmpty(pc.Name)))
        {
            pc.PcId ??= info.Id;
            if (string.IsNullOrEmpty(pc.Name)) pc.Name = info.Name;
            await _store.SaveAsync();
            UpdatePanels();
        }

        OfflinePanel.IsVisible = state != PcState.Online;
        UploadButton.IsEnabled = state == PcState.Online;
        UploadButton.Opacity = state == PcState.Online ? 1 : 0.55;
        switch (state)
        {
            case PcState.Online:
                SetStatus(Color.FromArgb("#22C55E"), $"Online · {pc.Host}");
                break;
            case PcState.Unauthorized:
                SetStatus(Color.FromArgb("#F59E0B"), "Trebuie reconectat");
                OfflineTitle.Text = "PC-ul a schimbat codul de acces";
                OfflineText.Text = "Pe PC s-a regenerat tokenul. Scanează din nou codul QR din fereastra PocketSync.";
                break;
            default:
                SetStatus(Color.FromArgb("#EF4444"), $"Offline · {pc.Host}");
                OfflineTitle.Text = "PC-ul nu răspunde";
                OfflineText.Text = "Verifică să fie pornit PocketSync pe PC și să fiți pe același WiFi. " +
                                   "Dacă PC-ul și-a schimbat adresa, scanează din nou codul QR.";
                break;
        }

        if (state != PcState.Online)
        {
            // cardul de eroare de sus explică deja; lista doar se golește
            FilesList.ItemsSource = null;
            EmptyCard.IsVisible = false;
            FilesHint.Text = "Apar aici imediat ce PC-ul e din nou online.";
            return;
        }

        try
        {
            var files = await _api.ListFilesAsync(pc);
            if (_store.Active != pc) return;

            EmptyCard.IsVisible = files.Count == 0;
            FilesHint.Text = files.Count == 0
                ? "Din folderul Storage de pe PC."
                : $"{files.Count} {(files.Count == 1 ? "fișier" : "fișiere")} · atinge ca să descarci, glisează ca să ștergi.";
            FilesList.ItemsSource = files.Select(ToRow).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Eroare", "Nu am putut lista fișierele: " + ex.Message, "OK");
        }
    }

    private void SetStatus(Color? dot, string text)
    {
        StatusDot.Fill = new SolidColorBrush(dot ?? Colors.Gray);
        StatusLabel.Text = text;
    }

    private static FileRow ToRow(FileItem f)
    {
        var ext = Path.GetExtension(f.Name).TrimStart('.');
        ext = ext.Length is > 0 and <= 4 ? ext.ToUpperInvariant() : "FIȘ";
        return new FileRow(f.Id, f.Name, ext,
            $"{FormatSize(f.SizeBytes)} · {f.ModifiedUtc.ToLocalTime():d MMM, HH:mm}");
    }

    private async void Refresh_Tapped(object? sender, TappedEventArgs e) => await RefreshAsync();

    private async void Retry_Clicked(object? sender, EventArgs e) => await RefreshAsync();

    private async void ListRefresh_Refreshing(object? sender, EventArgs e)
    {
        await RefreshAsync();
        ListRefresh.IsRefreshing = false;
    }

    private async void File_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not FileRow row || _store.Active is not { } pc) return;
        // browserul face download-ul: progres nativ, notificare, salvare în Downloads
        await Launcher.OpenAsync(ApiService.DownloadUrl(pc, row.Id));
    }

    private async void DeleteFile_Invoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not FileRow row || _store.Active is not { } pc) return;
        if (!await DisplayAlertAsync("Ștergi fișierul?",
                $"„{row.Name}” se mută în Recycle Bin pe {pc.DisplayName}.", "Șterge", "Anulează")) return;

        try
        {
            await _api.DeleteFileAsync(pc, row.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Eroare", "Nu am putut șterge: " + ex.Message, "OK");
        }
        await RefreshAsync();
    }

    // ---------------- upload ----------------

    private async void Upload_Clicked(object? sender, EventArgs e)
    {
        if (_store.Active is not { } pc) return;

        var picked = await FilePicker.PickMultipleAsync();
        if (picked is null) return;

        var items = new List<SharedItem>();
        foreach (var p in picked)
        {
            if (p is null) continue;
            var stream = await p.OpenReadAsync();
            if (stream is null) continue;
            var length = stream.CanSeek ? stream.Length : (long?)null;
            items.Add(new SharedItem(p.FileName, length, () => stream));
        }
        if (items.Count == 0) return;

        await UploadItemsAsync(pc, items);
    }

    /// <summary>Fișiere venite prin "Share → PocketSync". Cu mai multe PC-uri, întrebăm pe care.</summary>
    private async void ProcessShareQueue()
    {
        // OnAppearing și evenimentul de share pot veni aproape simultan — o singură întrebare
        if (ShareQueue.Count == 0 || _choosingTarget) return;
        _choosingTarget = true;
        try { await ProcessShareQueueCoreAsync(); }
        finally { _choosingTarget = false; }
    }

    private async Task ProcessShareQueueCoreAsync()
    {
        await _store.LoadAsync();

        var pcs = _store.All;
        if (pcs.Count == 0)
        {
            ShareQueue.TakeAll();
            await DisplayAlertAsync("PocketSync", "Adaugă întâi un PC, apoi dă share din nou.", "OK");
            return;
        }

        var target = _store.Active!;
        if (pcs.Count > 1)
        {
            // PC-ul activ primul, ca să fie la un tap distanță
            var ordered = pcs.OrderByDescending(p => p == target).ToList();
            var choice = await DisplayActionSheetAsync(
                $"Trimiți {ShareQueue.Count} {(ShareQueue.Count == 1 ? "fișier" : "fișiere")} pe:",
                "Anulează", null, ordered.Select(p => p.DisplayName).ToArray());

            var picked = ordered.FirstOrDefault(p => p.DisplayName == choice);
            if (picked is null) { ShareQueue.TakeAll(); return; }
            target = picked;
        }

        var items = ShareQueue.TakeAll();
        if (items.Count > 0) _ = UploadItemsAsync(target, items);
    }

    private async Task UploadItemsAsync(SavedPc pc, List<SharedItem> items)
    {
        if (_uploading) { await DisplayAlertAsync("PocketSync", "Așteaptă să se termine transferul curent.", "OK"); return; }
        _uploading = true;
        UploadButton.IsVisible = false;
        UploadResult.IsVisible = false;
        UploadPanel.IsVisible = true;

        var sent = 0;
        try
        {
            foreach (var item in items)
            {
                var label = items.Count > 1 ? $"{sent + 1} din {items.Count} · " : "";
                UploadStatus.Text = item.Name;
                UploadDetail.Text = $"{label}către {pc.DisplayName}";
                UploadProgress.Progress = 0;

                using var stream = item.OpenStream();
                if (stream is null) continue;

                var started = DateTime.UtcNow;
                var progress = new Progress<(long Sent, long Total)>(p =>
                {
                    if (p.Total > 0) UploadProgress.Progress = (double)p.Sent / p.Total;
                    var seconds = (DateTime.UtcNow - started).TotalSeconds;
                    var speed = seconds > 0.3 ? $" · {p.Sent / 1024.0 / 1024 / seconds:F1} MB/s" : "";
                    UploadDetail.Text = $"{label}{FormatSize(p.Sent)}" +
                                        (p.Total > 0 ? $" din {FormatSize(p.Total)}" : "") + speed;
                });

                await _api.UploadAsync(pc, stream, item.Name, item.Length, progress);
                sent++;
            }

            UploadResult.Text = sent == 1 ? $"✓  Trimis pe {pc.DisplayName}" : $"✓  {sent} fișiere trimise pe {pc.DisplayName}";
            UploadResult.IsVisible = true;
            if (pc == _store.Active) await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Eroare la trimitere",
                ex.Message + "\n\nVerifică WiFi-ul și încearcă din nou.", "OK");
        }
        finally
        {
            _uploading = false;
            UploadPanel.IsVisible = false;
            UploadButton.IsVisible = true;
        }
    }

    // ---------------- comenzi PC ----------------

    private async void Power_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view || _store.Active is not { } pc) return;
        await RunPowerAsync(pc, view.ClassId);
    }

    private async void Abort_Clicked(object? sender, EventArgs e)
    {
        if (_store.Active is { } pc) await RunPowerAsync(pc, "abort");
    }

    private async Task RunPowerAsync(SavedPc pc, string action)
    {
        var confirmText = action switch
        {
            "shutdown" => $"Sigur închizi {pc.DisplayName}?",
            "restart" => $"Sigur repornești {pc.DisplayName}?",
            "sleep" => $"{pc.DisplayName} intră în sleep. Continui?",
            _ => null // lock și abort sunt inofensive
        };
        if (confirmText is not null &&
            !await DisplayAlertAsync("PocketSync", confirmText, "Da", "Nu")) return;

        PowerMsg.IsVisible = true;
        try
        {
            PowerMsg.Text = await _api.PowerAsync(pc, action);
            // la shutdown/restart ai 15 secunde de răzgândit
            AbortButton.IsVisible = action is "shutdown" or "restart";
        }
        catch (Exception ex)
        {
            PowerMsg.Text = "Eroare: " + ex.Message;
        }
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}
