using PocketSync.Mobile.Services;

namespace PocketSync.Mobile;

public record DeviceRow(SavedPc Pc, string Name, string Host, bool IsActive,
    string StateText, Color StateColor, Color CardStroke, double CardStrokeThickness);

/// <summary>Lista PC-urilor legate: alegi PC-ul activ, redenumești, ștergi, adaugi.</summary>
public partial class DevicesPage : ContentPage
{
    private readonly ApiService _api;
    private readonly PcStore _store;

    // starea fiecărui PC, după cheie — se completează pe rând, pe măsură ce răspund
    private readonly Dictionary<string, PcState> _states = [];

    public DevicesPage(ApiService api, PcStore store)
    {
        InitializeComponent();
        _api = api;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _store.LoadAsync();
        _states.Clear();
        Render();

        // toate PC-urile verificate în paralel; fiecare își actualizează bulina când răspunde
        await Task.WhenAll(_store.All.ToList().Select(async pc =>
        {
            var (state, _) = await _api.CheckAsync(pc);
            _states[pc.Key] = state;
            Render();
        }));
    }

    private void Render()
    {
        var active = _store.Active;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var border = Color.FromArgb(dark ? "#272B36" : "#E5E7EF");
        var accent = Color.FromArgb("#6366F1");

        DevicesList.ItemsSource = _store.All.Select(pc =>
        {
            var (text, color) = _states.TryGetValue(pc.Key, out var s)
                ? s switch
                {
                    PcState.Online => ("Online", Color.FromArgb("#22C55E")),
                    PcState.Unauthorized => ("Trebuie scanat din nou", Color.FromArgb("#F59E0B")),
                    _ => ("Offline", Color.FromArgb("#EF4444"))
                }
                : ("Se verifică...", Colors.Gray);

            var isActive = pc == active;
            return new DeviceRow(pc, pc.DisplayName, pc.Host, isActive, text, color,
                isActive ? accent : border, isActive ? 2 : 1);
        }).ToList();
    }

    private async void Back_Tapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

    private async void Add_Clicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(AddPcPage));

    private async void Device_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DeviceRow row) return;
        _store.SetActive(row.Pc);
        await Shell.Current.GoToAsync("..");
    }

    private async void More_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DeviceRow row) return;
        var pc = row.Pc;

        const string rename = "Redenumește";
        const string rescan = "Actualizează (scanează QR din nou)";
        const string remove = "Șterge din listă";

        var choice = await DisplayActionSheetAsync(pc.DisplayName, "Anulează", remove, rename, rescan);
        switch (choice)
        {
            case rename:
                var name = await DisplayPromptAsync("Redenumește", "Cum vrei să se numească PC-ul?",
                    "Salvează", "Anulează", initialValue: pc.DisplayName, maxLength: 40);
                if (string.IsNullOrWhiteSpace(name)) return;
                pc.Name = name.Trim();
                await _store.SaveAsync();
                Render();
                break;

            case rescan:
                await Shell.Current.GoToAsync(nameof(AddPcPage));
                break;

            case remove:
                if (!await DisplayAlertAsync("Ștergi PC-ul?",
                        $"„{pc.DisplayName}” dispare din listă. Îl poți adăuga oricând la loc scanând codul QR.",
                        "Șterge", "Anulează")) return;
                await _store.RemoveAsync(pc);
                if (_store.All.Count == 0) { await Shell.Current.GoToAsync(".."); return; }
                Render();
                break;
        }
    }
}
