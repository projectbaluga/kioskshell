using System.Diagnostics;

namespace KioskShell;

// Full-screen overlay with big touch buttons. Separate window, so it always draws above the web view.
public sealed class MenuForm : Form
{
    private readonly KioskConfig _cfg;
    private readonly Label _title = new();
    private readonly Button _shutdown = MakeButton("Shut down");
    private readonly Button _restart = MakeButton("Restart");
    private readonly Button _cancel = MakeButton("Cancel");
    private readonly System.Windows.Forms.Timer _idle = new();
    private readonly System.Windows.Forms.Timer _tick = new();
    private bool _counting;
    private int _left;
    private string _verb = "";

    public MenuForm(KioskConfig cfg)
    {
        _cfg = cfg;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(20, 24, 30);
        Opacity = 0.97;

        _title.Text = "What would you like to do?";
        _title.AutoSize = true;
        _title.ForeColor = Color.White;
        _title.Font = new Font("Segoe UI", 28f, FontStyle.Regular);
        _title.Margin = new Padding(0, 0, 0, 28);

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            BackColor = Color.Transparent
        };
        panel.Controls.AddRange(new Control[] { _title, _shutdown, _restart, _cancel });

        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        host.Controls.Add(panel, 0, 0);
        Controls.Add(host);

        _shutdown.Click += (_, _) => StartCountdown("Shutting down", "/s /full");
        _restart.Click += (_, _) => StartCountdown("Restarting", "/r");
        _cancel.Click += (_, _) =>
        {
            if (_counting) RunShutdown("/a");   // abort the pending shutdown/restart
            Close();
        };

        _idle.Interval = Math.Max(5, cfg.MenuTimeoutSeconds) * 1000;
        _idle.Tick += (_, _) => { if (!_counting) Close(); };
        _idle.Start();

        _tick.Interval = 1000;
        _tick.Tick += (_, _) =>
        {
            if (_left > 0) _left--;
            _title.Text = $"{_verb} in {_left}…";
            if (_left <= 0) _tick.Stop();
        };
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
        catch (Exception ex) { Log.Write("shutdown.exe failed: " + ex.Message); }
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        Size = new Size(460, 100),
        Font = new Font("Segoe UI", 22f, FontStyle.Regular),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(44, 52, 64),
        ForeColor = Color.White,
        Margin = new Padding(0, 0, 0, 18),
        UseVisualStyleBackColor = false
    };
}
