# Validation

Run commands from the repository root on Windows. See [Contributing](../CONTRIBUTING.md) for prerequisites.

## Build and automated checks

```powershell
./scripts/build.ps1
```

This builds Release with locked dependencies and runs the Microsoft.Testing.Platform xUnit suite. Tests cover calendar boundaries and rules, date-span and date conversions, midnight scheduling, notification deduplication, settings persistence and language import/export, Windows-language detection (including Chinese scripts and regions), translation completeness, localized runtime and tray states, the always-on-top controls, minimum-width button labels, icon frames, UI state, monitor placement, and signed update handling with simulated HTTP responses.

For installer or packaging changes:

```powershell
./scripts/package.ps1
./scripts/test-installer.ps1
```

Packaging runs build and tests before creating both NSIS installers with warnings treated as errors and a self-contained portable ZIP in `artifacts/publish`. Installer tests exercise the maintenance helper in marked isolated directories and write `artifacts/installer-test-results.json`. They do not replace clean-machine testing of the actual installer and Windows integration.

The test suite checks that the installer's language identifiers and native labels match the application catalog and that all four custom setup messages exist for every language. NSIS supplies 20 wizard translations; `installer/languages/Cantonese.nsh` and `Cantonese.nlf` supply written Cantonese. Keep the `.nlf` file encoded as UTF-8 with a BOM so NSIS reads its characters correctly. `/WX` rejects missing wizard strings during packaging. When changing installer languages, inspect the actual language picker and welcome/license pages in both variants, including Cantonese, Simplified/Traditional Chinese, Japanese, and Korean. Cancel before installation when reviewing on a development machine.

## Release a version

From `C:\Users\joaki\source\repos\voltura-weeknumber`, run:

```powershell
pwsh -NoProfile -File .\scripts\release.ps1
# Or select the version explicitly:
pwsh -NoProfile -File .\scripts\release.ps1 -Version 1.1.0
```

Publication is the default; the old `-Publish` switch has been removed. `-KeyPath` defaults to `WEEKNUMBER_KEYPATH` in the current process environment. You can override it with `-KeyPath 'C:\Users\joaki\newkey'`. Open a new terminal after setting a persistent Windows environment variable. Missing key configuration or a nonexistent key file stops the command before staging, committing, or pushing anything.

The command checks GitHub releases and reuses a version in `version.json` that is newer than the existing stable version numbers (including reserved draft versions). Otherwise it prompts for a stable `x.y.z` version. An explicit `-Version` must also be newer. GitHub authentication/network errors stop the release instead of being treated as an absent release.

Notes live in `docs/releases/<version>.md`. Existing notes with meaningful content are reused. Missing or empty notes are created with the release heading, then opened in a separate Notepad++ instance. Notepad++ is discovered through `PATH` or its standard Program Files/local installation locations. Save your notes and close that separate window to continue. Heading-only or unsaved notes stop the command without staging, committing, or pushing; the notes file remains for editing and retrying.

After updating `version.json`, the script validates the signing key, builds/tests, packages once, and signs. It then stages **all current tracked and non-ignored untracked changes**, commits them as `Release <version>` if needed, pushes the current branch to `origin`, verifies the pushed commit, and publishes the five release assets. Review your working tree before running: existing code and installer edits are included. Push failures do not trigger an automatic pull, rebase, or force-push.

GitHub creates `v<version>` at the checked release commit if the tag does not exist; a matching existing tag is reused and a conflicting tag stops publication. No manual tag creation is needed. If an attempt fails before publication, fix the reported error and rerun: the pending version/notes and any existing release commit are reused. If GitHub publication itself fails, inspect the release first because GitHub may have accepted part of the request. Published versions are never overwritten.

Add `-PrepareOnly` to prepare version/notes and signed artifacts locally without staging, committing, pushing, or publishing. This still queries GitHub to validate the version. Edited files and generated artifacts remain available locally.

Add `-NoTests` to skip test execution, including the UI tests: `pwsh -NoProfile -File .\scripts\release.ps1 -NoTests`. Packaging still builds the application and creates and signs all release artifacts. Tests run by default. This option also works with `-Version`, `-KeyPath`, and `-PrepareOnly`.

