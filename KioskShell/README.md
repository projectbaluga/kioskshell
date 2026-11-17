# KioskShell: fullscreen web kiosk with a drag-down power menu

Shows one web page fullscreen (WebView2). Drag down from the very top edge to open a
touch-friendly menu: **Shut down / Restart / Cancel**. Shutdown runs with a 10 s countdown and a Cancel button.

> Written without a Windows machine to compile on, so it is **untested**. Build it and test
> it as a normal user first (steps 1-2) before enabling kiosk mode.

## 1. Build (on any Windows PC with the .NET 10 SDK)
```
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```
Edit `publish\kiosk.json` and set `Url`. The WebView2 runtime is normally preinstalled on Windows 11.

## 2. Test before locking down
Run `publish\KioskShell.exe`. With a mouse you can hold the left button at the very top and drag down
to test the gesture. On the touch screen, start the drag within ~28 px of the top edge.
Check the `Shut down` countdown and `Cancel` (log: `%LOCALAPPDATA%\KioskShell\log.txt`).

## 3. Install on the kiosk
- Copy `publish\*` to `C:\Program Files\KioskShell\`
- Create a **standard** local user (e.g. `KioskUser`) and sign in once so the profile exists.
- Edit the account name in `AssignedAccess.xml` if different.
- Apply it with `Apply-AssignedAccess.ps1` (as SYSTEM), or via Intune / a provisioning package.
- Turn off edge swipes so left/right swipes don't open Widgets or notifications:
  `reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\EdgeUI /v AllowEdgeSwipe /t REG_DWORD /d 0 /f`
- Optional but recommended: `powercfg /h off` (disables Fast Startup), and set Windows Update active hours.
- Sign out and sign in as `KioskUser` to test the full kiosk.

## 4. Admin exit
Ctrl+Alt+Del (or Windows button + Power on tablets) exits the kiosk to the sign-in screen.
Remove the kiosk later with `Clear-AssignedAccess`.

## Notes
- Standard users can normally shut down Windows. If you later apply the "remove Shut down" hardening
  policies, the menu's Shut down will silently fail. Test as the kiosk user.
- Tuning: `EdgeZonePx` (how close to the edge the touch must start) and `DragDistancePx`.
- If the page is unreachable, a "Reconnecting…" screen shows and the app retries; the gesture still works.
