using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KioskShell2;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\KioskShell2.SingleInstance", out var created);
        if (!created) return;
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => AppLog.Write("UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Write("Fatal: " + e.ExceptionObject);
        Application.Run(new KioskContext());
    }
}

internal sealed class KioskContext : ApplicationContext
{
    private readonly KioskConfig _config;
    private readonly GestureWindow _gesture;
    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 3000 };
    private static readonly HttpClient ProbeClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private Process? _edge;
    private bool _isOffline;
    private bool _probeRunning;
    private DateTime _lastProbeUtc = DateTime.MinValue;
    private KioskMenu? _menu;
    private bool _exiting;

    public KioskContext()
    {
        _config = KioskConfig.Load();
        _gesture = new GestureWindow(_config);
        _gesture.GestureRecognized += (_, _) => ShowMenu();
        _gesture.Show();
        LaunchEdge();
        _watchdog.Tick += async (_, _) => await CheckEdgeAsync();
        _watchdog.Start();
        AppLog.Write("KioskShell 2.0 helper started.");
    }

    private void LaunchEdge()
    {
        try
        {
            var edgePath = string.IsNullOrWhiteSpace(_config.EdgePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe")
                : _config.EdgePath;
            if (!File.Exists(edgePath))
                edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe");
            if (!File.Exists(edgePath)) throw new FileNotFoundException("Microsoft Edge was not found. Install Edge or set EdgePath in kiosk.v2.json.");

            var profile = Path.Combine(AppPaths.DataDir, "EdgeProfile");
            Directory.CreateDirectory(profile);
            var psi = new ProcessStartInfo(edgePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(edgePath)!
            };
            var launchUrl = _isOffline
                ? new Uri(Path.Combine(AppPaths.BaseDir, "offline.html")).AbsoluteUri
                : _config.Url;
            foreach (var arg in new[] { "--kiosk", launchUrl, "--edge-kiosk-type=fullscreen", "--no-first-run", "--disable-pinch", "--overscroll-history-navigation=0", "--user-data-dir=" + profile })
                psi.ArgumentList.Add(arg);
            _edge = Process.Start(psi);
            AppLog.Write("Started Edge kiosk for " + _config.Url);
        }
        catch (Exception ex) { AppLog.Write("Edge launch failed: " + ex); }
    }

    private async Task CheckEdgeAsync()
    {
        if (_exiting) return;
        if (!_probeRunning && (DateTime.UtcNow - _lastProbeUtc).TotalSeconds >= Math.Max(5, _config.OfflineRetrySeconds))
        {
            _probeRunning = true;
            _lastProbeUtc = DateTime.UtcNow;
            try
            {
                using var response = await ProbeClient.GetAsync(_config.Url, HttpCompletionOption.ResponseHeadersRead);
                SetOfflineState(false);
            }
            catch (Exception ex)
            {
                AppLog.Write("Website probe failed: " + ex.Message);
                SetOfflineState(true);
            }
            finally { _probeRunning = false; }
        }

        if (!_config.RestartEdgeOnExit) return;
        try
        {
            if (_edge is null || _edge.HasExited)
            {
                AppLog.Write("Edge process exited; relaunching kiosk.");
                _edge?.Dispose();
                _edge = null;
                LaunchEdge();
            }
        }
        catch (Exception ex) { AppLog.Write("Edge watchdog error: " + ex.Message); }
    }

    private void SetOfflineState(bool offline)
    {
        if (_isOffline == offline) return;
        _isOffline = offline;
        AppLog.Write(offline ? "Website is unreachable; switching Edge to local offline page." : "Website is reachable; restoring configured URL.");
        try { _edge?.Kill(entireProcessTree: true); } catch (Exception ex) { AppLog.Write("Could not switch Edge page: " + ex.Message); }
    }

    private void ShowMenu()
    {
        if (_menu is { IsDisposed: false }) { _menu.Activate(); return; }
        _menu = new KioskMenu(_config, SaveConfig, () => _exiting = true);
        _menu.FormClosed += (_, _) => _menu = null;
        _menu.Show();
        _menu.Activate();
    }

    private bool SaveConfig(KioskConfig next)
    {
        if (!next.IsValid(out var error))
        {
            MessageBox.Show(error, "Invalid settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!next.Save()) return false;
        _config.Url = next.Url;
        _config.EdgeZonePx = next.EdgeZonePx;
        _config.DragDistancePx = next.DragDistancePx;
        _config.MenuTimeoutSeconds = next.MenuTimeoutSeconds;
        _config.ShutdownDelaySeconds = next.ShutdownDelaySeconds;
        _gesture.UpdateConfig(_config);
        try { _edge?.Kill(); } catch { }
        LaunchEdge();
        return true;
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        _watchdog.Stop();
        _gesture.Close();
        base.ExitThreadCore();
    }
}

internal static class AppPaths
{
    public static string BaseDir => AppContext.BaseDirectory;
    public static string DataDir
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KioskShell2");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}

internal static class AppLog
{
    public static void Write(string message)
    {
        try { File.AppendAllText(Path.Combine(AppPaths.DataDir, "kioskshell2.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch { }
    }
}

internal sealed class KioskConfig
{
    public string Url { get; set; } = "https://example.com";
    public string EdgePath { get; set; } = "";
    public int EdgeZonePx { get; set; } = 28;
    public int DragDistancePx { get; set; } = 120;
    public int GestureMaxMilliseconds { get; set; } = 1500;
    public int MenuTimeoutSeconds { get; set; } = 20;
    public int ShutdownDelaySeconds { get; set; } = 10;
    public int OfflineRetrySeconds { get; set; } = 15;
    public bool RestartEdgeOnExit { get; set; } = true;

    public static KioskConfig Load()
    {
        try
        {
            var path = Path.Combine(AppPaths.BaseDir, "kiosk.v2.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<KioskConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex) { AppLog.Write("Config load failed: " + ex.Message); }
        return new();
    }

    public bool IsValid(out string error)
    {
        error = "";
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            error = "URL must be a valid http:// or https:// address.";
        else if (EdgeZonePx is < 8 or > 120 || DragDistancePx is < 40 or > 500)
            error = "Gesture edge zone or drag distance is outside the supported range.";
        else if (MenuTimeoutSeconds is < 5 or > 300 || ShutdownDelaySeconds is < 3 or > 120)
            error = "Menu timeout or power countdown is outside the supported range.";
        return error.Length == 0;
    }

    public bool Save()
    {
        try
        {
            File.WriteAllText(Path.Combine(AppPaths.BaseDir, "kiosk.v2.json"),
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
            return true;
        }
        catch (Exception ex) { AppLog.Write("Config save failed: " + ex); return false; }
    }
}