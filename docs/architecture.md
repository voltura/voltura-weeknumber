# Architecture

Voltura WeekNumber is a Windows x64 application built with .NET 10 and WPF. The solution contains the application in `apps/windows` and the xUnit test project in `tests/VolturaWeekNumber.Tests`. Shared build metadata reads `version.json`. No sibling repository is required to build or run it.

## Application and UI

`App` owns single-instance activation and awaited shutdown. `AppRuntime` composes services and routes application actions. `MainWindow` owns navigation, activation, and layout. `CalendarViewModel` presents the selected date; `DateLookupViewModel` handles day-of-year and Julian-day conversions. `SettingsEditor` keeps a draft until Save. Stable choice objects preserve selections when the display language changes.

## Calendar and tray

`WeekCalculator` accepts a date, explicit calendar options, and regional culture. ISO mode and Gregorian Monday/FirstFourDayWeek use .NET `ISOWeek`; other combinations use the regional calendar. `WeekTracker` compares week-start dates, including the year, to deduplicate notifications. A dispatcher timer targets local midnight; time, resume, display, and preference events are coalesced into a pending refresh.

`NativeTray` owns the hidden native window, notification identity, tray menu, and SafeHandle-backed icon. Rendering depends on week number, appearance, and icon pixel size. Explorer recreation republishes the icon. Silent notifications use the native per-notification flag without changing Windows sound preferences.

The executable declares PerMonitorV2 DPI awareness. Window placement respects monitor work areas. Display changes, activation, and tray reopening trigger recovery checks; a stale window DPI is handled through a native move and bounds restoration so Windows can deliver its DPI-change message.

## Settings and diagnostics

`SettingsStore` validates bounded JSON before replacing current state and writes through a unique temporary file followed by atomic promotion. Installed data lives in `%LOCALAPPDATA%\Voltura\WeekNumber`; portable data lives in `Data` beside the executable. Autostart registration uses an application-owned command under the current user's registry and rolls back if settings persistence fails.

Optional application logging uses a bounded channel, one writer, and two rotating files of approximately 1 MiB each. See [Privacy](../PRIVACY.md) for stored data and network behavior, and [Validation](validation.md) for isolated diagnostic modes.

## Updates and installation

`UpdateService` owns its HTTP client, schedule, cancellation, synchronization gate, and installer-process reference. Update eligibility requires the running path to match the per-user uninstall registration. Portable, development, and isolated profiles use manual updates.

A pinned RSA public key verifies signed manifest bytes. The selected installer must match the signed version, package variant, filename, size, and SHA-256. Downloads and redirects are restricted to allowed HTTPS origins; reads are bounded. The package is verified again before launch, and installation requires a user action.

NSIS extracts a package and invokes `installer/maintain.ps1`. Maintenance rejects unowned targets and reparse points, validates the payload, stages replacements, and journals backup, promotion, health checks, and registration. Failed installations restore the prior installation where possible. Interrupted removal finishes deletion rather than restoring a partially removed application; a recovery uninstaller remains registered while deletion is pending.

`scripts/package.ps1` produces standard and self-contained installers and a self-contained portable ZIP. Self-contained packages include runtime license and notice files. Release manifests use RSA-PSS signatures, separately from Windows Authenticode publisher signing. Private signing keys and passphrases must remain outside the repository.
