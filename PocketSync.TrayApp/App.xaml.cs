using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using PocketSync.Server;
using Serilog;

namespace PocketSync.TrayApp;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _window;

    public PocketSyncServer Server { get; private set; } = null!;
    public TrayAppSettings Settings { get; private set; } = null!;
    public bool IsExiting { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PocketSync", "logs");
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(logDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        ThemeManager.Initialize();
        Settings = TrayAppSettings.Load();
        Server = new PocketSyncServer(new ServerConfig
        {
            BasePath = Settings.StorageFolder,
            Port = Settings.Port
        });

        Server.FileReceived += (name, size) =>
        {
            Log.Information("Fișier primit: {Name} ({Size} bytes)", name, size);
            ShowToast("Fișier primit", $"{name} ({FormatSize(size)})");
        };
        Server.PowerCommandExecuted += action =>
            Log.Information("Comandă de putere: {Action}", action);

        try
        {
            await Server.StartAsync();
            Log.Information("Server pornit pe portul {Port}, folder {Folder}", Server.Port, Server.BasePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Serverul nu a putut porni");
            System.Windows.MessageBox.Show(
                "Serverul nu a putut porni: " + ex.Message +
                "\n\nVerifică dacă alt program folosește portul " + Settings.Port + ".",
                "PocketSync", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        CreateTrayIcon();

        // pornit cu Windows → stă în tray; pornit manual → arată fereastra
        if (!e.Args.Contains("--minimized"))
            ShowWindow();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "PocketSync",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Deschide PocketSync", null, (_, _) => ShowWindow());
        menu.Items.Add("Deschide pagina în browser", null, (_, _) =>
            Process.Start(new ProcessStartInfo($"http://localhost:{Server.Port}") { UseShellExecute = true }));
        menu.Items.Add("Deschide folderul", null, (_, _) =>
            Process.Start("explorer.exe", Server.BasePath));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Ieșire", null, async (_, _) =>
        {
            IsExiting = true;
            _trayIcon!.Visible = false;
            await Server.StopAsync();
            Log.Information("Server oprit, aplicația se închide");
            Log.CloseAndFlush();
            Shutdown();
        });
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        // click pe balon → deschide folderul cu fișierele primite
        _trayIcon.BalloonTipClicked += (_, _) =>
            Process.Start("explorer.exe", Server.ReceivedDir);
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow(this);
            _window.Closed += (_, _) => _window = null;
        }
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void ShowToast(string title, string message)
        => _trayIcon?.ShowBalloonTip(4000, title, message, System.Windows.Forms.ToolTipIcon.Info);

    /// <summary>Restart cu config nou (schimbare folder). Portul rămâne fix — regula de firewall e legată de el.</summary>
    public async Task RestartServerAsync()
    {
        await Server.StopAsync();
        Server = new PocketSyncServer(new ServerConfig
        {
            BasePath = Settings.StorageFolder,
            Port = Settings.Port
        });
        await Server.StartAsync();
        Log.Information("Server repornit: folder {Folder}", Server.BasePath);
    }

    private static Icon CreateIcon()
    {
        // iconul aplicației, copiat lângă exe; fallback pe unul desenat dacă lipsește
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Icon.ico");
        if (File.Exists(icoPath))
        {
            try { return new Icon(icoPath); }
            catch { /* fișier corupt — cădem pe fallback */ }
        }

        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
        g.FillEllipse(brush, 1, 1, 30, 30);
        using var font = new Font("Segoe UI", 15, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var text = "P";
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, Brushes.White, (32 - size.Width) / 2, (32 - size.Height) / 2 + 1);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}
