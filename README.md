# KioskShell

KioskShell is a Windows desktop kiosk application built with .NET 10, Windows Forms, and Microsoft Edge WebView2. It displays a configured web page or local HTML asset in a borderless, topmost fullscreen window. A hidden top-edge drag gesture opens an on-screen menu for settings, shutdown, and restart.

This README is the project's single user and developer guide. It documents behavior implemented by the source code in this repository.

## What the application does

- Opens one configured page in a borderless fullscreen window.
- Uses WebView2 to render remote websites and local files from the `assets` directory.
- Opens links that request a new window in the existing kiosk window.
- Disables WebView2 developer tools, default context menus, browser accelerator keys, status bar, zoom controls, swipe navigation, password autosave, general autofill, and the built-in error page.
- Opens a touch- and mouse-accessible menu when the user drags down from the top edge.
- Provides a Settings dialog to change and save the page URL.
- Provides a delayed shutdown or restart action, with a Cancel action that can abort the pending shutdown.
- Shows a configurable offline page and retries navigation automatically.
- Shows a first-run setup wizard, requests Administrator approval through UAC, and configures Windows Assigned Access using the included PowerShell installer.
- Writes an install marker after successful setup so subsequent launches go directly to the fullscreen kiosk.

KioskShell is a fullscreen application and kiosk launcher. The source does not implement a complete Windows security boundary: it does not disable every operating-system shortcut or prevent a user with sufficient Windows access from changing system settings.

## Requirements

### Running

- Windows x64.
- The KioskShell executable and its accompanying files.
- A compatible Microsoft Edge WebView2 Runtime. KioskShell uses the installed runtime by default. A Fixed Version runtime can instead be placed in a `WebView2Runtime` directory beside the executable; that directory must contain `msedgewebview2.exe`.

The published application is self-contained with respect to .NET. WebView2 is a separate browser runtime dependency unless supplied as the local Fixed Version runtime.

### Building

- .NET 10 SDK.
- Windows x64 for the configured publish target.
- Internet access during restore if the NuGet package is not already cached.

The project targets `net10.0-windows`, uses Windows Forms, and references `Microsoft.Web.WebView2` version `1.0.2903.40`.

## Run the portable application

1. Obtain the published KioskShell output or build it using the instructions below.
2. Keep the executable, `kiosk.json`, `assets/`, and any optional `WebView2Runtime/` directory together.
3. Run `KioskShell.exe`.
4. The configured page opens in fullscreen.

The application uses a single-instance mutex. Starting a second copy while one is already running exits without opening another window.

## Open the menu

Start a touch or mouse gesture at the top edge of the displayed page and drag downward.

The gesture is recognized when:
- The gesture starts within `EdgeZonePx` pixels of the top of the page.
- The downward movement reaches `DragDistancePx` pixels.
- The horizontal movement remains smaller than `DragDistancePx`.
- The gesture completes within 1.5 seconds.

The gesture is injected into the top-level page and also supports mouse input for desktop testing. A page inside an iframe does not independently trigger the gesture.

The menu contains:

- **Settings** — opens a dialog to edit the configured page URL.
- **Shut down** — starts a Windows shutdown countdown.
- **Restart** — starts a Windows restart countdown.
- **Cancel** — closes the menu or aborts a pending shutdown countdown.

The menu closes after a period of inactivity. Its timeout is at least 5 seconds, even if a smaller value is configured. Shutdown and restart countdowns are at least 3 seconds.

## Configuration

The application reads `kiosk.json` from the same directory as `KioskShell.exe`. The checked-in configuration is:

```json
{
  "Url": "assets/index.html",
  "EdgeZonePx": 28,
  "DragDistancePx": 120,
  "MenuTimeoutSeconds": 20,
  "ShutdownDelaySeconds": 10,
  "OfflineRetrySeconds": 10
}
```

