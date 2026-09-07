# Privacy

Voltura WeekNumber is a desktop calendar utility from Voltura AB. It requires no account. Calendar and date conversions run on your computer; the app does not upload selected dates, settings, or diagnostic logs, and has no usage analytics or automatic crash-reporting service.

## Local data

- **Installed app:** `%LOCALAPPDATA%\Voltura\WeekNumber` holds settings, optional application logs, setup diagnostics, and downloaded update data.
- **Portable app:** the `Data` folder beside `VolturaWeekNumber.exe` holds settings and optional application logs.
- **Settings:** `settings.json` stores language, appearance, calendar rules, notification choices, autostart preference, logging, and automatic-update preference.
- **Optional logging:** disabled by default. `application.log` and its rotated `.1` file contain operational events and error types, with rotation at approximately 1 MiB. Explicit display diagnostics also record display geometry and DPI information.
- **Setup diagnostics:** `setup.log` records installer output and is replaced on each installation attempt. Diagnostic output may contain local paths.
- **Exports:** settings backups and exported icons are written to locations you choose.

Starting with Windows creates an application-specific entry in the current user's Windows registry. The installed app also has uninstall registration and a Start menu shortcut.

## Network requests

Automatic updates are enabled by default for installed copies and can be disabled in Preferences. Update checks use the GitHub API; update metadata, signatures, and installers are downloaded from GitHub release infrastructure over HTTPS. Requests include an application user-agent, and the server receives normal network information such as your IP address and the requested resource. Settings and calendar lookups are not attached to these requests. You choose when to install the downloaded update.

The standard installer may contact Microsoft to download the .NET Desktop Runtime. The offline installer and portable ZIP include the runtime. Portable copies do not use the in-app updater.

Opening project, release, or donation links opens the relevant website in your browser. GitHub, Microsoft, Ko-fi, and PayPal handle requests under their own privacy policies. Repository pages may display externally hosted Shields.io badges; those requests belong to viewing the page, not running the desktop app.

## Removing data and sharing diagnostics

The uninstaller offers settings removal. If you keep settings during uninstall, the local data folder remains. To remove retained data manually, exit the app and delete `%LOCALAPPDATA%\Voltura\WeekNumber`. For a portable copy, delete its `Data` folder or the extracted application folder. Exported backups and icons remain wherever you saved them.

Review logs and screenshots before attaching them to a public issue; remove private paths, usernames, and unrelated personal information. Privacy questions can be raised through the [issue tracker](https://github.com/voltura/voltura-weeknumber/issues) without private details. Report vulnerabilities using [SECURITY.md](SECURITY.md).
