# Voltura WeekNumber

A compact .NET 10 WPF calendar-week utility for Windows x64, developed by Voltura AB.

The notification-area calendar shows the current week using independently rendered 16–256 px artwork. Double-click it to look up another date. Closing the window keeps the application running; choose **Exit** from the tray menu to stop it.

## Features

- English, Swedish, and German; display language is independent of calendar rules.
- Windows regional, ISO 8601, and custom week-number conventions.
- System/light/dark window appearance, automatic taskbar-icon contrast, custom icon colors/transparency, and multi-resolution ICO export.
- Optional Windows autostart, startup/new-week notifications, and per-notification silence.
- Atomic settings persistence, settings import/export, and optional bounded application logs.
- Signed-manifest update verification and automatic downloads for installed builds; installation is explicitly activated by the user.
- PerMonitorV2 awareness, monitor work-area recovery, and deterministic tray/native resource cleanup.

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

## Release status

Current artifacts are local development packages. They are not published and have no Authenticode publisher signature. A dedicated update-verification public key has not yet been configured. Installed development builds therefore fail closed when checking updates.

Before a first production build, run `scripts/initialize-signing-key.ps1 -PrivateKeyPath <outside-repository-path>` in an interactive PowerShell terminal. It asks for a passphrase and writes only the public key into the project. Back up the encrypted private key and retain its passphrase separately. Then rebuild and use `scripts/release.ps1 -KeyPath <private-key-path>` to prepare signed update metadata locally.

Publishing additionally requires the public GitHub repository, an existing release tag, release notes, and the explicit `-Publish` switch. No script commits, pushes, creates a GitHub repository, or generates a release tag.

See [architecture](docs/architecture.md), [feature parity](docs/feature-parity.md), and [validation](docs/validation.md).
