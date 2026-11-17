# KioskShell

A lightweight, portable kiosk browser for Windows.

It displays a single web page (or local HTML file) in true fullscreen and provides a simple touch-friendly menu for shutting down or restarting the PC.

---

## Features

- **True fullscreen** — no title bar, no taskbar, no Windows UI
- **Touch-friendly exit menu** — drag down from the top edge of the screen
- **Portable** — no installation required, runs from any folder or USB stick
- **Self-contained** — no .NET runtime needed on Windows 11
- **Offline fallback page** with automatic retry
- **Optional full kiosk mode** via Windows Assigned Access (Pro / Enterprise / Education)
- **Modern adaptive UI** with Fluent icons that scales to any screen size

---

## Quick Start

1. Go into the [`KioskShell`](KioskShell/) folder
2. Run **`KioskShell.exe`**
3. Drag down from the very top edge of the screen to open the menu

---

## Configuration

Edit `KioskShell/kiosk.json`:

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

| Setting | Description |
|---------|-------------|
| `Url` | Website (`https://...`) or local page (`assets/index.html`) |
| `EdgeZonePx` | How close to the top the gesture must start |
| `DragDistancePx` | How far you must drag to open the menu |
| `ShutdownDelaySeconds` | Countdown before power off / restart |

---

## Full Documentation

See the complete end-user guide here:  
**[KioskShell/README.md](KioskShell/README.md)**

It covers:
- Detailed configuration
- Custom local pages
- Full kiosk mode install/remove
- Smart App Control notes
- Troubleshooting

---

## Project Structure

```
kioskshell/
├── KioskShell/           ← Application folder (portable)
│   ├── KioskShell.exe
│   ├── kiosk.json
│   ├── assets/
│   ├── Manage-Kiosk.ps1
│   └── ...
├── .github/
└── README.md            ← This file
```

---

## License

Open source. Feel free to use, modify, and distribute.
