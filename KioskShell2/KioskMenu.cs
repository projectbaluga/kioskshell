using System.Diagnostics;

namespace KioskShell2;

internal sealed class KioskMenu : Form
{
    private readonly KioskConfig _config;
    private readonly Func<KioskConfig, bool> _save;
    private readonly Action _beginExit;
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _idle = new() { Interval = 20000 };
    private readonly System.Windows.Forms.Timer _countdown = new() { Interval = 1000 };
    private int _remaining;
    private bool _powerPending;

    public KioskMenu(KioskConfig config, Func<KioskConfig, bool> save, Action beginExit)
    {
        _config = config; _save = save; _beginExit = beginExit;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 720);
        TopMost = true; ShowInTaskbar = false;
        BackColor = Color.FromArgb(20, 24, 32);
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Color.Transparent };
        var title = new Label { Text = "KioskShell", ForeColor = Color.White, Font = new Font("Segoe UI", 25, FontStyle.Bold), AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 0, 24) };
        panel.Controls.Add(title);
        panel.Controls.Add(MakeButton("Settings", (_, _) => OpenSettings(), Color.FromArgb(55, 80, 115)));
        panel.Controls.Add(MakeButton("Shut down", (_, _) => StartPower("shutdown"), Color.FromArgb(160, 50, 50)));
        panel.Controls.Add(MakeButton("Restart", (_, _) => StartPower("restart"), Color.FromArgb(40, 105, 165)));
        panel.Controls.Add(MakeButton("Cancel power action", (_, _) => CancelPower(), Color.FromArgb(65, 70, 82)));
        _status.ForeColor = Color.White; _status.Font = new Font("Segoe UI", 16); _status.AutoSize = true; panel.Controls.Add(_status);
        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = BackColor };
        host.Controls.Add(panel, 0, 0); panel.Anchor = AnchorStyles.None; Controls.Add(host);
        _idle.Interval = Math.Clamp(config.MenuTimeoutSeconds, 5, 300) * 1000;
        _idle.Tick += (_, _) => { if (!_powerPending) Close(); };
        _idle.Start();
        _countdown.Tick += (_, _) => TickCountdown();
    }

    private Button MakeButton(string text, EventHandler click, Color color)
    {
        var b = new Button { Text = text, Width = 420, Height = 76, Font = new Font("Segoe UI", 18, FontStyle.Bold), BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 0, 12), UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderSize = 0; b.Click += click; return b;
    }

    private void OpenSettings()
    {
        _idle.Stop();
        using var dialog = new SettingsDialog(_config);
        if (dialog.ShowDialog(this) == DialogResult.OK && _save(dialog.Value))
            Close();
        else _idle.Start();
    }

    private void StartPower(string action)
    {
        if (_powerPending) return;
        _powerPending = true; _idle.Stop();
        _remaining = Math.Clamp(_config.ShutdownDelaySeconds, 3, 120);
        _status.Text = $"{(action == "shutdown" ? "Shutting down" : "Restarting")} in {_remaining} seconds. Press Cancel to stop.";
        _powerAction = action;
        _countdown.Start();
    }
    private string _powerAction = "";

    private void TickCountdown()
    {
        _remaining--;
        if (_remaining > 0) { _status.Text = $"{(_powerAction == "shutdown" ? "Shutting down" : "Restarting")} in {_remaining} seconds. Press Cancel to stop."; return; }
        _countdown.Stop();
        try
        {
            var args = _powerAction == "shutdown" ? "/s /t 0" : "/r /t 0";
            Process.Start(new ProcessStartInfo("shutdown.exe", args) { UseShellExecute = false, CreateNoWindow = true });
            _beginExit();
            AppLog.Write("Power action requested: " + _powerAction);
        }
        catch (Exception ex) { AppLog.Write("Power action failed: " + ex); _status.Text = "Power action failed. Please contact an administrator."; _powerPending = false; }
    }

    private void CancelPower()
    {
        if (!_powerPending) { Close(); return; }
        _countdown.Stop(); _powerPending = false; _remaining = 0; _status.Text = "Power action cancelled."; _idle.Start();
        AppLog.Write("Power action cancelled before execution.");
    }
}

internal sealed class SettingsDialog : Form
{
    private readonly TextBox _url = new();
    private readonly NumericUpDown _edge = new() { Minimum = 8, Maximum = 120 };
    private readonly NumericUpDown _drag = new() { Minimum = 40, Maximum = 500 };
    public KioskConfig Value { get; private set; }

    public SettingsDialog(KioskConfig source)
    {
        Value = new KioskConfig { Url = source.Url, EdgePath = source.EdgePath, EdgeZonePx = source.EdgeZonePx, DragDistancePx = source.DragDistancePx, GestureMaxMilliseconds = source.GestureMaxMilliseconds, MenuTimeoutSeconds = source.MenuTimeoutSeconds, ShutdownDelaySeconds = source.ShutdownDelaySeconds, OfflineRetrySeconds = source.OfflineRetrySeconds, RestartEdgeOnExit = source.RestartEdgeOnExit };
        Text = "KioskShell Settings"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(600, 300); MaximizeBox = false; MinimizeBox = false;
        var urlLabel = new Label { Text = "Website URL", Left = 24, Top = 24, AutoSize = true };
        _url.SetBounds(24, 50, 550, 32); _url.Text = source.Url;
        var edgeLabel = new Label { Text = "Top edge zone (px)", Left = 24, Top = 100, AutoSize = true };
        _edge.SetBounds(24, 124, 160, 30); _edge.Value = source.EdgeZonePx;
        var dragLabel = new Label { Text = "Drag distance (px)", Left = 220, Top = 100, AutoSize = true };
        _drag.SetBounds(220, 124, 160, 30); _drag.Value = source.DragDistancePx;
        var save = new Button { Text = "Save", Left = 340, Top = 235, Width = 110, Height = 36, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 464, Top = 235, Width = 110, Height = 36, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => { Value.Url = _url.Text.Trim(); Value.EdgeZonePx = (int)_edge.Value; Value.DragDistancePx = (int)_drag.Value; if (!Value.IsValid(out var error)) { MessageBox.Show(this, error); DialogResult = DialogResult.None; } };
        Controls.AddRange(new Control[] { urlLabel, _url, edgeLabel, _edge, dragLabel, _drag, save, cancel });
        AcceptButton = save; CancelButton = cancel;
    }
}