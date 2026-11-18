using System.Runtime.InteropServices;

namespace KioskShell2;

internal sealed class GestureWindow : Form
{
    private const int WM_POINTERDOWN = 0x0246, WM_POINTERUPDATE = 0x0245, WM_POINTERUP = 0x0247;
    private const int WM_LBUTTONDOWN = 0x0201, WM_MOUSEMOVE = 0x0200, WM_LBUTTONUP = 0x0202;
    private readonly System.Windows.Forms.Timer _reset = new() { Interval = 1600 };
    private KioskConfig _config;
    private Point _start;
    private DateTime _started;
    private bool _tracking;
    private bool _fired;
    public event EventHandler? GestureRecognized;

    public GestureWindow(KioskConfig config)
    {
        _config = config;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(0, 0, Screen.PrimaryScreen?.Bounds.Width ?? 1920, Math.Max(8, config.EdgeZonePx));
        TopMost = true;
        Opacity = 0.015; // nearly invisible input strip; deliberately intercepts only the top edge
        BackColor = Color.Black;
        _reset.Tick += (_, _) => { _tracking = false; _reset.Stop(); };
    }

    public void UpdateConfig(KioskConfig config)
    {
        _config = config;
        Bounds = new Rectangle(0, 0, Screen.PrimaryScreen?.Bounds.Width ?? 1920, Math.Max(8, config.EdgeZonePx));
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_POINTERDOWN:
            case WM_LBUTTONDOWN:
                Begin(GetPoint(m.LParam));
                break;
            case WM_POINTERUPDATE:
            case WM_MOUSEMOVE:
                if (_tracking) Move(GetPoint(m.LParam));
                break;
            case WM_POINTERUP:
            case WM_LBUTTONUP:
                _tracking = false;
                _reset.Stop();
                break;
        }
        base.WndProc(ref m);
    }

    private static Point GetPoint(IntPtr lp)
    {
        long value = lp.ToInt64();
        int x = unchecked((short)(value & 0xffff));
        int y = unchecked((short)((value >> 16) & 0xffff));
        return new Point(x, y);
    }

    private void Begin(Point point)
    {
        if (point.Y > _config.EdgeZonePx) return;
        _start = point;
        _started = DateTime.UtcNow;
        _tracking = true;
        _fired = false;
        _reset.Stop();
        _reset.Interval = Math.Max(500, _config.GestureMaxMilliseconds);
        _reset.Start();
    }

    private void Move(Point point)
    {
        if (!_tracking || _fired) return;
        if ((DateTime.UtcNow - _started).TotalMilliseconds > _config.GestureMaxMilliseconds) { _tracking = false; return; }
        int dy = point.Y - _start.Y;
        int dx = Math.Abs(point.X - _start.X);
        if (dy >= _config.DragDistancePx && dx < _config.DragDistancePx)
        {
            _fired = true;
            _tracking = false;
            _reset.Stop();
            GestureRecognized?.Invoke(this, EventArgs.Empty);
        }
    }
}