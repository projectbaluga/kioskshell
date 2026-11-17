# KioskShell

A lightweight, portable kiosk browser for Windows.  
It displays a single web page (or local HTML file) in true fullscreen and provides a simple touch-friendly menu for shutting down or restarting the PC.

---

## Features

- **True fullscreen** — no title bar, no taskbar, no Windows UI
- **Touch-friendly exit menu** — drag down from the top edge of the screen
- **Portable** — no installation required, runs from any folder or USB stick
- **Self-contained** — no .NET runtime needed (Windows 11 already includes WebView2)
- **Offline fallback page** with automatic retry
- **Optional full kiosk mode** via Windows Assigned Access (Pro/Enterprise/Education)

---

## Quick Start (Portable Mode)

1. Download or copy the entire `KioskShell` folder to any location (Desktop, USB drive, etc.).
2. Double-click **`KioskShell.exe`**.
3. The configured page will open in fullscreen.

**To open the menu:**  
Place your finger (or mouse) at the **very top edge** of the screen and drag downward.  
A menu with **Shut down**, **Restart**, and **Cancel** will appear.

---

## Folder Contents

| File / Folder          | Purpose                                      |
|------------------------|----------------------------------------------|
| `KioskShell.exe`       | Main application                             |
| `WebView2Loader.dll`   | Required — keep next to the exe               |
| `kiosk.json`           | Configuration file (edit this)               |
| `assets\index.html`    | Default sample page                          |
| `assets\offline.html`  | Shown when the page fails to load            |
| `assets\icon.ico`      | Application icon                             |
| `Manage-Kiosk.ps1`     | Script to install/remove full kiosk mode     |
| `data\`                | Created on first run (browser cache + logs)  |

---

## Configuration (`kiosk.json`)

Open `kiosk.json` with any text editor (Notepad is fine).

```json
{
  "Url": "https://example.com",

  "EdgeZonePx": 28,
  "DragDistancePx": 120,
  "MenuTimeoutSeconds": 20,
  "ShutdownDelaySeconds": 10,
  "OfflineRetrySeconds": 10
}
```

### Settings Explained

| Setting                | Description                                                                 | Default |
|------------------------|-----------------------------------------------------------------------------|---------|
| **Url**                | Website or local page to display. Must include `https://` for websites.     | `assets/index.html` |
| **EdgeZonePx**         | How close to the top edge the touch/mouse must start (in pixels)            | 28      |
| **DragDistancePx**     | How far you must drag down to trigger the menu                              | 120     |
| **MenuTimeoutSeconds** | Menu automatically closes after this many seconds of inactivity             | 20      |
| **ShutdownDelaySeconds** | Countdown time before the PC actually shuts down/restarts                 | 10      |
| **OfflineRetrySeconds**  | How often to retry loading the page when offline                          | 10      |

### Examples of valid `Url` values

```json
"Url": "https://dashboard.mycompany.com"
"Url": "https://192.168.1.50:8080"
"Url": "assets/index.html"
"Url": "assets/my-local-page.html"
```

> **Important:** Plain text like `"example.com"` is treated as a local file. Always include `https://` for websites.

After saving `kiosk.json`, restart `KioskShell.exe` for the changes to take effect.

---

## Custom Local Pages

You can place any HTML/CSS/JS files inside the `assets` folder and point `Url` to them:

```json
"Url": "assets/my-dashboard.html"
```

Local pages are served under a virtual host so they behave like normal websites.

---

## Full Kiosk Mode (Windows 11 Pro / Enterprise / Education)

This mode locks the PC into KioskShell using Windows Assigned Access. The machine will auto-login to a dedicated kiosk account and only allow this app.

### Requirements
- Windows 11 Pro, Enterprise, or Education
- Administrator privileges

### Install

1. **Turn off Smart App Control** (it blocks unsigned apps):
   - Open **Windows Security** → **App & browser control** → **Smart App Control settings** → **Off**

2. Open **PowerShell as Administrator** inside the KioskShell folder and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Manage-Kiosk.ps1 -Action Install
```

The script will:
- Copy files to `C:\KioskShell`
- Create a dedicated kiosk user account with auto-sign-in
- Configure Assigned Access
- Disable edge swipe gestures and Fast Startup

3. Restart the computer.

### Exit Full Kiosk Mode

- Press **Ctrl + Alt + Del**, then sign out or switch user, **or**
- On a tablet: use the Windows button + Power options

### Uninstall Full Kiosk Mode

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Manage-Kiosk.ps1 -Action Remove
```

---

## Smart App Control Warning

On newer Windows 11 systems, **Smart App Control** may block `KioskShell.exe` because it is not digitally signed.

**Solution:** Temporarily turn it off (see steps above).  
There is currently no way to allow a single unsigned app while keeping Smart App Control enabled.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| App won't start / blocked by Windows | Disable Smart App Control (see above) |
| Black screen or nothing loads | Check `data\log.txt` for errors. Verify the `Url` in `kiosk.json` is correct and reachable. |
| Menu does not appear | Make sure you start the drag from the very top edge of the screen. Try increasing `EdgeZonePx` or decreasing `DragDistancePx`. |
| Page shows "Reconnecting…" | The configured URL is offline or unreachable. The app will retry automatically. |
| Shutdown / Restart does nothing | The current Windows user may not have shutdown rights. Run as a standard user or check Group Policy. |
| WebView2 errors | Windows 11 includes the WebView2 runtime. On older systems, download the Evergreen runtime from Microsoft or place a Fixed Version runtime in a `WebView2Runtime` folder next to the exe. |

---

## Tips

- You can run KioskShell from a USB stick for temporary demos.
- The `data` folder is safe to delete if you want to clear the browser cache and logs.
- For digital signage or information displays, point `Url` to your dashboard or slideshow page.
- The menu has a modern adaptive design that scales automatically to different screen sizes and orientations.

---

## Support & Feedback

This is an open-source project.  
Issues, suggestions, and contributions are welcome on the GitHub repository.

---

**KioskShell** — Simple. Portable. Focused.
