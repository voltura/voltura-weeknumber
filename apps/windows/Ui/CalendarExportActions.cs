using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VolturaWeekNumber.Features.Calendar;
using CalendarMode = VolturaWeekNumber.Features.Calendar.CalendarMode;

namespace VolturaWeekNumber.Ui;

public static class CalendarExportActions
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(string), typeof(CalendarExportActions), new PropertyMetadata(null, TargetChanged));
    public static void SetTarget(DependencyObject element, string value) => element.SetValue(TargetProperty, value);
    public static string GetTarget(DependencyObject element) => (string)element.GetValue(TargetProperty);

    private static void TargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            element.ContextMenu = new ContextMenu();
            element.ContextMenuOpening += OpenMenu;
        }
    }

    internal static string Label(CalendarExportRequest request)
    {
        var strings = Strings.Current;
        var period = request.Scope switch
        {
            CalendarExportScope.Year => request.Anchor.Year.ToString(CultureInfo.InvariantCulture),
            CalendarExportScope.Month => request.Anchor.ToString("Y", strings.Culture),
            _ => request.Anchor.ToString("d", strings.Culture),
        };

        return string.Format(strings.Culture, strings[$"Export{request.Scope}Format"], period);
    }

    internal static IReadOnlyList<CalendarExportRequest> Requests(string target, object? item,
        DateOnly month, bool decade)
    {
        var date = item switch
        {
            CalendarMonthItem value => value.Date,
            CalendarPickerItem value => value.Date,
            CalendarDayItem value => value.Date,
            CalendarWeekItem value => value.Week.Range.Start,
            _ => null,
        };

        if (date is not { } anchor)
        {
            return [];
        }

        var containing = target == "Week"
            ? month
            : anchor;
        var requests = new List<CalendarExportRequest> { new(CalendarExportScope.Year, containing) };

        if (target == "Picker" && decade)
        {
            return requests;
        }

        requests.Add(new(CalendarExportScope.Month, containing));

        if (target is "Day" or "Week")
        {
            requests.Add(new(CalendarExportScope.Week, anchor));
        }

        return requests;
    }

    private static void OpenMenu(object sender, ContextMenuEventArgs args)
    {
        args.Handled = true;
        OpenContextMenu((FrameworkElement)sender, args.CursorLeft < 0);
    }

    internal static void OpenContextMenu(FrameworkElement element, bool keyboard)
    {
        var owner = Window.GetWindow(element);
        var tray = owner as TrayCalendarWindow;
        var page = Ancestors(element).OfType<CalendarBrowserPage>().FirstOrDefault();
        var model = tray?.Model.Month ?? page?.DataContext as CalendarBrowserViewModel;

        if (owner is null || model is null)
        {
            return;
        }

        var month = Ancestors(element).OfType<FrameworkElement>()
            .Select(parent => parent.DataContext).OfType<CalendarMonthItem>().FirstOrDefault()?.Date ?? model.Anchor;
        var requests = Requests(GetTarget(element), element.DataContext, month,
            tray?.Model.View == TrayCalendarView.Decade);

        if (requests.Count == 0)
        {
            return;
        }

        var menu = element.ContextMenu!;

        menu.Items.Clear();

        foreach (var request in requests)
        {
            var captured = request;

            if (request.Scope == CalendarExportScope.Week)
            {
                var offset = ((int)request.Anchor.DayOfWeek - (int)WeekCalculator.FirstWeekday(model.Options, CultureInfo.CurrentCulture) + 7) % 7;

                captured = request with { Anchor = DateOnly.FromDayNumber(Math.Max(0, request.Anchor.DayNumber - offset)) };
            }

            var item = new MenuItem { Header = Label(captured) };

            item.Click += (_, _) => Export(owner, captured, model.Options);
            menu.Items.Add(item);
        }

        menu.PlacementTarget = element;
        menu.Placement = keyboard
            ? System.Windows.Controls.Primitives.PlacementMode.Bottom
            : System.Windows.Controls.Primitives.PlacementMode.MousePoint;

        var interaction = tray?.BeginExportInteraction();
        RoutedEventHandler? closed = null;

        closed = (_, _) =>
        {
            menu.Closed -= closed;
            // Menu closing precedes its item's Click; release after the owned save interaction.
            _ = element.Dispatcher.InvokeAsync(() => interaction?.Dispose());
        };

        menu.Closed += closed;
        menu.IsOpen = true;
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            yield return current;
        }
    }

    internal static CalendarExportRequest Normalize(CalendarExportRequest request, CalendarOptions options) =>
        request.Scope == CalendarExportScope.Week
            ? request with { Anchor = WeekCalculator.Calculate(request.Anchor, options, CultureInfo.CurrentCulture).WeekStart }
            : request;

    internal static void Export(Window owner, CalendarExportRequest request, CalendarOptions options,
        ICalendarExportDialog? dialog = null)
    {
        using var interaction = (owner as TrayCalendarWindow)?.BeginExportInteraction();
        var strings = Strings.Current;

        dialog ??= new CalendarExportDialog();

        try
        {
            var region = CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());

            request = Normalize(request, options);

            var content = CalendarExport.Generate(request, options, region, strings.Culture,
                strings["WeekNumberFormat"], strings[options.Mode switch
                {
                    CalendarMode.Iso => "Iso",
                    CalendarMode.Custom => "Custom",
                    _ => "Regional",
                }] + " · " + strings.Culture.DateTimeFormat.GetDayName(WeekCalculator.FirstWeekday(options, region))
                + " · " + strings[WeekCalculator.WeekRule(options, region) switch
                {
                    CalendarWeekRule.FirstDay => "FirstDayRule",
                    CalendarWeekRule.FirstFullWeek => "FirstFullRule",
                    _ => "FirstFourRule",
                }], DateTimeOffset.UtcNow);

            if (dialog.ChoosePath(owner, request) is { } path)
            {
                CalendarExport.Save(path, content);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            dialog.ShowError(owner, strings[error is ArgumentOutOfRangeException
                ? "CalendarUnavailable"
                : "CalendarExportFailed"]);
        }
    }
}
