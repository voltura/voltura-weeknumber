# Architecture

Voltura WeekNumber is a Windows x64 application built with .NET 10 and WPF. The solution contains the application in `apps/windows` and the xUnit test project in `tests/VolturaWeekNumber.Tests`. Shared build metadata reads `version.json`.

## Application and UI

`App` owns single-instance activation and awaited shutdown. `AppRuntime` composes services and routes application actions. `MainWindow` owns navigation, activation, and layout. `CalendarViewModel` presents the selected date, moves it by whole weeks, and resolves signed week offsets from the current local date; `WeekLookupViewModel` resolves week numbers to date ranges, and `DateLookupViewModel` handles day-of-year and Julian-day conversions. Date views follow today until a date is selected or navigated; Today and a zero-week offset resume automatic date changes. `SettingsEditor` keeps preference edits in a draft until Save. The automatic-update toggle on About saves immediately while preserving other draft edits. Stable choice objects preserve selections when the display language changes.

## Calendar and tray

`LanguageCatalog` owns supported language identifiers, native labels, formatting cultures, and compiled translation tables. Settings validation, language choices, Windows-language resolution, and review captures share this catalog. `Strings` keeps the existing WPF binding interface and formats complete week-number phrases. Display language does not change the culture used for calendar calculations.

`WeekCalculator` accepts a date, explicit calendar options, and regional culture. ISO mode and Gregorian Monday/FirstFourDayWeek use .NET `ISOWeek`; other combinations use the calendar selected in the culture's `DateTimeFormat`. Reverse lookup reuses the same calculation in a bounded pass over the requested year and runs only when requested. `WeekTracker` compares week-start dates, including the year, to deduplicate notifications. A dispatcher timer targets local midnight; time, resume, display, and preference events are coalesced into a pending refresh.

`NativeTray` owns the hidden native window, notification identity, tray menu, and SafeHandle-backed icon. Rendering depends on week number, appearance, and icon pixel size. Explorer recreation republishes the icon. Silent notifications use the native per-notification flag without changing Windows sound preferences.

Dates outside the regional calendar's supported range show an unavailable week (—) in the tray while midnight scheduling remains active. The next valid refresh restores the week and resets the notification baseline. Icons support weeks 1–56, including longer lunisolar leap years.

The executable declares PerMonitorV2 DPI awareness. Window placement respects monitor work areas. Display changes, activation, and tray reopening trigger recovery checks; a stale window DPI is handled through a native move and bounds restoration so Windows can deliver its DPI-change message.

`WindowWorkAreaPlacement` retains the requested logical size while hidden and across display changes. Only an interactive user resize updates that preference; native bounds changes do not. Recovery fits the retained size to the current work area.

Week-reference copying uses raw selected-date and lookup-range data with a pure formatter. Short and localized references follow the active week convention and display language. ISO references recalculate the selected date, or each lookup range’s first day, using ISO rules; repeated ISO references are deduplicated. Both split-copy controls use Localized for the primary action and expose the three formats in a menu. Clipboard writes pass through an injectable platform service and persist after the application exits. Input edits or settings changes clear stale lookup results. Copy success and failure use the existing status area; copy-format choices are not saved.

Week navigation changes the selected Gregorian date by exactly seven days and is disabled at the representable date boundaries. The offset input accepts a signed whole number, anchors each calculation to the current local date, and rejects results outside years 1–9999 without changing the selection. Offset input and errors are transient UI state and are not persisted.

## Settings and diagnostics

`SettingsStore` validates bounded JSON before replacing current state and writes through a unique temporary file followed by atomic promotion. Installed data lives in `%LOCALAPPDATA%\Voltura\WeekNumber`; portable data lives in `Data` beside the executable. Autostart registration uses an application-owned command under the current user's registry and rolls back if settings persistence fails.

Optional application logging uses a bounded channel, one writer, and two rotating files of approximately 1 MiB each. See [Privacy](../PRIVACY.md) for stored data and network behavior, and [Validation](validation.md) for isolated diagnostic modes.

## Updates and installation

`UpdateService` owns HTTP, the pending update files, and an immutable UI state. One operation gate prevents overlapping checks and installer launches. Automatic checks run two minutes after startup and then daily while the app is running, automatic updates are enabled, and no update is ready to install. Manual checks use the same operation. Update eligibility requires the running path to match the per-user uninstall registration. Portable, development, and isolated profiles open the release downloads page instead of using the in-app updater.

Signature verification establishes authenticity; the service separately decides whether a version is newer. Startup restores a verified newer package or discards obsolete, incomplete, or damaged cache files. Cache cleanup is best effort and never claims that the app is up to date: only a successful online check does that. Replacing a package removes its old manifest first and publishes the new manifest last. Installation reverifies the package and holds the operation gate until setup exits. The UI receives its status and install action together; failures identify checking, downloading, verification, or launching setup. The optional application log records the failed operation and exception type.

A pinned RSA public key verifies signed manifest bytes. The selected installer must match the signed version, package variant, filename, size, and SHA-256. Downloads and redirects are restricted to allowed HTTPS origins; reads are bounded. The package is verified again before launch, and installation requires a user action.

NSIS extracts a package and invokes `installer/maintain.ps1`. Maintenance rejects unowned targets and reparse points, validates the payload, stages replacements, and journals backup, promotion, health checks, and registration. Failed installations restore the prior installation where possible until the installation is committed. Commitment is recorded before backup deletion; interrupted backup cleanup preserves the healthy installation and finishes deletion. Interrupted removal finishes deletion rather than restoring a partially removed application; a recovery uninstaller remains registered while deletion is pending.

`scripts/package.ps1` produces standard and self-contained installers and a self-contained portable ZIP. Self-contained packages include runtime license and notice files. Release manifests use RSA-PSS signatures, separately from Windows Authenticode publisher signing. Private signing keys and passphrases must remain outside the repository.