| Property | Default | Meaning |
| --- | ---: | --- |
| `Url` | `assets/index.html` | Initial page to display. |
| `EdgeZonePx` | `28` | Maximum distance from the top edge at which a gesture may begin. |
| `DragDistancePx` | `120` | Required downward drag distance; horizontal movement must remain below this value. |
| `MenuTimeoutSeconds` | `20` | Seconds before the idle menu closes. The runtime minimum is 5 seconds. |
| `ShutdownDelaySeconds` | `10` | Seconds before shutdown or restart. The runtime minimum is 3 seconds. |
| `OfflineRetrySeconds` | `10` | Delay between automatic retries after a navigation failure. The runtime minimum is 3 seconds. |

Configuration is deserialized case-insensitively. JSON comments and trailing commas are accepted. If the file is missing, invalid, or cannot be read, KioskShell falls back to the built-in defaults; configuration errors are written to the application log.

### URL resolution

The `Url` value is interpreted as follows:

- An absolute `http://` or `https://` URL is navigated to directly.
- An absolute `file:///` URL is navigated to directly.
- Any other value is treated as a path inside the local `assets/` directory. Backslashes are converted to forward slashes, leading slashes are removed, and an optional leading `assets/` is stripped.
- An empty value resolves to `about:blank`.

Examples:

| Value | Result |
| --- | --- |
| `https://example.com` | Opens the remote website. |
| `http://192.168.1.20:8080` | Opens the specified local-network website. |
| `file:///C:/Kiosk/page.html` | Opens the specified local file. |
| `assets/index.html` | Opens `assets/index.html` through the local virtual host. |
| `my-page.html` | Opens `assets/my-page.html` through the local virtual host. |
| `example.com` | Treated as a local asset path, not as a website URL. |

Local assets are mapped to the virtual HTTPS host `assets.kioskshell.local`. For example, `assets/index.html` is served from `https://assets.kioskshell.local/index.html`.

The Settings dialog saves the new `Url` value to `kiosk.json` and applies the navigation immediately. It does not edit the other configuration properties.

## Local pages and offline behavior

Place local HTML, CSS, JavaScript, images, and other page resources under `assets/`. Set `Url` to the desired relative asset path.

The repository includes:
- `assets/index.html` — sample landing page with a live clock and a reminder of the top-edge gesture.
- `assets/offline.html` — page displayed when navigation fails.

When navigation fails, KioskShell logs the failure, displays `assets/offline.html` if it can be read, and starts the retry timer. If the offline file is unavailable, a built-in “Reconnecting…” page is used. The retry timer navigates to the configured URL again after `OfflineRetrySeconds` (minimum 3 seconds).

## Application data and logs

KioskShell resolves its data directory when it first needs to write application data:

1. It tries to create and write to a `data/` directory beside the executable.
2. If that location is not writable, it uses `%LOCALAPPDATA%\KioskShell`.

The selected directory contains the WebView2 user-data/profile directory and `log.txt`. The log records unhandled application exceptions, configuration and save errors, WebView2 initialization failures, navigation failures, and shutdown command failures.

If the application cannot save settings, check that `kiosk.json` and its containing directory are writable by the current Windows user.

## Windows Assigned Access mode

The repository includes `Manage-Kiosk.ps1` to configure or remove Windows Assigned Access for KioskShell.

### Install

Requirements:
- Windows edition that supports the Assigned Access configuration used by the script (Windows Pro, Enterprise, or Education).
- An elevated PowerShell session.
- The portable application files, including `KioskShell.exe`, beside `Manage-Kiosk.ps1`.

From the KioskShell application directory, run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Manage-Kiosk.ps1 -Action Install
```

An alternate installation directory can be supplied:

```powershell
.\Manage-Kiosk.ps1 -Action Install -InstallDir 'D:\Kiosk'
```

The install action:
1. Copies the portable application directory to the installation directory unless source and destination are already the same.
2. Generates an Assigned Access XML configuration for KioskShell.
3. Applies the Assigned Access configuration through the MDM bridge as SYSTEM, using a temporary scheduled task.
4. Sets the Windows EdgeUI `AllowEdgeSwipe` policy value to `0`.
5. Runs `powercfg /h off` to disable hibernation and Fast Startup.
6. Reports whether Smart App Control is enabled, because it may block an unsigned executable.

Restart the PC after installation. Windows Assigned Access is configured to use its managed auto-logon kiosk account and launch KioskShell.

### Remove the Assigned Access configuration

Run elevated PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Manage-Kiosk.ps1 -Action Remove
```

