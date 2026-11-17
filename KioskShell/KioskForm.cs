using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KioskShell;

public sealed class KioskForm : Form
{
    private const string AssetHost = "assets.kioskshell.local";

    private readonly KioskConfig _cfg;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _retry = new();
    private MenuForm? _menu;

    public KioskForm(KioskConfig cfg)
    {
        _cfg = cfg;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Controls.Add(_web);

        _retry.Interval = Math.Max(3, cfg.OfflineRetrySeconds) * 1000;
        _retry.Tick += (_, _) => { _retry.Stop(); Navigate(); };

        Load += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            // Optional fully-portable browser: drop a Fixed Version WebView2 runtime into .\WebView2Runtime
            // (the folder that contains msedgewebview2.exe). Otherwise the Windows 11 built-in runtime is used.
            var runtimeDir = Path.Combine(AppPaths.BaseDir, "WebView2Runtime");
            string? browserFolder = Directory.Exists(runtimeDir) ? runtimeDir : null;

            var env = await CoreWebView2Environment.CreateAsync(
                browserFolder, Path.Combine(AppPaths.DataDir, "WebView2"));
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;

            // Serve .\assets as https://assets.kioskshell.local/ so local pages behave like normal web pages.
            if (Directory.Exists(AppPaths.AssetsDir))
                core.SetVirtualHostNameToFolderMapping(
                    AssetHost, AppPaths.AssetsDir, CoreWebView2HostResourceAccessKind.Allow);

            var s = core.Settings;
            s.AreDevToolsEnabled = false;
            s.AreDefaultContextMenusEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.IsPinchZoomEnabled = false;
            s.IsSwipeNavigationEnabled = false;
            s.IsPasswordAutosaveEnabled = false;
            s.IsGeneralAutofillEnabled = false;
            s.IsBuiltInErrorPageEnabled = false;

            // Keep everything inside this one window.
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (!string.IsNullOrEmpty(e.Uri)) core.Navigate(e.Uri);
            };
            core.WebMessageReceived += OnWebMessage;
            core.NavigationCompleted += OnNavigationCompleted;

            var js = GestureJs
                .Replace("__EDGE__", _cfg.EdgeZonePx.ToString())
                .Replace("__DIST__", _cfg.DragDistancePx.ToString());
            await core.AddScriptToExecuteOnDocumentCreatedAsync(js);

            Navigate();
        }
        catch (Exception ex)
        {
            Log.Write("WebView2 init failed: " + ex);
        }
    }

    // "https://..." and "file:///..." are used as-is. Anything else is a page inside .\assets.
    private string ResolveUrl()
    {
        var u = (_cfg.Url ?? "").Trim();
        if (u.Length == 0) return "about:blank";
        if (Uri.TryCreate(u, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeFile))
            return u;

        u = u.Replace('\\', '/').TrimStart('/');
        if (u.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) u = u.Substring(7);
        return $"https://{AssetHost}/{u}";
    }

    private void Navigate()
    {
        try { _web.CoreWebView2.Navigate(ResolveUrl()); }
        catch (Exception ex) { Log.Write("Navigate failed: " + ex.Message); ShowOffline(); }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
        {
            Log.Write("Navigation failed: " + e.WebErrorStatus);
            ShowOffline();
        }
    }

    private void ShowOffline()
    {
        _web.CoreWebView2.NavigateToString(LoadOfflineHtml());
        _retry.Stop();
        _retry.Start();
    }

    // Editable: .\assets\offline.html. Falls back to a built-in page if the file is missing.
    private static string LoadOfflineHtml()
    {
        try
        {
            var p = Path.Combine(AppPaths.AssetsDir, "offline.html");
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        catch { }
        return FallbackOfflineHtml;
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try { msg = e.TryGetWebMessageAsString(); } catch { return; }
        if (msg == "kiosk-menu") ShowMenu();
    }

    private void ShowMenu()
    {
        if (_menu is { IsDisposed: false }) return;
        _menu = new MenuForm(_cfg);
        _menu.FormClosed += (_, _) => { _menu = null; Activate(); };
        _menu.Show(this);
    }

    // Injected into every page. Detects: touch starts within EDGE px of the top, then drags down DIST px.
    // Uses touch events so it can stop the page from scrolling while the gesture is in progress,
    // plus mouse events so you can test it on a desktop.
    private const string GestureJs = """
(() => {
  if (window.top !== window) return;
  const EDGE = __EDGE__, DIST = __DIST__, MAX_MS = 1500;
  let start = null, fired = false;

  const post = () => { try { window.chrome.webview.postMessage('kiosk-menu'); } catch (e) {} };
  const begin = (x, y) => { start = (y <= EDGE) ? { x, y, t: Date.now() } : null; fired = false; };
  const move = (x, y, ev) => {
    if (!start || fired) return;
    if (Date.now() - start.t > MAX_MS) { start = null; return; }
    if (ev && ev.cancelable) ev.preventDefault();
    if (y - start.y >= DIST && Math.abs(x - start.x) < DIST) { fired = true; start = null; post(); }
  };
  const end = () => { start = null; };

  addEventListener('touchstart', e => {
    if (e.touches.length === 1) begin(e.touches[0].clientX, e.touches[0].clientY); else start = null;
  }, { capture: true, passive: true });
  addEventListener('touchmove', e => {
    if (e.touches.length === 1) move(e.touches[0].clientX, e.touches[0].clientY, e);
  }, { capture: true, passive: false });
  addEventListener('touchend', end, true);
  addEventListener('touchcancel', end, true);

  addEventListener('mousedown', e => begin(e.clientX, e.clientY), true);
  addEventListener('mousemove', e => { if (e.buttons === 1) move(e.clientX, e.clientY, null); }, true);
  addEventListener('mouseup', end, true);
})();
""";

    private const string FallbackOfflineHtml = """
<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<body style="margin:0;height:100vh;display:grid;place-items:center;background:#111;color:#eee;font:28px 'Segoe UI',sans-serif">
<div style="text-align:center">Reconnecting…</div>
</body>
""";
}
