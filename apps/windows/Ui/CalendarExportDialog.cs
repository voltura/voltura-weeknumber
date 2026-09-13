using System.Windows;
using Microsoft.Win32;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal interface ICalendarExportDialog
{
    string? ChoosePath(Window owner, CalendarExportRequest request);
    void ShowError(Window owner, string message);
}

internal sealed class CalendarExportDialog : ICalendarExportDialog
{
    public string? ChoosePath(Window owner, CalendarExportRequest request)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "iCalendar (*.ics)|*.ics",
            DefaultExt = ".ics",
            AddExtension = true,
            FileName = request.FileName,
            Title = CalendarExportActions.Label(request),
            OverwritePrompt = true,
        };

        return dialog.ShowDialog(owner) == true
            ? dialog.FileName
            : null;
    }

    public void ShowError(Window owner, string message) => MessageBox.Show(owner, message,
        "Voltura WeekNumber", MessageBoxButton.OK, MessageBoxImage.Error);
}
