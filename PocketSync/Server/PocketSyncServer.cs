using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace PocketSync.Server;

/// <summary>
/// Serverul PocketSync, împachetat ca clasă ca să poată fi găzduit
/// și de consolă (dev) și de tray app (WPF) — același cod, două gazde.
/// </summary>
public sealed class PocketSyncServer
{
    private WebApplication? _app;
    private readonly ServerConfig _overrides;
    private readonly ConcurrentDictionary<Guid, string> _fileMap = new();

    public TokenStore Tokens { get; } = new();

    public int Port { get; private set; }
    public string BasePath { get; private set; } = "";
    public string ReceivedDir { get; private set; } = "";
    public string StorageDir { get; private set; } = "";
    public bool IsRunning => _app is not null;

    /// <summary>Se declanșează după fiecare fișier primit cu succes (nume, mărime în bytes).</summary>
    public event Action<string, long>? FileReceived;

    /// <summary>Se declanșează la cerere de comandă de putere (numele acțiunii) — pentru logging/toast.</summary>
    public event Action<string>? PowerCommandExecuted;

    public PocketSyncServer(ServerConfig? overrides = null)
    {
        _overrides = overrides ?? new ServerConfig();
    }

    public string? LanIp => NetworkHelper.GetBestLanAddress()?.ToString();

    public string LanUrl => $"http://{LanIp ?? "localhost"}:{Port}";

    /// <summary>URL-ul din QR: pagina + token. Query-ul e acceptat o dată, apoi mutat în cookie.</summary>
    public string PairingUrl => $"{LanUrl}/?token={Tokens.Token}";

    public byte[] PairingQrPng() => QrGenerator.GeneratePng(PairingUrl);

