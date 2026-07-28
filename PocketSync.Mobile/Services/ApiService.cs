using System.Net.Http.Headers;
using System.Text.Json;

namespace PocketSync.Mobile.Services;

public record FileItem(Guid Id, string Name, long SizeBytes, DateTime ModifiedUtc);

/// <summary>
/// Clientul HTTP către server. HttpClient e singleton (socket exhaustion altfel),
/// timeout infinit la nivel de client — transferurile mari nu sunt "nu răspunde".
/// Tokenul merge în header X-Auth-Token (cookie-urile sunt pentru browsere).
/// </summary>
public sealed class ApiService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public string? BaseUrl { get; private set; }
    public string? Token { get; private set; }
    public bool IsPaired => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(Token);

    public async Task LoadAsync()
    {
        BaseUrl = await SecureStorage.GetAsync("server_url");
        Token = await SecureStorage.GetAsync("auth_token");
        ApplyToken();
    }

    /// <summary>Primește URL-ul de pairing din QR (http://ip:port/?token=...) și îl desface.</summary>
    public async Task<bool> PairAsync(string pairingUrl)
    {
        if (!Uri.TryCreate(pairingUrl.Trim(), UriKind.Absolute, out var uri)) return false;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var token = query["token"];
        if (string.IsNullOrEmpty(token)) return false;

        BaseUrl = $"{uri.Scheme}://{uri.Authority}";
        Token = token;
        ApplyToken();

        if (!await CheckStatusAsync()) { BaseUrl = null; Token = null; return false; }

        await SecureStorage.SetAsync("server_url", BaseUrl!);
        await SecureStorage.SetAsync("auth_token", Token!);
        return true;
    }

    private void ApplyToken()
    {
        _http.DefaultRequestHeaders.Remove("X-Auth-Token");
        if (!string.IsNullOrEmpty(Token))
            _http.DefaultRequestHeaders.Add("X-Auth-Token", Token);
    }

    public async Task<bool> CheckStatusAsync()
    {
        if (string.IsNullOrEmpty(BaseUrl)) return false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var r = await _http.GetAsync($"{BaseUrl}/api/status", cts.Token);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<FileItem>> ListFilesAsync()
    {
        var json = await _http.GetStringAsync($"{BaseUrl}/api/files");
        return JsonSerializer.Deserialize<List<FileItem>>(json, JsonOpts) ?? [];
    }

    /// <summary>Link de download deschis în browser — cu ?token= ca să treacă de auth fără cookie.</summary>
    public string DownloadUrl(Guid id) => $"{BaseUrl}/api/files/{id}/download?token={Token}";

    /// <summary>Comandă de putere (lock/sleep/restart/shutdown/abort). Întoarce mesajul serverului.</summary>
    public async Task<string> PowerAsync(string action)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await _http.PostAsync($"{BaseUrl}/api/power/{action}", null, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("message", out var m)
            ? m.GetString() ?? "OK" : "OK";
    }

    public async Task UploadAsync(Stream source, string fileName, long? length,
        IProgress<(long Sent, long Total)>? progress, CancellationToken ct = default)
    {
        // StreamContent, nu ByteArrayContent: un mp4 de 4 GB nu are ce căuta în RAM-ul telefonului
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new ProgressReadStream(source, length ?? -1, progress));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        var response = await _http.PostAsync($"{BaseUrl}/api/upload", content, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// HttpClient nu raportează progres la upload — un Stream care numără ce s-a citit o face.
    /// Un Stream e doar o interfață; poți pune orice în spatele ei.
    /// </summary>
    private sealed class ProgressReadStream(Stream inner, long total,
        IProgress<(long, long)>? progress) : Stream
    {
        private long _read;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => total >= 0 ? total : throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            _read += n;
            progress?.Report((_read, total));
            return n;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            var n = await inner.ReadAsync(buffer.AsMemory(offset, count), ct);
            _read += n;
            progress?.Report((_read, total));
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