The signing passphrase still comes from `VOLTURA_AIR_UPDATE_SIGNING_PASSPHRASE`. If unset, empty, or whitespace, it prompts securely. The passphrase and matching key are validated before building or packaging; signing reuses the unlocked key without a second prompt. Standalone `scripts/sign-update.ps1` retains its explicit `-KeyPath` parameter and the same passphrase behavior. Keep private keys outside the repository.

Run `scripts/verify.ps1` to include signing and release-workflow regression checks with temporary keys and simulated editing, packaging, Git changes, and publication.

## UI captures

Calendar export tests cover scope resolution, boundary weeks, regional/custom rules, iCalendar encoding, safe file replacement, and owned tray interactions. Set `VOLTURA_CALENDAR_REVIEW` to an artifact directory when running the tests to also write week/month/year/Unicode `.ics` samples and calendar layout captures. Before claiming compatibility with a calendar client, open a sample in a separate test calendar and verify the dates, all-day markers, free/busy behavior, and reminder state. Repeated-import handling belongs to the receiving client.

Use a separate profile so normal settings are unaffected. The capture writes the main window, default- and minimum-width Date span views, and Calendar year, month, and week views plus tray month, year, and decade views in light and dark themes, an icon review sheet, the application ICO, and DPI metadata, then exits. Captures use the isolated profile's language and calendar rules.

```powershell
$reviewRoot = Join-Path (Get-Location) 'artifacts/ui-review'
./apps/windows/bin/Release/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode "$reviewRoot/profile" --render-review "$reviewRoot/images"
```

The README screenshot is stored in `docs/images/voltura-weeknumber.png`; refresh it from `window-light.png` when the main page changes.

## Display and idle diagnostics

For a monitor or TV/receiver reconnection check, launch with `--isolated-test-mode <absolute-profile-directory> --trace-dpi`. The profile's bounded `application.log` records display events, native and monitor DPI, WPF scale, bounds, and visibility without polling.

For a 90-second idle measurement:

```powershell
$reviewRoot = Join-Path (Get-Location) 'artifacts/ui-review'
./apps/windows/bin/Release/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode "$reviewRoot/idle-profile" --autostart --measure-idle "$reviewRoot/idle.json"
```

The report includes startup time, CPU, memory, handles, icon render count, and DPI awareness. This diagnostic launch does not register Windows autostart.

## Manual acceptance

Choose checks relevant to the changed behavior and record the build, environment, and observed result:

- **Calendar and appearance:** test date entry and invalid input, year boundaries, each calendar mode, all 21 language options, light/dark/high-contrast appearance, keyboard navigation, the always-on-top pin against other applications, custom icon transparency, and export/import dialogs.
- **Tray and notifications:** restart Explorer; reopen the window; activate a second instance; test notification clicks, Exit, startup/new-week notifications, sound settings, and Windows quiet mode. Resume across midnight and change the clock or time zone.
- **One-time tray placement:** in a disposable Windows 11 profile, compare visible application-icon coordinates with `UIOrderList` order, then verify that the helper moves only WeekNumber's identifier to the rightmost end. Check the attempt marker and confirm another launch preserves the user's subsequent order, including after a failed attempt. Saved-order verification does not prove visible relocation: allow Explorer to apply or discard it naturally, without restarting Explorer/Windows or requesting a restart for this check. Placement is disabled in isolated test/review mode; automated registry tests use temporary keys and injected failures.
- **Displays:** move the window and taskbar between different DPI displays, change the primary monitor, disconnect/reconnect a display, and power-cycle relevant TV/receiver hardware. Check scale, icon sharpness, visible placement, and manual window sizing.
- **Installation:** use a clean Windows Sandbox or VM for both installer wizards, missing-runtime download, UAC cancellation, offline failure, reboot-required outcomes, autostart, upgrade, and uninstall with and without settings removal. Check the portable ZIP separately.
- **Updates:** test a real installed-version upgrade with authorized signed release metadata, both package variants, failure/retry behavior, and settings retention. After upgrading, restart and open About before checking online: the already-installed package must not produce an error or an install action. Then check online and confirm the latest-version message. Cancel setup and confirm it can be reopened. Confirm that portable copies use manual updates.

Generated output belongs under ignored `artifacts/`; retain only intentional documentation images in Git. A passing automated suite does not establish hardware or operating-system acceptance.