    public async Task StartAsync()
    {
        if (_app is not null) return;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });

        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null);

        // config: overrides din gazdă > appsettings.json > default (Desktop\PocketSync, 5000)
        Port = _overrides.Port
            ?? builder.Configuration.GetValue<int?>("PocketSync:Port")
            ?? 5000;

        var configFolder = _overrides.BasePath
            ?? builder.Configuration["PocketSync:StorageFolder"];
        BasePath = string.IsNullOrEmpty(configFolder) ? ServerConfig.DefaultBasePath() : configFolder;

        ReceivedDir = Path.Combine(BasePath, "Received");
        StorageDir = Path.Combine(BasePath, "Storage");
        Directory.CreateDirectory(ReceivedDir);
        Directory.CreateDirectory(StorageDir);

        builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");

        var app = builder.Build();

        ConfigureAuth(app);

        app.UseDefaultFiles();
        app.UseStaticFiles();

        MapEndpoints(app);

        await app.StartAsync();
        _app = app;
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    public Task WaitForShutdownAsync()
        => _app?.WaitForShutdownAsync() ?? Task.CompletedTask;

    // ---------------------------------------------------------------- auth

    private void ConfigureAuth(WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;

            // diagnostic public: vrei să vezi că serverul trăiește fără să fii autentificat
            if (path.StartsWithSegments("/api/status")) { await next(); return; }

            // 1. token în query — primul acces, din QR
            if (ctx.Request.Query.TryGetValue("token", out var queryToken) &&
                Tokens.IsValid(queryToken.FirstOrDefault()))
            {
                if (HttpMethods.IsGet(ctx.Request.Method) && !path.StartsWithSegments("/api"))
                {
                    // mută tokenul în cookie și scoate-l din URL (istoric, Referer, sync)
                    ctx.Response.Cookies.Append("ps_auth", Tokens.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromDays(365)
                    });
                    ctx.Response.Redirect(path.HasValue ? path.Value! : "/");
                    return;
                }
                await next(); return; // API cu ?token= — folosit de aplicația mobilă la download
            }

            // 2. header — aplicația MAUI, curl
            if (ctx.Request.Headers.TryGetValue("X-Auth-Token", out var headerToken) &&
                Tokens.IsValid(headerToken.FirstOrDefault()))
            { await next(); return; }

            // 3. cookie — browserul, după pairing
            if (ctx.Request.Cookies.TryGetValue("ps_auth", out var cookieToken) &&
                Tokens.IsValid(cookieToken))
            { await next(); return; }

            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (ctx.Request.Headers.Accept.ToString().Contains("text/html"))
            {
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync(
                    "<html><body style=\"font-family:sans-serif;text-align:center;padding-top:4rem\">" +
                    "<h2>PocketSync</h2><p>Acces neautorizat.</p>" +
                    "<p>Scanează codul QR de pe PC ca să te conectezi.</p></body></html>");
            }
        });
    }

    // ------------------------------------------------------------ endpoints

    private void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/status", () => new
        {
            ok = true,
            version = "1.0",
            name = Environment.MachineName
        });

        app.MapPost("/api/upload", async (HttpRequest request) =>
        {
            var mediaType = MediaTypeHeaderValue.Parse(request.ContentType!);
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            var reader = new MultipartReader(boundary!, request.Body);
            var section = await reader.ReadNextSectionAsync();
            var fileSection = section?.AsFileSection();

            if (fileSection is null)
                return Results.BadRequest("No file sent");

            // tower 1: aruncă orice cale, păstrează doar numele
            var fileName = Path.GetFileName(fileSection.FileName);

            // Android permite ? și : în nume; Windows nu
            foreach (var ch in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(ch, '_');

            var path = Path.Combine(ReceivedDir, fileName);

            // coliziuni: sufix cu ticks între nume și extensie
            if (File.Exists(path))
            {
                var extension = Path.GetExtension(fileName);
                fileName = Path.GetFileNameWithoutExtension(fileName)
                           + "_" + DateTime.Now.Ticks + extension;
                path = Path.Combine(ReceivedDir, fileName);
            }

            // tower 2: verifică path-ul REZOLVAT, nu textul
            path = Path.GetFullPath(path);
            if (!path.StartsWith(ReceivedDir, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest();

            long size;
            await using (var stream = File.Create(path))
            {
                await fileSection.FileStream!.CopyToAsync(stream);
                size = stream.Length;
            }

            FileReceived?.Invoke(fileName, size);
            return Results.Ok(new { name = fileName, size });
        }).DisableAntiforgery();

        app.MapGet("/api/files", () =>
        {
            _fileMap.Clear();
            var files = new DirectoryInfo(StorageDir).GetFiles();

            return files
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f =>
                {
                    var id = Guid.NewGuid();
                    _fileMap[id] = f.FullName;
                    return new FileItemDto(id, f.Name, f.Length, f.LastWriteTimeUtc);
                })
                .ToList();
        });

        app.MapGet("/api/files/{id}/download", (Guid id) =>
        {
            if (!_fileMap.TryGetValue(id, out var path) || !File.Exists(path))
                return Results.NotFound();

            return Results.File(path, "application/octet-stream", Path.GetFileName(path));
        });

        app.MapDelete("/api/files/{id}", (Guid id) =>
        {
            if (!_fileMap.TryGetValue(id, out var path) || !File.Exists(path))
                return Results.NotFound();

            // Recycle Bin, nu delete permanent — un tap greșit trebuie să fie recuperabil
            return RecycleBin.Delete(path)
                ? Results.Ok()
                : Results.Problem("Nu am putut șterge fișierul.");
        });

        // Comenzi de putere: whitelist strict (enum), NICIODATĂ string de la client spre shell.
        app.MapPost("/api/power/{action}", (string action) =>
        {
            if (!Enum.TryParse<PowerAction>(action, ignoreCase: true, out var parsed))
                return Results.BadRequest("Unknown action");

            var (exe, args) = parsed switch
            {
                PowerAction.Shutdown => ("shutdown", "/s /t 15"),
                PowerAction.Restart  => ("shutdown", "/r /t 15"),
                PowerAction.Abort    => ("shutdown", "/a"),
                PowerAction.Lock     => ("rundll32.exe", "user32.dll,LockWorkStation"),
                PowerAction.Sleep    => ("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"),
                _ => throw new UnreachableException()
            };

            try
            {
                Process.Start(new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false });
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }

            PowerCommandExecuted?.Invoke(parsed.ToString());

            var message = parsed switch
            {
                PowerAction.Shutdown => "PC-ul se închide în 15 secunde. Poți anula.",
                PowerAction.Restart  => "PC-ul repornește în 15 secunde. Poți anula.",
                PowerAction.Abort    => "Anulat.",
                PowerAction.Lock     => "PC blocat.",
                PowerAction.Sleep    => "PC-ul intră în sleep.",
                _ => "OK"
            };
            return Results.Ok(new { message });
        });
    }

    private enum PowerAction { Shutdown, Restart, Sleep, Lock, Abort }
}

public record FileItemDto(Guid Id, string Name, long SizeBytes, DateTime ModifiedUtc);
