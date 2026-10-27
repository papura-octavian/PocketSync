using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PocketSync.TrayApp;

public record RecentItem(string Name, string Ext, string Size, string Time);

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly ObservableCollection<RecentItem> _recent = [];
    private readonly DispatcherTimer _copyReset = new() { Interval = TimeSpan.FromSeconds(2) };

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();

        var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Icon.ico");
        if (System.IO.File.Exists(icoPath))
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icoPath));

        RecentList.ItemsSource = _recent;
        _copyReset.Tick += (_, _) => { _copyReset.Stop(); SetCopyState(copied: false); };

        // bara de titlu urmează tema Windows, inclusiv dacă se schimbă cât e fereastra deschisă
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        ThemeManager.Changed += OnThemeChanged;

        _app.Server.FileReceived += OnFileReceived;
        RefreshInfo();
        AutoStartCheck.IsChecked = AutoStart.IsEnabled();
    }

    private void OnThemeChanged() => ThemeManager.ApplyTitleBar(this);

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.Changed -= OnThemeChanged;
        base.OnClosed(e);
    }

    private void RefreshInfo()
    {
        var server = _app.Server;
        var running = server.IsRunning;

        StatusPill.SetResourceReference(BackgroundProperty, running ? "OkSoftBrush" : "DangerSoftBrush");
        StatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, running ? "OkBrush" : "DangerBrush");
        StatusText.SetResourceReference(ForegroundProperty, running ? "OkBrush" : "DangerBrush");
        StatusText.Text = running ? "Activ" : "Oprit";
        StatusAddress.Text = running ? $"{server.LanIp ?? "localhost"}:{server.Port}" : "";
        StatusPill.ToolTip = running
            ? $"Serverul ascultă pe portul {server.Port}. Portul se schimbă din %APPDATA%\\PocketSync\\settings.json (cere regulă nouă de firewall)."
            : "Serverul nu a pornit — verifică dacă alt program folosește portul.";

        UrlText.Text = server.PairingUrl;
        FolderText.Text = server.BasePath;
        FolderText.ToolTip = server.BasePath;

        if (running)
        {
            var png = server.PairingQrPng();
            var image = new BitmapImage();
            using var ms = new MemoryStream(png);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            QrImage.Source = image;
        }
    }

    private void OnFileReceived(string name, long size)
        => Dispatcher.Invoke(() =>
        {
            var ext = System.IO.Path.GetExtension(name).TrimStart('.');
            ext = ext.Length is > 0 and <= 4 ? ext.ToUpperInvariant() : "FIȘ";

            _recent.Insert(0, new RecentItem(name, ext, App.FormatSize(size), DateTime.Now.ToString("HH:mm")));
            while (_recent.Count > 30)
                _recent.RemoveAt(_recent.Count - 1);
            RecentEmpty.Visibility = Visibility.Collapsed;
        });

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_app.Server.PairingUrl);
        SetCopyState(copied: true);
        _copyReset.Stop();
        _copyReset.Start();
    }

    private void SetCopyState(bool copied)
    {
        CopyIcon.Text = copied ? "" : ""; // bifă / copiere
        CopyText.Text = copied ? "Copiat" : "Copiază";
    }

    private void OpenReceived_Click(object sender, RoutedEventArgs e)
        => Process.Start("explorer.exe", _app.Server.ReceivedDir);

    private void OpenStorage_Click(object sender, RoutedEventArgs e)
        => Process.Start("explorer.exe", _app.Server.StorageDir);

    private void OpenPage_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo($"http://localhost:{_app.Server.Port}")
        { UseShellExecute = true });

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Alege folderul PocketSync (va conține Received și Storage)",
            UseDescriptionForTitle = true,
            SelectedPath = _app.Server.BasePath
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        _app.Settings.StorageFolder = dialog.SelectedPath;
        _app.Settings.Save();
        await _app.RestartServerAsync();
        _app.Server.FileReceived += OnFileReceived; // serverul e obiect nou
        RefreshInfo();
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (AutoStartCheck.IsChecked == true) AutoStart.Enable();
        else AutoStart.Disable();
    }

    private void RegenerateToken_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Codul nou deconectează toate telefoanele legate de acest PC. Pe fiecare telefon va trebui să scanezi QR-ul din nou (PC-ul rămâne în listă, doar se actualizează). Continui?",
            "PocketSync", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _app.Server.Tokens.Regenerate();
        RefreshInfo();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_app.IsExiting) return; // Ieșire reală din meniul de tray

        // X = ascunde, nu omoară — serverul merge mai departe din tray
        e.Cancel = true;
        Hide();
    }
}