### Tray calendar

#### Imported events

Import buttons in the tray calendar and main calendar browser open a themed chooser for a local `.ics` file or an HTTP/HTTPS calendar address. File selection uses the native picker. Web imports download a snapshot with a 30-second timeout, at most five redirects, and a 10 MiB limit on downloaded content, including decompressed or chunked responses. Event dots and details are tray-only. **Calendars…** in the tray and event form lists import sources and offers replacement and removal from either source type. Identical content is rejected as already imported; replacement is explicit and retains the source identity. Settings exports do not include calendar data.

Focused URL-import tests cover address validation, query preservation, Unicode content, duplicate imports, replacement, restart persistence, HTTP errors, cancellation, and declared/streamed size limits without opening windows. Manually check the source chooser in light/dark/high-contrast themes, keyboard file and URL selection, and failed URL imports from both calendar windows. URL-import work does not run automated UI reviews or restart the user's app.

Successful file imports, URL imports, and replacements show a localized confirmation using the themed calendar message window after saving. Cancellation and failed imports do not show success. Event details use pages of ten events to bound UI construction and text layout. Calendar persistence runs off the UI thread; shutdown cancels downloads and waits for active calendar operations before disposing the store or closing their owners. Event queries are serialized, with canceled queued requests skipped. Damaged saved sources are isolated from healthy calendars and remain listed with an error and a removal action in the calendar manager.

Recurrence validation also conservatively bounds the intermediate BY-part expansion to 10,000 candidates per interval and 1,000,000 estimated candidates across the checked intervals per source, including exception rules and negative BYSETPOS. Complex schedules may report the limit even when their final occurrence count is small.

Explicit recurrence-period endpoints are indexed once per event during each query, preserving the first matching endpoint without repeated scans. Retained calendar text has a combined 50 MiB UTF-16 limit, checked on import, replacement, and loading saved sources. Replacement excludes the old source from that budget. Saved sources exceeding the budget remain on disk and appear with the existing limit error in the calendar manager.

Automated import coverage includes recurrence exclusions/overrides/cancellations, time-zone and DST conversion, floating times, duration and all-day overlap, duplicate detection, failed replacement/write preservation, and restart persistence. Inputs are limited to 10 MiB and 20,000 event components per file, 100 sources, and 10,000 evaluated occurrences per range. Before enumeration, a conservative limit of 10,000 recurrence intervals per source bounds traversal from the original event start through the query end, including exception rules; old high-frequency schedules can therefore report the limit even when few events fall in the displayed month. Unknown time zones and unsupported recurrence ranges report an error; excessive evaluation never silently returns a partial calendar. Sources are stored atomically beneath the selected profile's `Calendars` directory.

Set `VOLTURA_CALENDAR_REVIEW` to an ignored artifact directory when running import UI tests for event-dot and detail captures. Check long titles/descriptions, multiple sources, all-day and midnight events, light/dark/high-contrast appearance, keyboard navigation, and localized labels. On real Explorer, verify unpinned focus transfers between both windows, owned dialogs and menus, outside dismissal, Escape while pinned, dragging, repeated tray clicks, and placement at both screen edges on mixed-DPI monitors. Native input and physical monitor checks remain manual acceptance gates.

Left-click the tray icon to toggle the flyout; outside click dismisses it unless pinned; Escape always dismisses it. Every opening starts at the current month. Click the heading to browse months, then decades. Up/down moves one month, one year, or one decade according to the current view. Today always returns to the current month. Muted months and years remain selectable and open their actual period.

Verify second-click dismissal without reopening, rapid clicks, keyboard tray activation, and the overflow tray. Check selectable next-year months and years before/after the decade, all calendar rules, midnight/resume refresh, and placement beside taskbars on mixed-DPI monitors. Confirm the main calendar keeps its independent browsing state. Render-review writes `tray-calendar-{month,year,decade}-{light,dark}.png`; captures and automated tests do not replace real Explorer input and display checks.

Clicking a day selects it with an outline while today keeps its filled highlight. The calendar pin is independent of the main window pin and remains set for the app session, including when the flyout is hidden and reopened. Verify pinned outside-click behavior, explicit dismissal, unpinning, and selection across navigation. Selected/pinned captures are saved as `tray-calendar-selected-{light,dark}.png`.
