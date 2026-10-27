using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PocketSync.Mobile.Services;

public record FileItem(Guid Id, string Name, long SizeBytes, DateTime ModifiedUtc);

public record PcInfo(string? Id, string Name, string? Version);

public enum PcState { Online, Offline, Unauthorized }

/// <summary>Tokenul nu mai e valid — s-a regenerat pe PC. Trebuie scanat QR-ul din nou.</summary>
public sealed class UnauthorizedPcException() : Exception("Tokenul nu mai e valid.");

/// <summary>
/// Clientul HTTP către PC-uri. HttpClient e singleton (socket exhaustion altfel),
/// timeout infinit la nivel de client — transferurile mari nu sunt "nu răspunde".
/// Tokenul merge în header X-Auth-Token, pus pe fiecare request: fiecare PC are tokenul lui,
/// deci nu mai poate sta în DefaultRequestHeaders.
/// </summary>
public sealed class ApiService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>Desface URL-ul de pairing din QR (http://ip:port/?token=...).</summary>
    public static bool TryParsePairingUrl(string text, out string baseUrl, out string token)
    {
        baseUrl = token = "";
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        token = query["token"] ?? "";
        if (string.IsNullOrEmpty(token)) return false;

        baseUrl = $"{uri.Scheme}://{uri.Authority}";
        return true;
    }

    private static HttpRequestMessage Request(HttpMethod method, string baseUrl, string token, string path)
    {
        var req = new HttpRequestMessage(method, baseUrl + path);
        req.Headers.Add("X-Auth-Token", token);
        return req;
    }

    private Task<HttpResponseMessage> SendAsync(SavedPc pc, HttpMethod method, string path,
        CancellationToken ct = default, HttpContent? content = null)
    {
        var req = Request(method, pc.BaseUrl, pc.Token, path);
        req.Content = content;
        return _http.SendAsync(req, ct);
    }

    /// <summary>
    /// Verifică autentificat cine e PC-ul. /api/status e public — ar spune "ok" și cu token greșit,
    /// de aceea la pairing folosim /api/device. Aruncă UnauthorizedPcException la token greșit.
    /// </summary>
    public async Task<PcInfo> GetDeviceAsync(string baseUrl, string token, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(6));

        using var r = await _http.SendAsync(Request(HttpMethod.Get, baseUrl, token, "/api/device"), cts.Token);
        if (r.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedPcException();

        if (r.StatusCode == HttpStatusCode.NotFound)
        {
            // server 1.0: nu are /api/device. Tokenul îl verificăm pe /api/files, numele vine din status.
            using var files = await _http.SendAsync(Request(HttpMethod.Get, baseUrl, token, "/api/files"), cts.Token);
            if (files.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedPcException();
            files.EnsureSuccessStatusCode();

            var status = await _http.GetStringAsync(baseUrl + "/api/status", cts.Token);
            var legacy = JsonSerializer.Deserialize<PcInfo>(status, JsonOpts);
            return new PcInfo(null, legacy?.Name ?? "PC", legacy?.Version);
        }

        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize<PcInfo>(json, JsonOpts) ?? new PcInfo(null, "PC", null);
    }

    /// <summary>Online / offline / token expirat — pentru bulina de stare.</summary>
    public async Task<(PcState State, PcInfo? Info)> CheckAsync(SavedPc pc)
    {
        try
        {
            var info = await GetDeviceAsync(pc.BaseUrl, pc.Token, TimeSpan.FromSeconds(4));
            return (PcState.Online, info);
        }
        catch (UnauthorizedPcException) { return (PcState.Unauthorized, null); }
        catch { return (PcState.Offline, null); }
    }

    public async Task<List<FileItem>> ListFilesAsync(SavedPc pc)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var r = await SendAsync(pc, HttpMethod.Get, "/api/files", cts.Token);
        if (r.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedPcException();
        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadAsStringAsync(cts.Token);
        return JsonSerializer.Deserialize<List<FileItem>>(json, JsonOpts) ?? [];
    }

    /// <summary>Link de download deschis în browser — cu ?token= ca să treacă de auth fără cookie.</summary>
    public static string DownloadUrl(SavedPc pc, Guid id)
        => $"{pc.BaseUrl}/api/files/{id}/download?token={Uri.EscapeDataString(pc.Token)}";

    /// <summary>Șterge de pe PC — ajunge în Recycle Bin, nu dispare definitiv.</summary>
    public async Task DeleteFileAsync(SavedPc pc, Guid id)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var r = await SendAsync(pc, HttpMethod.Delete, $"/api/files/{id}", cts.Token);
        r.EnsureSuccessStatusCode();
    }

    /// <summary>Comandă de putere (lock/sleep/restart/shutdown/abort). Întoarce mesajul serverului.</summary>
    public async Task<string> PowerAsync(SavedPc pc, string action)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await SendAsync(pc, HttpMethod.Post, $"/api/power/{action}", cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("message", out var m)
            ? m.GetString() ?? "OK" : "OK";
    }

    public async Task UploadAsync(SavedPc pc, Stream source, string fileName, long? length,
        IProgress<(long Sent, long Total)>? progress, CancellationToken ct = default)
    {
        // StreamContent, nu ByteArrayContent: un mp4 de 4 GB nu are ce căuta în RAM-ul telefonului
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(new ProgressReadStream(source, length ?? -1, progress));
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        using var response = await SendAsync(pc, HttpMethod.Post, "/api/upload", ct, content);
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
