using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PocketSync.TrayApp;

public partial class MainWindow : Window
{
    private readonly App _app;

    public MainWindow(App app)
    {
        _app = app;
        InitializeComponent();

        _app.Server.FileReceived += OnFileReceived;
        RefreshInfo();
        AutoStartCheck.IsChecked = AutoStart.IsEnabled();
    }

    private void RefreshInfo()
    {
        var server = _app.Server;
        var running = server.IsRunning;

        StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
            running ? System.Windows.Media.Color.FromRgb(0x16, 0xa3, 0x4a)
                    : System.Windows.Media.Color.FromRgb(0xdc, 0x26, 0x26));
        StatusText.Text = running ? $"Activ pe {server.LanUrl}" : "Oprit";
        UrlText.Text = server.PairingUrl;
        FolderText.Text = server.BasePath;
        PortText.Text = $"{server.Port}  (se schimbă din %APPDATA%\\PocketSync\\settings.json — cere regulă nouă de firewall)";

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
            RecentList.Items.Insert(0, $"{DateTime.Now:HH:mm}  {name}  ({App.FormatSize(size)})");
            while (RecentList.Items.Count > 20)
                RecentList.Items.RemoveAt(RecentList.Items.Count - 1);
        });

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(_app.Server.PairingUrl);
        CopyLinkButton.Content = "Copiat!";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
        => Process.Start("explorer.exe", _app.Server.BasePath);

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
            "Tokenul nou deconectează toate device-urile pairate. Va trebui să scanezi QR-ul din nou. Continui?",
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
