# Architecture

The WPF application and xUnit test assembly are the only .NET projects. Shared build metadata reads `version.json`. Application resources and platform helpers are local copies with attribution; no sibling repository is required.

`App` owns single-instance activation and awaited runtime shutdown. `AppRuntime` composes services and routes application actions. `MainWindow` owns navigation, window activation, and declarative composition. `CalendarViewModel` owns selected-date presentation; `SettingsEditor` owns a draft until Save. Stable choice objects keep WPF selections intact across localization changes.

The calendar calculator accepts a date, explicit options, and regional culture. ISO mode and Gregorian Monday/FirstFourDayWeek use .NET ISOWeek. Other combinations use the regional calendar. `WeekTracker` compares week-start dates, including the year, to deduplicate notifications. One dispatcher timer targets local midnight; time, resume, display, and preference events are coalesced into a single pending refresh.

`NativeTray` owns the hidden native window, Shell notification identity, tray menu, and SafeHandle-backed HICON. It renders only when week, appearance, or icon pixel size changes. Explorer recreation republishes the icon. Unicode native notifications use `NIIF_NOSOUND` without modifying Windows sound preferences. WPF Fluent controls are retained; the shared flat button primitive follows the Voltura Air style and includes keyboard focus states.

`SettingsStore` validates bounded JSON before changing current state and promotes a unique sibling temporary file atomically. Settings remain outside installed files. Optional logs use one bounded channel, one writer, and two files capped at about 1 MiB each. Registry autostart changes are scoped to the application's command and rolled back if settings persistence fails.

`UpdateService` owns its HttpClient, schedule, cancellation, gate, and installer-process reference. Installed eligibility is checked against the running path and per-user uninstall registration. Portable, development, and isolated profiles do not auto-update. A pinned RSA public key verifies exact manifest bytes; the selected installer must match the signed version, variant, name, size, and SHA-256. Download origins and redirects are allowlisted, reads are bounded, and partial metadata never becomes ready. The package is reverified before launch. Setup stops the installed process only after staging verification, so cancellation before that point leaves the app running.

NSIS extracts a package and invokes `maintain.ps1`. The helper rejects unowned targets and reparse points, validates each payload file, stages a replacement, journals backup/promotion/health/registration, and restores the prior installation on failure. Once uninstall begins deleting files, recovery finishes removal rather than restoring a partial directory. A recovery uninstaller remains registered while deletion is pending.

Production public-key configuration and authorized signing are prerequisites for release. No private key or passphrase belongs in this repository. Application updates use RSA-PSS manifests; this is separate from optional Windows Authenticode publisher signing.
