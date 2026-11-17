# KioskShell (portable)

Shows one web page fullscreen. **Drag down from the very top edge** to open a touch-friendly menu:
**Shut down / Restart / Cancel** (10-second countdown with Cancel).

## What is in the folder
| Item | Purpose |
|---|---|
| `KioskShell.exe` | The app. Self-contained: no .NET install required. |
| `WebView2Loader.dll` | Must stay next to the exe. |
| `kiosk.json` | Settings: the web address and gesture tuning. |
| `assets\index.html` | Sample page shown by default. Replace it, or point `Url` at your own site. |
| `assets\offline.html` | Shown when the page can't load (retries automatically). Edit freely. |
| `assets\icon.ico` | App icon. |
| `Manage-Kiosk.ps1` | One-command install/remove as a Windows kiosk. |
| `data\` | Created at first run: browser data and `log.txt`. Safe to delete. |

## Run it (portable, no install)
Copy the folder anywhere (USB stick included) and run `KioskShell.exe`.
Test the gesture with a mouse: hold the left button at the very top edge and drag down.

Optional: for PCs without the Edge WebView2 runtime (Windows 11 already has it), drop a *Fixed Version*
WebView2 runtime folder (the one containing `msedgewebview2.exe`) into `WebView2Runtime\` next to the exe.

## Settings (`kiosk.json`)
- `Url`: `https://...` for a website, or `assets/index.html` for a local page. Plain text like `example.com` is treated as a local file, so include `https://`.
- `EdgeZonePx` / `DragDistancePx`: how close to the top the touch must start, and how far it must drag.
- `ShutdownDelaySeconds`: countdown length before the PC actually powers off.

## Make it a kiosk (Windows 11 Pro/Enterprise/Education)
1. Turn off Smart App Control (it blocks unsigned apps), or sign the exe.
2. In an **elevated PowerShell** inside this folder:
   `Set-ExecutionPolicy -Scope Process Bypass; .\Manage-Kiosk.ps1 -Action Install`
   (copies to `C:\KioskShell`, creates an auto-sign-in kiosk account, applies Assigned Access,
   disables edge-swipe gestures and Fast Startup)
3. Restart. Admin exit: Ctrl+Alt+Del (or Windows button + Power on a tablet).
4. To undo: `.\Manage-Kiosk.ps1 -Action Remove`

## Notes
- Standard users can normally shut down Windows. If you later apply hardening that removes the shutdown right, the menu's Shut down will silently fail.
- The first build is untested on real hardware. Check `data\log.txt` if something doesn't start.
