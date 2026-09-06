# Feature parity and carried-forward fixes

| Original behavior / lesson | Replacement | Verification |
|---|---|---|
| Current week in tray; date lookup | One shared calculator and WPF date picker | Calendar boundary tests and rendered window |
| Regional/custom first-day and week rule | Regional, ISO, all 21 custom combinations | Regional and custom-rule tests |
| Incorrect week-53 correction uses today's year | Date-specific ISO week and week-year | 2024-12-31, 2025-12-29, 2021-01-01, 2027-01-01 |
| Swedish tooltip / first-language fixes | Localized weekday/date formatting; separate UI and regional culture | Localized render review |
| Color selection and reset fixes | Validated ARGB settings and reset to automatic theme | Color validation and rendered icons |
| Icon export fix | One ICO encoder with twelve PNG frames | Decode every exported frame for weeks 01–53 |
| Blurry icon after monitor changes | Native tray DPI sizing, explicit manifest, display refresh | Live PMv2 probe; mixed-monitor manual gate |
| Lite's fewer redraws | Render-key comparison; midnight date updates | Idle render counter |
| Lite's single-instance and cleanup fixes | Per-profile mutex/event activation, SafeHandle icon ownership | Isolated runtime smoke; native measurements |
| Startup/new-week/silent notifications | Deduplicated week changes and native per-notification sound control | WeekTracker tests; Windows notification manual gate |
| Start with Windows | Quoted application-owned HKCU command | Isolated tests avoid registry writes; installed manual gate |
| Logging and settings backup | Bounded log, JSON export/import, atomic settings | Round-trip, rejection, and file preservation tests |
| Settings reset during update | Settings stored separately from installation | Maintenance tests preserve installation boundaries |
| Old offline/update failures | Async bounded requests; signed staging and retry | Simulated offline, malformed, corrupt, oversized, wrong-origin, and interrupted update tests |
| Windows taskbar-size toggle | Intentionally removed by agreement | No system-wide taskbar setting writes |
| Graphics tuning / resolution selection | Intentionally replaced by automatic size-specific rendering | All 12 frame sizes |

UI, menu, notification, and installer strings support English, Swedish, and German. Legacy XML migration, a background Windows service, web UI, and 32-bit binaries are outside this implementation.
