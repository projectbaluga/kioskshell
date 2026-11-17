using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace KioskShell;

// Full-screen overlay with adaptive, touch-friendly buttons and Fluent icons.
public sealed class MenuForm : Form
{
    private readonly KioskConfig _cfg;
    private readonly Label _title = new();
    private readonly IconButton _shutdown;
    private readonly IconButton _restart;
    private readonly IconButton _cancel;
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
    private readonly float _iconFontSize;

    // Segoe Fluent Icons glyphs (Windows 11)
    private const string IconPower   = "\uE7E8"; // Power
    private const string IconRestart = "\uE72C"; // Refresh / Restart
    private const string IconCancel  = "\uE711"; // ChromeClose

    public MenuForm(KioskConfig cfg)
    {
        _cfg = cfg;

        // Calculate scale from screen height (reference: 1080p)
        var screen = Screen.PrimaryScreen!.Bounds;
        _scale = Math.Clamp(screen.Height / 1080f, 0.65f, 1.6f);

        _btnWidth      = (int)(Math.Min(screen.Width * 0.58f, 560) * _scale);
        _btnHeight     = (int)(108 * _scale);
        _titleFontSize = 26f * _scale;
        _btnFontSize   = 20f * _scale;
        _iconFontSize  = 26f * _scale;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = screen;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(16, 18, 24);
        Opacity = 0.97;

        // Soft dark gradient background
        Paint += (_, e) =>
        {
            using var brush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(22, 24, 32),
                Color.FromArgb(14, 16, 22),
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        };

        // Title
        _title.Text = "What would you like to do?";
        _title.AutoSize = true;
        _title.ForeColor = Color.FromArgb(235, 238, 245);
        _title.Font = new Font("Segoe UI Semibold", _titleFontSize, FontStyle.Regular);
        _title.Margin = new Padding(0, 0, 0, (int)(36 * _scale));
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.Width = _btnWidth;

        // Buttons with icons
        _shutdown = MakeIconButton(IconPower,   "Shut down", Color.FromArgb(200, 55, 55));
        _restart  = MakeIconButton(IconRestart, "Restart",   Color.FromArgb(45, 115, 200));
        _cancel   = MakeIconButton(IconCancel,  "Cancel",    Color.FromArgb(52, 58, 72));

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
        AddHoverEffect(_shutdown, Color.FromArgb(220, 70, 70), Color.FromArgb(200, 55, 55));
        AddHoverEffect(_restart,  Color.FromArgb(60, 135, 220), Color.FromArgb(45, 115, 200));
        AddHoverEffect(_cancel,   Color.FromArgb(68, 74, 90), Color.FromArgb(52, 58, 72));
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

    private IconButton MakeIconButton(string icon, string text, Color bg)
    {
        var btn = new IconButton
        {
            IconGlyph = icon,
            LabelText = text,
            Size = new Size(_btnWidth, _btnHeight),
            BackColor = bg,
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, (int)(18 * _scale)),
            Cursor = Cursors.Hand,
            IconFontSize = _iconFontSize,
            LabelFontSize = _btnFontSize,
            ScaleFactor = _scale
        };

        btn.Region = CreateRoundedRegion(btn.Width, btn.Height, (int)(16 * _scale));
        return btn;
    }

    private static void AddHoverEffect(IconButton btn, Color hover, Color normal)
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

    // Custom button that draws Fluent icon + text cleanly
    private sealed class IconButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string IconGlyph { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LabelText { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float IconFontSize { get; set; } = 24f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float LabelFontSize { get; set; } = 18f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float ScaleFactor { get; set; } = 1f;

        public IconButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Text = ""; // we paint everything ourselves
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var bg = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(bg, ClientRectangle);

            using var iconFont = new Font("Segoe Fluent Icons", IconFontSize);
            using var textFont = new Font("Segoe UI Semibold", LabelFontSize);

            var iconSize = e.Graphics.MeasureString(IconGlyph, iconFont);
            var textSize = e.Graphics.MeasureString(LabelText, textFont);

            float gap = 14f * ScaleFactor;
            float totalWidth = iconSize.Width + gap + textSize.Width;
            float startX = (ClientRectangle.Width - totalWidth) / 2f;
            float centerY = ClientRectangle.Height / 2f;

            using var brush = new SolidBrush(ForeColor);

            e.Graphics.DrawString(IconGlyph, iconFont, brush,
                startX,
                centerY - iconSize.Height / 2f);

            e.Graphics.DrawString(LabelText, textFont, brush,
                startX + iconSize.Width + gap,
                centerY - textSize.Height / 2f);
        }
    }
}
