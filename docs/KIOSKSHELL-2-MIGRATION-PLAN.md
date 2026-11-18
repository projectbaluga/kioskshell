# KioskShell 2.0 — Web Migration Plan

This document records the agreed migration direction. The existing `main` branch remains the stable legacy implementation; all v2 work belongs on `feature/kioskshell-2-web-migration`.

## Product goal

Replace the WinForms + WebView2 kiosk host with Microsoft Edge in kiosk mode, while preserving the current KioskShell workflow and behavior:

- Load an administrator-configured external website.
- Start automatically in a locked-down, full-screen kiosk session.
- Reveal the KioskShell control menu using the existing top-edge drag gesture.
- Provide settings and controlled website configuration.
- Provide shutdown/restart countdowns with cancellation.
- Show an offline state and retry/recover when the external site is unavailable.
- Preserve administrator maintenance, installation/uninstallation, and useful logs.

## Target architecture

1. **Microsoft Edge kiosk** displays the configured external URL directly. Direct navigation is preferred over an iframe wrapper because many external sites disallow framing and may depend on cookies, redirects, or browser APIs.
2. **Windows Assigned Access** launches Edge in kiosk mode for a dedicated standard kiosk account. The supported Edge kiosk mode and Windows edition/Assigned Access constraints must be validated on the target device before finalizing installer behavior.
3. **Small Windows helper** provides only OS integration that a normal webpage cannot do:
   - detect the configured top-edge drag gesture while the external page is displayed;
   - present the KioskShell control/settings surface without depending on the external website DOM;
   - expose a loopback-only, authenticated, narrowly allowlisted API for shutdown/restart and configuration operations;
   - implement countdown/cancel semantics and write diagnostic logs.
4. **Configuration** remains local and administrator-controlled. Store the target URL and kiosk behavior in a validated configuration file. Do not let arbitrary webpages invoke arbitrary shell commands.
5. **Installer scripts** configure the kiosk account/Assigned Access, Edge kiosk launch, helper startup, and recovery. Uninstall must restore the prior kiosk configuration safely and must not remove unrelated user data.

## Gesture behavior

Preserve the current top-edge drag-down gesture. Because the target website is external and will be loaded directly by Edge, do not rely on injecting JavaScript into that site or on its DOM. The helper must own gesture detection and show the KioskShell menu as a separate trusted local surface. Define and test the gesture threshold, edge zone, pointer capture, and menu dismissal behavior.

## Security requirements

- Run the kiosk account as a standard user, not administrator.
- Bind helper IPC/API only to loopback; reject remote network clients.
- Require a per-install secret or equivalent local authorization and validate request origin/host where applicable.
- Implement explicit allowlisted operations only (for example, status, get/update approved settings, shutdown, restart, cancel pending power action).
- Never accept executable paths, command strings, PowerShell snippets, or arbitrary arguments from the webpage.
- Require confirmation and a visible countdown before shutdown/restart; cancellation must be reliable.
- Keep maintenance access separate from ordinary kiosk controls.
- Treat external website content as untrusted.

## Migration stages

1. **Inventory and parity specification** — map current setup, URL configuration, hidden gesture, settings, offline/retry, power countdown/cancel, logs, and maintenance behavior to tests.
2. **Helper prototype** — prove the top-edge gesture, trusted menu surface, loopback API, countdown/cancel, shutdown/restart, and recovery on the target Windows build.
3. **Web/kiosk prototype** — launch a configurable external URL directly in Edge kiosk mode under a dedicated account.
4. **Installer/uninstaller** — implement idempotent setup, validation, rollback, and safe removal.
5. **End-to-end validation** — test startup, URL changes, blocked/framed sites, offline/retry, gesture, menu, power actions, crash/relaunch, and admin maintenance.
6. **Release** — publish a separate v2 artifact only after validation; do not replace the existing main-branch release prematurely.

## Acceptance checklist

- [ ] Existing main branch is unchanged by v2 work.
- [ ] Edge launches the configured external URL automatically in kiosk mode.
- [ ] Top-edge drag-down gesture opens the KioskShell menu over arbitrary external sites.
- [ ] Menu/settings remain usable without modifying the external site's DOM.
- [ ] Shutdown/restart countdowns can be cancelled.
- [ ] No arbitrary command execution is exposed to the browser.
- [ ] Offline/retry behavior is defined and tested (note: Edge's own error page may require a managed local fallback strategy).
- [ ] Helper and Edge recover after process failure.
- [ ] Installer/uninstaller are repeatable and preserve unrelated system settings/data.
- [ ] Tested on the exact Windows edition/build used by the kiosk.

## Important implementation note

A pure webpage cannot securely or directly shut down Windows, restart the PC, or reliably draw a trusted menu over an arbitrary external site. Those capabilities remain in the small local helper. The main user-facing browsing experience is still web-based; the helper is a narrowly scoped system integration component, not a replacement desktop kiosk application.
