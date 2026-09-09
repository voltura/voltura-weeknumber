# Validation

Run commands from the repository root on Windows. See [Contributing](../CONTRIBUTING.md) for prerequisites.

## Build and automated checks

```powershell
./scripts/build.ps1
```

This builds Release with locked dependencies and runs the Microsoft.Testing.Platform xUnit suite. Tests cover calendar boundaries and rules, date conversions, midnight scheduling, notification deduplication, settings persistence, icon frames, UI state, monitor placement, and signed update handling with simulated HTTP responses.

For installer or packaging changes:

```powershell
./scripts/package.ps1
./scripts/test-installer.ps1
```

Packaging runs build and tests before creating both NSIS installers with warnings treated as errors and a self-contained portable ZIP in `artifacts/publish`. Installer tests exercise the maintenance helper in marked isolated directories and write `artifacts/installer-test-results.json`. They do not replace clean-machine testing of the actual installer and Windows integration.

`scripts/release.ps1 -KeyPath <private-key-path> -Publish` reads the signing passphrase from `VOLTURA_AIR_UPDATE_SIGNING_PASSPHRASE` in the current process environment. If it is unset, empty, or whitespace, it prompts securely. The passphrase and matching signing key are validated before building or packaging, and signing reuses the unlocked key without a second prompt. After changing a persistent Windows environment variable, open a new terminal so the release process inherits it. Standalone `scripts/sign-update.ps1` uses the same environment variable and prompt fallback.

Commit and push the version, release notes, and code before running with `-Publish`. The script checks for uncommitted changes and conflicting remote tags before packaging. GitHub creates `v<version>` at the checked release commit when publishing if the tag does not already exist; a matching existing tag is reused. You do not need to create or push tags manually. Run `scripts/verify.ps1` to include signing and release-script regression checks using temporary keys and simulated packaging/publication.

## UI captures

Use a separate profile for review so normal settings are unaffected. Diagnostic modes require `--isolated-test-mode`; supply a path immediately after each of `--isolated-test-mode`, `--render-review`, and `--measure-idle` when used.

```powershell
$reviewRoot = Join-Path (Get-Location) 'artifacts/ui-review'
./apps/windows/bin/Release/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode "$reviewRoot/profile" --render-review "$reviewRoot/images"
```

The mode exits after capturing the actual WPF UI, icon sheet, light/dark date lookups, English/Swedish/German pages, compact layouts, and color picker. It also writes `window-dpi.json`. Inspect images for clipping, text contrast, date entry, focus states, and consistent control spacing. The README screenshot is stored in `docs/images/voltura-weeknumber.png`; refresh it from an English `window-light.png` capture when the main page changes.

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

- **Calendar and appearance:** test date entry and invalid input, year boundaries, each calendar mode, all three languages, light/dark/high-contrast appearance, keyboard navigation, custom icon transparency, and export/import dialogs.
- **Tray and notifications:** restart Explorer; reopen the window; activate a second instance; test notification clicks, Exit, startup/new-week notifications, sound settings, and Windows quiet mode. Resume across midnight and change the clock or time zone.
- **Displays:** move the window and taskbar between different DPI displays, change the primary monitor, disconnect/reconnect a display, and power-cycle relevant TV/receiver hardware. Check scale, icon sharpness, visible placement, and manual window sizing.
- **Installation:** use a clean Windows Sandbox or VM for both installer wizards, missing-runtime download, UAC cancellation, offline failure, reboot-required outcomes, autostart, upgrade, and uninstall with and without settings removal. Check the portable ZIP separately.
- **Updates:** test a real installed-version upgrade with authorized signed release metadata, both package variants, failure/retry behavior, and settings retention. After upgrading, restart and open About before checking online: the already-installed package must not produce an error or an install action. Then check online and confirm the latest-version message. Cancel setup and confirm it can be reopened. Confirm that portable copies use manual updates.

Generated output belongs under ignored `artifacts/`; retain only intentional documentation images in Git. A passing automated suite does not establish hardware or operating-system acceptance.
