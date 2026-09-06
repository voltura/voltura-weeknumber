# Validation

## Repeatable local gates

`scripts/build.ps1` runs the warning-free Release build with locked dependencies and the Microsoft.Testing.Platform xUnit suite. `scripts/package.ps1` builds both installers with NSIS `/WX`, plus the self-contained portable ZIP. `scripts/test-installer.ps1` exercises the real maintenance helper under a uniquely marked isolated directory and records its results in `artifacts/installer-test-results.json`; it does not change real installation, startup, or uninstall registry entries.

The unit suite covers calendar boundaries and rule combinations, DST midnight scheduling, notification deduplication, settings validation and atomic replacement, all week/icon frame combinations, stable settings-choice identities, work-area geometry/recovery, and signed update download/staging/recovery through a simulated HttpMessageHandler.

The application supports isolated review modes (a separate data directory is mandatory):

```powershell
./apps/windows/bin/Debug/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode C:\temp\weeknumber-review --render-review C:\temp\weeknumber-images
./apps/windows/bin/Debug/net10.0-windows/VolturaWeekNumber.exe --isolated-test-mode C:\temp\weeknumber-idle --autostart --measure-idle C:\temp\weeknumber-idle.json
```

Review mode generates the full icon sheet and actual WPF client-area renders in light/dark and compact localized layouts. It applies settings through the production settings path. The idle probe measures startup, CPU, working set, handles, render count, and effective native DPI awareness over 90 seconds.

The calendar icon direction was approved during implementation. The requested Today/Idag button correction uses one shared themed button style throughout the application.

All three local packages were built with NSIS `/WX` for the installers. The 16 isolated installer scenarios passed under both PowerShell 7.6.5 and Windows PowerShell 5.1, including NSIS manifest preparation for both variants, interrupted installation/removal, corrupt payload rejection, and rollback. The final portable executable passed its health check; the ZIP contains the portable marker and bundled runtime license notices. PowerShell script syntax checks passed. These results supplement the operating-system acceptance checks below.

Following the reported installer failure, the actual small NSIS installer completed a per-user installation with exit code 0. Its prerequisite, manifest preparation, and maintenance stages each returned 0; the installed executable passed its health check, and uninstall registration and the Start menu shortcut were verified. The failure was reproduced in Windows PowerShell 5.1: wrapping the `ConvertFrom-Json` pipeline in an array subexpression nested the manifest entries when appending the generated uninstaller. NSIS now calls a shared preparation script, which is exercised by the regression suite. Setup also selects Windows PowerShell's own module directory to avoid incompatible inherited PowerShell 7 modules. The bounded setup output is retained at `%LOCALAPPDATA%\Voltura\WeekNumber\setup.log` and replaced on each installation attempt.

## Manual Windows acceptance still required

Recorded local results: the Release suite passed 43 tests with zero build warnings. The executable manifest contains PerMonitorV2; both the native tray window and visible WPF window reported PerMonitorV2 at 216 DPI. A second launch exited successfully and activated the original WPF window. An external 90-second idle observation showed unchanged CPU time, handles settling from 486 to 480, and stable working set (about 148 MiB). The separate icon-render counter remained at one. In-process diagnostic sampling produced different handle counts, so the external observation is the idle resource baseline.

- On mixed-DPI displays, move the visible window and taskbar, change the primary display, disconnect/reconnect a display, and verify title-bar scale, sharp tray output, work-area recovery, and preservation of manual sizing.
- Restart Explorer and verify tray restoration; test keyboard-only tray opening, window reopening, notification clicks, and Exit.
- Exercise actual startup/new-week/silent notifications and Windows quiet-mode behavior. Resume across midnight/week boundaries and change the clock/time zone.
- In a clean Windows Sandbox or VM, test both actual NSIS wizards, missing-runtime download, UAC cancellation, offline failure, reboot-required outcomes, autostart, upgrade, and uninstall with/without settings removal. Isolated helper tests do not prove these operating-system interactions.
- Exercise high contrast, date-picker keyboard validation, custom transparent icon colors, and native Open/Save dialogs.
- Configure the production update key, rebuild, sign the metadata, and test an actual installed-version upgrade from the intended release channel before first public release.

The actual installer validation created the per-user installation, uninstall entry, and Start menu shortcut. No physical display topology, Windows notification policy, real user autostart, UAC prerequisite installation, or public release was changed.
