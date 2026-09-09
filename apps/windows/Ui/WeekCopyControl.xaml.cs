using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

public partial class WeekCopyControl : System.Windows.Controls.UserControl
{
    internal event Action<WeekReferenceFormat>? CopyRequested;

    public WeekCopyControl() => InitializeComponent();

    private void CopyClick(object sender, RoutedEventArgs args) =>
        CopyRequested?.Invoke(WeekReferenceFormat.Localized);

    private void FormatsClick(object sender, RoutedEventArgs args)
    {
        FormatsButton.ContextMenu.PlacementTarget = FormatsButton;
        FormatsButton.ContextMenu.Placement = PlacementMode.Bottom;
        FormatsButton.ContextMenu.IsOpen = true;
    }

    private void FormatClick(object sender, RoutedEventArgs args) =>
        CopyRequested?.Invoke(Enum.Parse<WeekReferenceFormat>((string)((MenuItem)sender).Tag));
}
