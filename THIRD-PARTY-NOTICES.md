# Third-party software notices

Voltura WeekNumber is distributed under the [MIT License](https://github.com/voltura/voltura-weeknumber/blob/master/LICENSE). Third-party components retain their own licenses and notices.

## Microsoft .NET runtime

Voltura WeekNumber uses .NET 10, WPF, and Windows Forms. The offline installer and portable ZIP bundle the Microsoft .NET and Windows Desktop runtimes. Their full license and notice texts are included in the distribution:

- `ThirdPartyNotices/Microsoft.NETCore.App/`
- `ThirdPartyNotices/Microsoft.WindowsDesktop.App/`

The runtime license identifies **Copyright (c) .NET Foundation and Contributors** and the MIT License. Bundled third-party notices identify additional runtime components and their terms. These distributed texts, copied from the exact runtime packages during packaging, are the authoritative notice set for that release.

The standard installer uses a separately installed Microsoft .NET Desktop Runtime and downloads Microsoft's runtime installer when needed.

## Application dependencies and build tools

The application ships Ical.Net 5.2.3 (MIT) for iCalendar parsing and recurrence, and its NodaTime 3.2.2 dependency (Apache-2.0) for time-zone handling. Their full licenses and required notices are included in `ThirdPartyNotices/` in the distribution and in [docs/licenses](docs/licenses) in the source repository:

- `Ical.Net-LICENSE.md` — copyright Douglas Day, Rian Stockbower, and the ical-org Project.
- `NodaTime-LICENSE.txt` and `NodaTime-NOTICE.txt` — includes attribution for code derived from Joda Time.

Test dependencies are listed in `tests/VolturaWeekNumber.Tests/VolturaWeekNumber.Tests.csproj` and its lock file; they are not shipped with the application. NSIS builds the installers and is not an application runtime dependency.

Voltura-owned source attribution is recorded in [NOTICE.md](NOTICE.md). Third-party authors and vendors do not endorse Voltura WeekNumber or Voltura AB. Their software remains subject to its own license and warranty terms.

Please report missing or incorrect attribution through the [issue tracker](https://github.com/voltura/voltura-weeknumber/issues).