The remove action clears the Assigned Access configuration and restores the EdgeUI `AllowEdgeSwipe` value to its pre-install state when a saved state is available. It also removes the first-run completion marker. It does **not** delete the copied application files or automatically delete the Windows kiosk account. Restart and sign in with a normal account after removal; review the kiosk account in Windows Settings if you no longer need it.

### Exiting the assigned kiosk session

The script's own instructions are to use **Ctrl+Alt+Del** to sign out or switch user. On a tablet, use the Windows button and Power options if available.

### Smart App Control

The script warns when it detects Smart App Control in the enabled state. Windows may block an unsigned KioskShell executable. The script does not disable Smart App Control or sign the executable.

## Build from source

From the repository root on Windows:

```powershell
dotnet restore KioskShell/KioskShell.csproj
dotnet publish KioskShell/KioskShell.csproj -c Release -o portable/KioskShell-Portable
```

The project is configured for a self-contained, compressed, single-file `win-x64` publish. It also copies `kiosk.json`, `Manage-Kiosk.ps1`, and the `assets/` contents to the output directory.

The GitHub Actions workflow at `.github/workflows/build.yml` runs on pushes and manual dispatches. It publishes the application on a Windows runner and uploads an artifact named `KioskShell-Portable`.

## Source layout

| Path | Responsibility |
| --- | --- |
| `KioskShell/Program.cs` | Single-instance startup, first-run detection, configuration loading/saving, app paths, and logging. |\n| `KioskShell/SetupWizardForm.cs` | First-run wizard and UAC-elevated installer launch. |
| `KioskShell/KioskForm.cs` | Fullscreen host window, WebView2 initialization/settings, URL resolution, navigation, offline fallback/retry, and gesture bridge. |
| `KioskShell/MenuForm.cs` | Fullscreen power menu, idle timeout, shutdown/restart countdown, and menu actions. |
| `KioskShell/SettingsForm.cs` | Dialog for changing and saving the page URL. |
| `KioskShell/KioskShell.csproj` | .NET target, WebView2 dependency, and publish settings. |
| `KioskShell/kiosk.json` | Default runtime configuration. |
| `KioskShell/Manage-Kiosk.ps1` | Windows Assigned Access install/remove operations. |
| `KioskShell/assets/` | Sample page and offline page. |
| `.github/workflows/build.yml` | Automated Windows publish workflow. |

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| The app does not open or Windows blocks it | Check Windows security controls and whether the executable is trusted. Smart App Control may block unsigned builds. |
| A blank page appears | Check `Url`, network connectivity, WebView2 Runtime availability, and `data/log.txt` (or `%LOCALAPPDATA%\KioskShell\log.txt` if the executable directory is not writable). |
| A website does not open | Use a complete `https://` or `http://` URL. A bare domain is interpreted as an asset path. |
| Local content does not load | Confirm the file exists beneath `assets/` and that the configured relative path matches its name. |
| The menu does not appear | Start the gesture at the very top of the page, drag down far enough, and complete it within 1.5 seconds. Check `EdgeZonePx` and `DragDistancePx`. |
| The menu closes too quickly | Increase `MenuTimeoutSeconds`; the effective minimum is 5 seconds. |
| The page keeps showing “Reconnecting…” | Check whether the configured page is reachable and inspect the application log. Automatic retry continues at the configured interval. |
| Settings cannot be saved | Make sure the application directory and `kiosk.json` are writable. |
| Shutdown or restart does not occur | Check Windows shutdown permissions and policy. The application starts the standard Windows `shutdown.exe` command. |
| Assigned Access installation fails | Run PowerShell as Administrator, verify the Windows edition supports Assigned Access, and inspect the error shown by the script. The temporary SYSTEM task writes details under `%ProgramData%\KioskShell\apply.log`. |

## License

See [LICENSE](LICENSE) for the repository's license terms.
