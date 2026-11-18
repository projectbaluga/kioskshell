using System.Diagnostics;

namespace KioskShell;

/// <summary>
/// First-run setup. The wizard saves the selected page and starts the installer with a UAC
/// elevation prompt. Windows Assigned Access then creates and manages the kiosk auto-logon user.
/// </summary>
internal sealed class SetupWizardForm : Form
{
    private readonly TextBox _url = new();
    private readonly TextBox _displayName = new();
    private readonly TextBox _installDir = new();
    private readonly Label _status = new();
    private readonly Button _install = new();
    private readonly Button _cancel = new();

    public SetupWizardForm()
    {
        Text = "KioskShell Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(620, 470);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(24, 28, 38);
        ForeColor = Color.White;

        var title = new Label
        {
            Text = "Welcome to KioskShell",
            Font = new Font("Segoe UI Semibold", 22),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(28, 24)
        };
        var intro = new Label
        {
            Text = "Let's prepare this Windows PC for single-app kiosk mode. Windows will ask for Administrator approval before making system changes.",
            ForeColor = Color.FromArgb(190, 198, 212),
            Location = new Point(30, 72),
            Size = new Size(560, 48)
        };

        AddField("Website or local page", _url, "https://example.com", 142);
        _url.Text = KioskConfig.Load().Url;
        AddField("Kiosk sign-in display name", _displayName, "Kiosk", 220);
        _displayName.Text = "Kiosk";
        AddField("Application install folder", _installDir, @"C:\KioskShell", 298);
        _installDir.Text = @"C:\KioskShell";

        var note = new Label
        {
            Text = "Setup configures Windows Assigned Access and its managed standard-user auto sign-in. Keep your normal Administrator account and password available for maintenance. A restart is required after setup.",
            ForeColor = Color.FromArgb(190, 198, 212),
            Location = new Point(30, 356),
            Size = new Size(560, 48)
        };
        _status.SetBounds(30, 408, 340, 28);
        _status.ForeColor = Color.FromArgb(255, 205, 110);

        _install.Text = "Install kiosk";
        _install.SetBounds(390, 410, 120, 36);
        _install.BackColor = Color.FromArgb(40, 125, 210);
        _install.ForeColor = Color.White;
        _install.FlatStyle = FlatStyle.Flat;
        _install.FlatAppearance.BorderSize = 0;
        _install.Click += (_, _) => StartInstall();

        _cancel.Text = "Exit";
        _cancel.SetBounds(520, 410, 70, 36);
        _cancel.BackColor = Color.FromArgb(55, 62, 76);
        _cancel.ForeColor = Color.White;
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.FlatAppearance.BorderSize = 0;
        _cancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { title, intro, note, _status, _install, _cancel });
        AcceptButton = _install;
        CancelButton = _cancel;
    }

    private void AddField(string caption, TextBox box, string placeholder, int y)
    {
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = Color.FromArgb(205, 212, 224),
            Location = new Point(30, y)
        };
        box.SetBounds(30, y + 24, 560, 30);
        box.Text = placeholder;
        box.BackColor = Color.FromArgb(42, 48, 62);
        box.ForeColor = Color.White;
        box.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(label);
        Controls.Add(box);
    }

    private void StartInstall()
    {
        var url = _url.Text.Trim();
        var display = _displayName.Text.Trim();
        var installDir = _installDir.Text.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(display) ||
            string.IsNullOrWhiteSpace(installDir) || !Path.IsPathFullyQualified(installDir))
        {
            MessageBox.Show(this, "Enter a page URL, display name, and a fully qualified install folder (for example C:\\KioskShell).",
                "Setup details required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var cfg = KioskConfig.Load();
        cfg.Url = url;
        if (!cfg.Save())
        {
            MessageBox.Show(this, "KioskShell could not save kiosk.json. Move the app to a writable folder and try again.",
                "Cannot save settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var script = Path.Combine(AppPaths.BaseDir, "Manage-Kiosk.ps1");
        if (!File.Exists(script))
        {
            MessageBox.Show(this, "Manage-Kiosk.ps1 was not found beside KioskShell.exe. Please use the complete portable package.",
                "Installer missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Action Install -InstallDir \"{installDir}\" -DisplayName \"{display.Replace("\"", "\"\"")}\"";
            Process.Start(new ProcessStartInfo("powershell.exe", args)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppPaths.BaseDir
            });
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            _status.Text = "Administrator approval was cancelled.";
        }
        catch (Exception ex)
        {
            Log.Write("Could not launch elevated installer: " + ex);
            MessageBox.Show(this, "Could not start the elevated installer. " + ex.Message,
                "Setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
