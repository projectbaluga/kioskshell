# KioskShell 2.0 — Production Migration Plan

This document records the production implementation direction. The existing `main` branch remains the stable legacy implementation; all v2 work belongs on `feature/kioskshell-2-web-migration`. The v2 workstream is production implementation, not a throwaway prototype.

## Product goal

Replace the WinForms + WebView2 kiosk host with Microsoft Edge in kiosk mode, while preserving the current KioskShell workflow and behavior:

- Load an administrator-configured external website directly.
- Start automatically in a locked-down, full-screen kiosk session.
- Reveal the KioskShell control menu using the existing top-edge drag gesture.
- Provide settings and controlled website configuration.
- Provide shutdown/restart countdowns with reliable cancellation.
- Provide a managed offline/recovery experience when the external site is unavailable.
- Preserve administrator maintenance, installation/uninstallation, and useful logs.

## Production architecture

1. **Microsoft Edge kiosk** displays the configured external URL directly. Direct navigation avoids iframe restrictions and preserves site cookies, redirects, and browser APIs.
2. **Windows Assigned Access** starts the kiosk session using a dedicated standard account and a managed launcher. The installer must select a supported Edge kiosk/Assigned Access configuration for the detected Windows edition; it must not assume that every Windows edition supports the same kiosk features.
3. **KioskShell Windows helper** is a small, signed/releasable OS integration service/application, not a second browser. It:
   - detects the top-edge drag gesture independently of the external website DOM;
   - presents a trusted, touch-friendly local menu/settings surface above Edge;
   - launches and monitors Edge kiosk, and recovers it after unexpected exit;
   - owns the shutdown/restart countdown and cancellation;
   - maintains the approved local configuration and diagnostic logs.
4. **Configuration and IPC** are local and administrator-controlled. Any local API/IPC must be loopback-only or use an authenticated local IPC mechanism, with a per-install credential where applicable. Expose only fixed, allowlisted operations.
5. **Installer and uninstaller** configure Edge, Assigned Access, helper startup/recovery, and required policies. Installation must validate prerequisites, preserve prior system settings, log every change, and roll back partial changes. Removal must restore only settings KioskShell changed and preserve unrelated user data.

## Gesture and menu behavior

Preserve the existing top-edge drag-down gesture (configurable edge zone and drag threshold). Since Edge loads the external URL directly, gesture detection must not depend on injected JavaScript or the site's DOM. The helper owns input detection and displays a separate trusted menu. The implementation must handle touch and mouse input, multiple displays, DPI scaling, and menu dismissal without leaving an invisible input-blocking surface.

## Security requirements

- Run the kiosk session as a standard user, not an administrator.
- Do not expose arbitrary command execution, executable paths, shell arguments, or PowerShell snippets to browser content.
- Keep system actions inside the helper and invoke only fixed shutdown/restart operations.
- Require an explicit user action, visible countdown, and reliable cancel path before power actions.
- Protect local IPC/API with loopback binding plus authentication, or an equivalent ACL-protected named pipe.
- Validate URL schemes and configuration values; reject malformed or unsafe settings.
- Treat all external website content as untrusted.
- Keep administrator maintenance separate from ordinary kiosk controls.
- Avoid weakening Smart App Control, Code Integrity, or other Windows security controls as an installation workaround.

## Production implementation sequence

1. **V2 application foundation** — add a separate v2 solution/project and configuration model; retain v1 source and behavior on `main`.
2. **Edge lifecycle and kiosk launch** — implement direct URL launch, kiosk arguments/policies, process supervision, startup, and crash recovery.
3. **Trusted helper UI and gesture** — implement global top-edge drag detection, menu, settings, idle dismissal, and DPI/multi-monitor handling.
4. **Power workflow** — implement shutdown/restart countdown, cancellation, error reporting, and audit logging.
5. **Offline and recovery** — define reliable detection/retry behavior and a managed fallback that does not depend on modifying the external website.
6. **Installer, maintenance, and removal** — implement prerequisite/edition checks, idempotent installation, state backup, rollback, repair, and safe uninstall.
7. **Automated and device validation** — build in CI and run the full acceptance suite on the target Windows edition/build, including touch hardware and real external websites.
8. **Release** — publish a separate v2 production artifact only after all release gates pass. Do not replace the v1 artifact or merge into `main` prematurely.

## Production release gates

- [ ] Existing `main` branch remains unchanged by v2 work.
- [ ] Edge launches the configured external URL directly in kiosk mode at sign-in.
- [ ] Helper and Edge recover after process failure and after reboot.
- [ ] Top-edge drag-down gesture works with touch and mouse over arbitrary external sites.
- [ ] Menu/settings remain usable without modifying the external site's DOM.
- [ ] Shutdown/restart countdowns can be cancelled reliably; no duplicate power actions remain queued.
- [ ] No arbitrary command execution is exposed to browser content.
- [ ] Offline detection, fallback, and retry behavior are tested against real network failures.
- [ ] Installer validates Windows edition/prerequisites, backs up prior state, and rolls back failures.
- [ ] Uninstaller restores only KioskShell-managed settings and leaves unrelated data intact.
- [ ] CI build and packaging succeed; artifacts are inspected.
- [ ] Full end-to-end tests pass on the exact Windows edition/build and touch hardware intended for deployment.
- [ ] Release notes, versioning, checksums, and recovery/maintenance instructions are published.

## Important implementation note

A pure webpage cannot securely shut down Windows, restart the PC, or reliably draw a trusted menu over an arbitrary external site. Those capabilities remain in the small local helper. The main user-facing browsing experience is web-based; the helper is a narrowly scoped system integration component.
