using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace KioskShell;

// Full-screen overlay with adaptive, touch-friendly buttons.
public sealed class MenuForm : Form
{
    private readonly KioskConfig _cfg;
    private readonly Label _title = new();
    private readonly Button _shutdown;
    private readonly Button _restart;
    private readonly Button _cancel;
    private readonly System.Windows.Forms.Timer _idle = new();
    private readonly System.Windows.Forms.Timer _tick = new();
    private bool _counting;
    private int _left;
    private string _verb = "";

    // Adaptive sizes based on screen
    private readonly float _scale;
    private readonly int _btnWidth;
    private readonly int _btnHeight;
    private readonly float _titleFontSize;
    private readonly float _btnFontSize;

    public MenuForm(KioskConfig cfg)
    {
        _cfg = cfg;

        // Calculate scale from screen height (reference: 1080p)
        var screen = Screen.PrimaryScreen!.Bounds;
        _scale = Math.Clamp(screen.Height / 1080f, 0.65f, 1.6f);

        _btnWidth  = (int)(Math.Min(screen.Width * 0.55f, 520) * _scale);
        _btnHeight = (int)(96 * _scale);
        _titleFontSize = 26f * _scale;
        _btnFontSize   = 20f * _scale;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(18, 20, 26);
        Opacity = 0.97;

        // Title
        _title.Text = "What would you like to do?";
        _title.AutoSize = true;
        _title.ForeColor = Color.FromArgb(240, 242, 245);
        _title.Font = new Font("Segoe UI Semibold", _titleFontSize, FontStyle.Regular);
        _title.Margin = new Padding(0, 0, 0, (int)(32 * _scale));
        _title.TextAlign = ContentAlignment.MiddleCenter;

        // Buttons
        _shutdown = MakeButton("Shut down", Color.FromArgb(190, 50, 50));
        _restart  = MakeButton("Restart",   Color.FromArgb(50, 110, 190));
        _cancel   = MakeButton("Cancel",    Color.FromArgb(55, 60, 72));

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        // Center the title properly
        _title.Width = _btnWidth;
        _title.TextAlign = ContentAlignment.MiddleCenter;

        panel.Controls.AddRange(new Control[] { _title, _shutdown, _restart, _cancel });

        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(panel, 0, 0);
        host.SetCellPosition(panel, new TableLayoutPanelCellPosition(0, 0));
        panel.Anchor = AnchorStyles.None;

        Controls.Add(host);

        // Events
        _shutdown.Click += (_, _) => StartCountdown("Shutting down", "/s /full");
        _restart.Click  += (_, _) => StartCountdown("Restarting", "/r");
        _cancel.Click   += (_, _) =>
        {
            if (_counting) RunShutdown("/a");
            Close();
        };

        // Auto-close timer
        _idle.Interval = Math.Max(5, cfg.MenuTimeoutSeconds) * 1000;
        _idle.Tick += (_, _) => { if (!_counting) Close(); };
        _idle.Start();

        // Countdown timer
        _tick.Interval = 1000;
        _tick.Tick += (_, _) =>
        {
            if (_left > 0) _left--;
            _title.Text = $"{_verb} in {_left}…";
            if (_left <= 0) _tick.Stop();
        };

        // Hover effects
        AddHoverEffect(_shutdown, Color.FromArgb(210, 60, 60), Color.FromArgb(190, 50, 50));
        AddHoverEffect(_restart,  Color.FromArgb(60, 130, 210), Color.FromArgb(50, 110, 190));
        AddHoverEffect(_cancel,   Color.FromArgb(70, 76, 90), Color.FromArgb(55, 60, 72));
    }

    private void StartCountdown(string verb, string args)
    {
        _idle.Stop();
        _counting = true;
        _verb = verb;
        _left = Math.Max(3, _cfg.ShutdownDelaySeconds);

        RunShutdown($"{args} /t {_left}");

        _shutdown.Visible = false;
        _restart.Visible = false;
        _cancel.Text = "Cancel";
        _title.Text = $"{_verb} in {_left}…";
        _tick.Start();
    }

    private static void RunShutdown(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("shutdown.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Log.Write("shutdown.exe failed: " + ex.Message);
        }
    }

    private Button MakeButton(string text, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(_btnWidth, _btnHeight),
            Font = new Font("Segoe UI Semibold", _btnFontSize, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, (int)(16 * _scale)),
            UseVisualStyleBackColor = false,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };

        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = bg;
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.12f);

        // Soft rounded corners via Region
        btn.Region = CreateRoundedRegion(btn.Width, btn.Height, (int)(14 * _scale));

        return btn;
    }

    private static void AddHoverEffect(Button btn, Color hover, Color normal)
    {
        btn.MouseEnter += (_, _) => btn.BackColor = hover;
        btn.MouseLeave += (_, _) => btn.BackColor = normal;
    }

    private static Region CreateRoundedRegion(int width, int height, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(width - d, 0, d, d, 270, 90);
        path.AddArc(width - d, height - d, d, d, 0, 90);
        path.AddArc(0, height - d, d, d, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }
}
