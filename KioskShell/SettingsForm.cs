using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace KioskShell;

/// <summary>Simple settings dialog — change the page URL and save to kiosk.json.</summary>
public sealed class SettingsForm : Form
{
    private readonly KioskConfig _cfg;
    private readonly float _scale;
    private readonly TextBox _urlBox;
    private readonly Button _save;
    private readonly Button _cancel;

    public SettingsForm(KioskConfig cfg, float scale)
    {
        _cfg = cfg;
        _scale = Math.Clamp(scale, 0.7f, 1.5f);

        var screen = Screen.PrimaryScreen!.Bounds;
        int w = (int)(Math.Min(screen.Width * 0.7f, 720) * _scale);
        int h = (int)(280 * _scale);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(w, h);
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(28, 32, 42);

        // Rounded form
        Region = CreateRoundedRegion(w, h, (int)(16 * _scale));

        var title = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI Semibold", 20f * _scale),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point((int)(28 * _scale), (int)(24 * _scale))
        };

        var urlLabel = new Label
        {
            Text = "Page URL",
            Font = new Font("Segoe UI", 12f * _scale),
            ForeColor = Color.FromArgb(180, 185, 200),
            AutoSize = true,
            Location = new Point((int)(28 * _scale), (int)(78 * _scale))
        };

        _urlBox = new TextBox
        {
            Text = cfg.Url ?? "",
            Font = new Font("Segoe UI", 14f * _scale),
            BackColor = Color.FromArgb(40, 46, 58),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point((int)(28 * _scale), (int)(108 * _scale)),
            Size = new Size(w - (int)(56 * _scale), (int)(40 * _scale))
        };

        int btnW = (int)(160 * _scale);
        int btnH = (int)(48 * _scale);
        int btnY = h - (int)(72 * _scale);

        _save = MakeButton("Save", Color.FromArgb(45, 130, 210),
            new Point(w - (int)(28 * _scale) - btnW * 2 - (int)(12 * _scale), btnY), btnW, btnH);
        _cancel = MakeButton("Cancel", Color.FromArgb(55, 60, 75),
            new Point(w - (int)(28 * _scale) - btnW, btnY), btnW, btnH);

        _save.Click += (_, _) =>
        {
            var url = _urlBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, "Please enter a URL.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _cfg.Url = url;
            if (_cfg.Save())
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(this,
                    "Could not save settings. Check that the folder is writable.",
                    "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange(new Control[] { title, urlLabel, _urlBox, _save, _cancel });

        // Focus the URL box
        Shown += (_, _) => { _urlBox.Focus(); _urlBox.SelectAll(); };
    }

    private Button MakeButton(string text, Color bg, Point location, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 13f * _scale),
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = location,
            Size = new Size(width, height),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Region = CreateRoundedRegion(width, height, (int)(10 * _scale));
        return btn;
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
