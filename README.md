# Voltura WeekNumber

A compact .NET 10 WPF calendar-week utility for Windows x64, developed by Voltura AB.

The notification-area calendar shows the current week using independently rendered 16–256 px artwork. Double-click it to look up another date. Closing the window keeps the application running; choose **Exit** from the tray menu to stop it.

## Features

- English, Swedish, and German; display language is independent of calendar rules.
- Windows regional, ISO 8601, and custom week-number conventions.
- Day-of-year lookup (001–365/366), including reverse lookup by year and day.
- Integer Julian-day lookup in both directions, using noon Universal Time and Gregorian dates for years 1–9999.
- System/light/dark window appearance, automatic taskbar-icon contrast, custom icon colors/transparency, and multi-resolution ICO export.
- Optional Windows autostart, startup/new-week notifications, and per-notification silence.
- Atomic settings persistence, settings import/export, and optional bounded application logs.
- Signed-manifest update verification and automatic downloads for installed builds; installation is explicitly activated by the user.
- PerMonitorV2 awareness, monitor work-area recovery, and deterministic tray/native resource cleanup.
- Automatic tray-icon visibility using the same per-application promotion as Voltura Air, where supported by Windows.

## Build and run

Requires Windows, .NET SDK 10.0.400, and PowerShell 7.6. Packaging also requires NSIS.

```powershell
./scripts/build.ps1
dotnet run --project apps/windows/VolturaWeekNumber.csproj
./scripts/package.ps1
./scripts/test-installer.ps1
```

Packages are written to `artifacts/publish`: a small runtime-acquiring installer, a self-contained offline installer, and a portable ZIP. The small installer verifies the Microsoft .NET Desktop Runtime installer before requesting elevation. The application itself installs per user and runs without elevation.

Installed settings live in `%LOCALAPPDATA%\Voltura\WeekNumber`; portable settings live in `Data` beside the portable executable. Old WeekNumber XML settings are intentionally not imported. A damaged settings file is preserved and requires a valid import or a manual repair.

## Display recovery diagnostics

For an isolated TV/receiver or monitor reconnection test, launch the executable with
`--isolated-test-mode <absolute-test-profile-directory> --trace-dpi`.
The bounded `application.log` in that directory records display events and the visible
window's native DPI, monitor DPI, WPF scale, bounds, and visibility. It does not poll.
The existing `--render-review <absolute-output-directory>` mode also writes `window-dpi.json`
and captures both lookup pages in English, Swedish, and German at normal and compact sizes.

Recovery checks on display changes, activation, and tray reopening detect a stale window DPI.
When it differs from the current monitor, a one-pixel native move and bounds restoration lets
Windows deliver its own DPI change to WPF. Real TV/receiver power-cycle testing remains necessary
to validate recovery on the affected hardware.
