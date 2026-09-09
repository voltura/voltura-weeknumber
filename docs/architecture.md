# Architecture

Voltura WeekNumber is a Windows x64 application built with .NET 10 and WPF. The solution contains the application in `apps/windows` and the xUnit test project in `tests/VolturaWeekNumber.Tests`. Shared build metadata reads `version.json`. No sibling repository is required to build or run it.

## Application and UI

`App` owns single-instance activation and awaited shutdown. `AppRuntime` composes services and routes application actions. `MainWindow` owns navigation, activation, and layout. `CalendarViewModel` presents the selected date; `DateLookupViewModel` handles day-of-year and Julian-day conversions. `SettingsEditor` keeps a draft until Save. Stable choice objects preserve selections when the display language changes.

## Calendar and tray

`WeekCalculator` accepts a date, explicit calendar options, and regional culture. ISO mode and Gregorian Monday/FirstFourDayWeek use .NET `ISOWeek`; other combinations use the regional calendar. `WeekTracker` compares week-start dates, including the year, to deduplicate notifications. A dispatcher timer targets local midnight; time, resume, display, and preference events are coalesced into a pending refresh.

`NativeTray` owns the hidden native window, notification identity, tray menu, and SafeHandle-backed icon. Rendering depends on week number, appearance, and icon pixel size. Explorer recreation republishes the icon. Silent notifications use the native per-notification flag without changing Windows sound preferences.

Dates outside the regional calendar's supported range show an unavailable week (—) in the tray while midnight scheduling remains active. The next valid refresh restores the week and resets the notification baseline. Icons support weeks 1–56, including longer lunisolar leap years.

The executable declares PerMonitorV2 DPI awareness. Window placement respects monitor work areas. Display changes, activation, and tray reopening trigger recovery checks; a stale window DPI is handled through a native move and bounds restoration so Windows can deliver its DPI-change message.

`WindowWorkAreaPlacement` retains the requested logical size while hidden and across display changes. Only an interactive user resize updates that preference; native bounds changes do not. Recovery fits the retained size to the current work area.

## Settings and diagnostics

`SettingsStore` validates bounded JSON before replacing current state and writes through a unique temporary file followed by atomic promotion. Installed data lives in `%LOCALAPPDATA%\Voltura\WeekNumber`; portable data lives in `Data` beside the executable. Autostart registration uses an application-owned command under the current user's registry and rolls back if settings persistence fails.

Optional application logging uses a bounded channel, one writer, and two rotating files of approximately 1 MiB each. See [Privacy](../PRIVACY.md) for stored data and network behavior, and [Validation](validation.md) for isolated diagnostic modes.

## Updates and installation

`UpdateService` owns HTTP, one operation gate, a disposable pending package, and an immutable UI state. Automatic checks run two minutes after startup and then daily while enabled; the schedule is in memory. Manual checks use the same operation. Update eligibility requires the running path to match the per-user uninstall registration. Portable, development, and isolated profiles use manual updates.

Signature verification establishes authenticity; the service separately decides whether a version is newer. Startup restores a verified newer package or discards obsolete, incomplete, or damaged cache files. Cache cleanup is best effort and never claims that the app is up to date: only a successful online check does that. Replacing a package removes its old manifest first and publishes the new manifest last. Installation reverifies the package and holds the operation gate until setup exits. The UI receives its status and install action together; failures identify checking, downloading, verification, or launching setup. The optional application log records the failed operation and exception type.

A pinned RSA public key verifies signed manifest bytes. The selected installer must match the signed version, package variant, filename, size, and SHA-256. Downloads and redirects are restricted to allowed HTTPS origins; reads are bounded. The package is verified again before launch, and installation requires a user action.

NSIS extracts a package and invokes `installer/maintain.ps1`. Maintenance rejects unowned targets and reparse points, validates the payload, stages replacements, and journals backup, promotion, health checks, and registration. Failed installations restore the prior installation where possible. Interrupted removal finishes deletion rather than restoring a partially removed application; a recovery uninstaller remains registered while deletion is pending.

`scripts/package.ps1` produces standard and self-contained installers and a self-contained portable ZIP. Self-contained packages include runtime license and notice files. Release manifests use RSA-PSS signatures, separately from Windows Authenticode publisher signing. Private signing keys and passphrases must remain outside the repository.
