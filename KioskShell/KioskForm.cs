using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KioskShell;

public sealed class KioskForm : Form
{
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
            // The kiosk user can't write next to the exe, so keep browser data in their profile.
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KioskShell", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;
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

    private void Navigate()
    {
        try { _web.CoreWebView2.Navigate(_cfg.Url); }
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
        _web.CoreWebView2.NavigateToString(OfflineHtml);
        _retry.Stop();
        _retry.Start();
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

    private const string OfflineHtml = """
<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<body style="margin:0;height:100vh;display:grid;place-items:center;background:#111;color:#eee;font:28px 'Segoe UI',sans-serif">
<div style="text-align:center">Reconnecting…
<div style="font-size:18px;opacity:.6;margin-top:12px">Drag down from the top edge for the power menu</div></div>
</body>
""";
}
