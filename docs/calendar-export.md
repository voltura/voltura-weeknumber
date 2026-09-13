# Calendar export

Use the export button immediately left of **Today** to save a local `.ics` file. The main calendar exports the displayed year, month, or week. The floating tray calendar exports the displayed year or month; its decade picker has no export button.

Right-click a month for **Export year** and **Export month**. Right-click a day or week row for those actions plus **Export week**. Year tiles in the floating decade picker offer **Export year**. Keyboard users can focus these items and press Shift+F10 or the Menu key. Menu labels identify the target period; opening a menu does not change the selected date or navigate.

Day actions use the clicked date, even when it belongs to an adjacent month. A week row's year/month actions use the month containing that row; its week action exports that particular week.

Each event is a single all-day **Week N** marker on the first day of its week. It does not mark you busy and has no reminder. The description includes the week date range and current week convention. Export follows the same ISO, regional, or custom rules as the app.

Month and year files include only week starts within that calendar month or year. For example, September 2026 under ISO rules exports September 7, 14, 21, and 28. Exporting the week containing September 1 instead creates one marker on August 31. Week numbers are calculated at the marker date, including at year boundaries.

Choose the destination in the normal Save dialog. Cancel leaves the calendar unchanged. Unsupported periods produce an error rather than an incomplete file. Failed writes preserve an existing destination file. The floating calendar stays open while its export menu or dialog is active, without changing its pin setting.

The file can be opened or imported using a calendar application's iCalendar support. Export does not connect to accounts or synchronize calendars. Import behavior, including handling of repeated imports, depends on the receiving application; stable event identifiers do not guarantee duplicate prevention.
