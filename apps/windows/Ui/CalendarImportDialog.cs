using System.Windows;

namespace VolturaWeekNumber.Ui;

internal interface ICalendarImportDialog
{
    string? ChoosePath(Window owner);
    void ShowError(Window owner, string message);
    void ShowSuccess(Window owner, string message);
}

internal sealed class CalendarImportDialog : ICalendarImportDialog
{
    public string? ChoosePath(Window owner)
    {
        var dialog = new CalendarImportSourceWindow { Owner = owner, Topmost = owner.Topmost };

        return dialog.ShowDialog() == true
            ? dialog.Source
            : null;
    }

    public void ShowError(Window owner, string message) => CalendarMessageWindow.ShowMessage(owner, message);
    public void ShowSuccess(Window owner, string message) => CalendarMessageWindow.ShowMessage(owner, message);
}
