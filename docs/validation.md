# Validation

Run commands from the repository root on Windows. See [Contributing](../CONTRIBUTING.md) for prerequisites.

## Build and automated checks

```powershell
./scripts/build.ps1
```

This builds Release with locked dependencies and runs the Microsoft.Testing.Platform xUnit suite. Tests cover calendar boundaries and rules, date conversions, midnight scheduling, notification deduplication, settings persistence and language import/export, Windows-language detection (including Chinese scripts and regions), translation completeness, localized runtime and tray states, minimum-width button labels, icon frames, UI state, monitor placement, and signed update handling with simulated HTTP responses.

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

Use a separate profile for review so normal settings are unaffected. Diagnostic modes require `--isolated-test-mode`; supply a path immediately after each of `--isolated-test-mode`, `--render-review`, and `--measure-idle` when used.

```powershell
$reviewRoot = Join-Path (Get-Location) 'artifacts/ui-review'
./apps/windows/bin/Release/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode "$reviewRoot/profile" --render-review "$reviewRoot/images"
```

The mode exits after capturing the actual WPF UI and icon sheet, plus week number, date lookup, Preferences, About, and color-picker views in all 21 languages and light/dark themes. Each main page is captured at normal and minimum window widths after layout animations finish. It also writes `window-dpi.json`. Inspect images for clipping, text contrast, date entry, focus states, and consistent control spacing. The README screenshot is stored in `docs/images/voltura-weeknumber.png`; refresh it from an English `window-light.png` capture when the main page changes.

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

- **Calendar and appearance:** test date entry and invalid input, year boundaries, each calendar mode, all 21 language options, light/dark/high-contrast appearance, keyboard navigation, custom icon transparency, and export/import dialogs.
- **Tray and notifications:** restart Explorer; reopen the window; activate a second instance; test notification clicks, Exit, startup/new-week notifications, sound settings, and Windows quiet mode. Resume across midnight and change the clock or time zone.
- **Displays:** move the window and taskbar between different DPI displays, change the primary monitor, disconnect/reconnect a display, and power-cycle relevant TV/receiver hardware. Check scale, icon sharpness, visible placement, and manual window sizing.
- **Installation:** use a clean Windows Sandbox or VM for both installer wizards, missing-runtime download, UAC cancellation, offline failure, reboot-required outcomes, autostart, upgrade, and uninstall with and without settings removal. Check the portable ZIP separately.
- **Updates:** test a real installed-version upgrade with authorized signed release metadata, both package variants, failure/retry behavior, and settings retention. After upgrading, restart and open About before checking online: the already-installed package must not produce an error or an install action. Then check online and confirm the latest-version message. Cancel setup and confirm it can be reopened. Confirm that portable copies use manual updates.

Generated output belongs under ignored `artifacts/`; retain only intentional documentation images in Git. A passing automated suite does not establish hardware or operating-system acceptance.
