# Contributing

Thanks for helping improve Voltura WeekNumber. Useful contributions include reproducible bug reports, calendar correctness, translations, accessibility, UI polish, and documentation fixes.

## Before opening a pull request

1. Open an issue before making a large feature change.
2. Keep each pull request focused on one problem.
3. Describe the resulting behavior and update relevant documentation.
4. Run checks appropriate to the change and report what you verified. See the [validation guide](docs/validation.md).

Keep calendar rules independent of the UI language, preserve local settings, and keep tray behavior responsive. Read [architecture](docs/architecture.md) before changing service ownership, update handling, or installation.

## Build and run

Use Windows, the .NET 10 SDK selected by `global.json` (10.0.400 with patch roll-forward), and PowerShell 7.6. Installer packaging also requires NSIS available at the path used by `scripts/package.ps1`.

```powershell
git clone https://github.com/voltura/voltura-weeknumber.git
cd voltura-weeknumber
./scripts/build.ps1
dotnet run --project apps/windows/VolturaWeekNumber.csproj
```

The build script restores locked dependencies, builds Release, and runs the xUnit suite. Update and commit the relevant NuGet lock files when intentionally changing package dependencies.

```powershell
./scripts/package.ps1
./scripts/test-installer.ps1
```

Packages are written to `artifacts/publish`. Use the isolated review modes in the validation guide when testing UI or diagnostics without changing your normal profile. Keep generated artifacts, local settings, and signing credentials out of commits.

## License and community

Contributions are licensed under the project's [MIT License](LICENSE); no separate contributor license agreement is required. Project activity follows the [Code of Conduct](CODE_OF_CONDUCT.md).

Report vulnerabilities privately using [SECURITY.md](SECURITY.md), rather than including exploit details in public issues or pull requests.
