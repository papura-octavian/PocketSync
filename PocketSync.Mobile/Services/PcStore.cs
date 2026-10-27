using System.Text.Json;

namespace PocketSync.Mobile.Services;

/// <summary>Un PC legat de telefon. Tokenul e per PC — fiecare PC are serverul și secretul lui.</summary>
public sealed class SavedPc
{
    /// <summary>Cheie locală, stabilă — nu depinde de IP sau de ce întoarce serverul.</summary>
    public string Key { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ID-ul permanent al PC-ului (din /api/device). Null pentru servere 1.0, care nu-l au.</summary>
    public string? PcId { get; set; }

    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;

    public string Host => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var u) ? u.Authority : BaseUrl;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Host : Name;
}

/// <summary>
/// Lista de PC-uri, salvată criptat în SecureStorage (conține tokenuri).
/// PC-ul activ (cel cu care faci transferul) e ținut separat, în Preferences — nu e secret.
/// </summary>
public sealed class PcStore
{
    private const string PcsKey = "saved_pcs";
    private const string ActiveKey = "active_pc";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private List<SavedPc> _pcs = [];
    private bool _loaded;

    public IReadOnlyList<SavedPc> All => _pcs;

    public SavedPc? Active
    {
        get
        {
            var key = Preferences.Get(ActiveKey, null);
            return _pcs.FirstOrDefault(p => p.Key == key) ?? _pcs.FirstOrDefault();
        }
    }

    public async Task LoadAsync()
    {
        if (_loaded) return;

        var json = await SecureStorage.GetAsync(PcsKey);
        if (!string.IsNullOrEmpty(json))
        {
            try { _pcs = JsonSerializer.Deserialize<List<SavedPc>>(json, JsonOpts) ?? []; }
            catch { _pcs = []; }
        }
        else
        {
            await MigrateFromV1Async();
        }

        _loaded = true;
    }

    /// <summary>
    /// v1.0 ținea un singur PC în două chei separate. Îl mutăm în listă,
    /// ca update-ul să nu ceară pairing din nou.
    /// </summary>
    private async Task MigrateFromV1Async()
    {
        var url = await SecureStorage.GetAsync("server_url");
        var token = await SecureStorage.GetAsync("auth_token");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token)) return;

        _pcs = [new SavedPc { BaseUrl = url, Token = token }];
        await SaveAsync();

        SecureStorage.Remove("server_url");
        SecureStorage.Remove("auth_token");
    }

    public Task SaveAsync()
        => SecureStorage.SetAsync(PcsKey, JsonSerializer.Serialize(_pcs, JsonOpts));

    public void SetActive(SavedPc pc) => Preferences.Set(ActiveKey, pc.Key);

    /// <summary>
    /// Același PC (după ID) sau aceeași adresă → actualizăm intrarea existentă (IP sau token nou).
    /// Altfel e un PC nou. Întoarce intrarea și dacă e nouă.
    /// </summary>
    public (SavedPc Pc, bool IsNew) Find(string? pcId, string baseUrl)
    {
        var existing = (pcId is not null ? _pcs.FirstOrDefault(p => p.PcId == pcId) : null)
                       ?? _pcs.FirstOrDefault(p => p.PcId is null &&
                              string.Equals(p.BaseUrl, baseUrl, StringComparison.OrdinalIgnoreCase));
        return existing is not null ? (existing, false) : (new SavedPc(), true);
    }

    public async Task AddAsync(SavedPc pc)
    {
        _pcs.Add(pc);
        await SaveAsync();
    }

    public async Task RemoveAsync(SavedPc pc)
    {
        _pcs.Remove(pc);
        if (Preferences.Get(ActiveKey, null) == pc.Key)
            Preferences.Remove(ActiveKey);
        await SaveAsync();
    }
}
