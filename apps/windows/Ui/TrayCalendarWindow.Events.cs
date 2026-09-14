using System.Windows;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Calendar;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Ui;

public partial class TrayCalendarWindow
{
    private ImportedCalendarStore? _importStore;
    private CalendarEventsWindow? _eventsWindow;
    private CancellationTokenSource? _eventQuery;
    private IReadOnlyList<ImportedOccurrence> _occurrences = [];
    private readonly List<Task> _eventQueries = [];

    internal void SetImportStore(ImportedCalendarStore store)
    {
        if (_importStore is not null)
        {
            _importStore.Changed -= ImportsChanged;
        }

        _importStore = store;
        SetValue(CalendarImportActions.StoreProperty, store);
        store.Changed += ImportsChanged;
    }

    private void ImportsChanged() => _ = Dispatcher.InvokeAsync(RefreshEvents);

    internal Task EventRefresh { get; private set; } = Task.CompletedTask;
    internal void RefreshEvents()
    {
        _eventQueries.RemoveAll(task => task.IsCompleted);
        EventRefresh = RefreshEventsAsync();
        _eventQueries.Add(EventRefresh);
    }
    internal Task DrainEventQueriesAsync() => Task.WhenAll(_eventQueries);

    private async Task RefreshEventsAsync()
    {
        _eventQuery?.Cancel();

        if (_exit || !IsVisible || !Model.IsMonth || _importStore is null || _importStore.IsStopping)
        {
            return;
        }

        var query = new CancellationTokenSource();

        _eventQuery = query;

        var days = Model.Month.Weeks.SelectMany(week => week.Days).Where(day => day.Date is not null).ToArray();

        if (days.Length == 0)
        {
            query.Dispose();
            _eventQuery = null;

            return;
        }

        try
        {
            var occurrences = await _importStore.QueryAsync(days.Min(day => day.Date!.Value), days.Max(day => day.Date!.Value), query.Token);

            if (query.IsCancellationRequested || !ReferenceEquals(_eventQuery, query) || !IsVisible)
            {
                return;
            }

            _occurrences = occurrences;

            foreach (var day in days)
            {
                day.SetEventCount(occurrences.Count(item => item.Includes(day.Date!.Value)));
            }

            EventError.Text = "";
            EventError.Visibility = Visibility.Collapsed;

            if (_eventsWindow is { IsVisible: true, Date: { } date })
            {
                ShowEventDate(date);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (CalendarImportActions.IsImportError(error))
        {
            if (query.IsCancellationRequested || !ReferenceEquals(_eventQuery, query))
            {
                return;
            }

            _occurrences = [];

            foreach (var day in days)
            {
                day.SetEventCount(0);
            }

            _eventsWindow?.Hide();
            EventError.Text = CalendarImportActions.ErrorText(error);
            EventError.Visibility = Visibility.Visible;
        }
        finally
        {
            if (ReferenceEquals(_eventQuery, query))
            {
                _eventQuery = null;
            }

            query.Dispose();
        }
    }

    internal void SelectEventDate(DateOnly? date)
    {
        SelectedDate = date;

        if (date is null || _eventsWindow is { IsVisible: true } && _eventsWindow.Date == date)
        {
            _eventsWindow?.Hide();

            return;
        }

        ShowEventDate(date.Value);
    }

    private void ShowEventDate(DateOnly date)
    {
        var entries = _occurrences.Where(item => item.Includes(date)).ToArray();

        if (entries.Length == 0)
        {
            _eventsWindow?.Hide();

            return;
        }

        using var interaction = BeginCalendarInteraction();

        _eventsWindow ??= new CalendarEventsWindow(this);
        _eventsWindow.SetValue(CalendarImportActions.StoreProperty, _importStore);
        _eventsWindow.Display(date, entries);
        TrayCalendarPlacement.PlaceEvents(_eventsWindow, this);
        _eventsWindow.ShowDetails();
        TrayCalendarPlacement.PlaceEvents(_eventsWindow, this);
    }

    internal void QueueGroupDismissal() => _ = Dispatcher.InvokeAsync(() =>
    {
        if (!IsActive && !OwnedWindows.Cast<Window>().Any(window => window.IsActive))
        {
            DismissOnDeactivate();
        }
    }, DispatcherPriority.Background);

    private async void ImportClick(object sender, RoutedEventArgs args) => await CalendarImportActions.ImportAsync(this);
    private async void ManageCalendarsClick(object sender, RoutedEventArgs args) => await CalendarImportActions.ManageAsync(this);

    private void ReleaseEvents()
    {
        _eventQuery?.Cancel();

        if (_importStore is not null)
        {
            _importStore.Changed -= ImportsChanged;
        }

        _eventsWindow?.Exit();
    }
}
