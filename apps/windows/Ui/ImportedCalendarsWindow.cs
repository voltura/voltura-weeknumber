using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal sealed class ImportedCalendarsWindow : Window
{
    private readonly ImportedCalendarStore _store;
    private readonly StackPanel _list = new();
    private bool _busy;

    internal ImportedCalendarsWindow(ImportedCalendarStore store, Guid? selected)
    {
        _store = store;
        Title = Strings.Current["ImportedCalendars"].TrimEnd('…', '.');
        Width = 540;
        Height = 460;
        MinWidth = 400;
        MinHeight = 260;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "WindowBrush");

        var root = new DockPanel { Margin = new Thickness(16) };
        var import = ActionButton("ImportCalendar", async () => await CalendarImportActions.ImportAsync(this));

        import.HorizontalAlignment = HorizontalAlignment.Left;
        import.Margin = new Thickness(0, 0, 8, 12);
        DockPanel.SetDock(import, Dock.Top);
        root.Children.Add(import);
        root.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape && !_busy)
            {
                if (CalendarImportActions.Group(this) is { } tray)
                {
                    tray.Hide();
                }
                else
                {
                    Close();
                }

                args.Handled = true;
            }
        };

        Closing += (_, args) =>
        {
            args.Cancel = _busy && !_store.IsStopping;

            if (!args.Cancel && IsActive && CalendarImportActions.Group(this) is { IsVisible: true } tray)
            {
                tray.Activate();
            }
        };

        Deactivated += (_, _) => CalendarImportActions.Group(this)?.QueueGroupDismissal();
        _store.Changed += StoreChanged;
        Closed += (_, _) => _store.Changed -= StoreChanged;
        Reload(selected);
    }

    private Button ActionButton(string key, Func<Task> action)
    {
        var button = new Button { Content = Strings.Current[key], Width = double.NaN, Height = double.NaN, MinHeight = 40, MinWidth = 0, Margin = new Thickness(0, 0, 8, 0) };
        var content = new SpacingStackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var glyph = new TextBlock
        {
            Text = key switch
            {
                "ImportCalendar" => "\uE787",
                "ReplaceCalendar" => "\uE8EE",
                "RemoveCalendar" => "\uE74D",
                _ => string.Empty,
            },
            FontSize = 14,
        };

        glyph.SetResourceReference(StyleProperty, "ButtonGlyph");
        content.Children.Add(glyph);
        content.Children.Add(new TextBlock { Text = Strings.Current[key], VerticalAlignment = VerticalAlignment.Center });
        button.Content = content;
        System.Windows.Automation.AutomationProperties.SetName(button, Strings.Current[key]);

        button.Click += async (_, _) =>
        {
            if (_busy || _store.IsStopping)
            {
                return;
            }

            _busy = true;
            IsEnabled = false;

            using var interaction = CalendarImportActions.Group(this)?.BeginCalendarInteraction();

            try
            {
                await action();
            }
            catch (Exception error) when (CalendarImportActions.IsImportError(error))
            {
                if (!_store.IsStopping)
                {
                    CalendarImportActions.ShowError(this, error);
                }
            }
            finally
            {
                _busy = false;
                IsEnabled = true;

                if (!_store.IsStopping)
                {
                    Reload(null);
                }
            }
        };

        return button;
    }

    private void StoreChanged() => _ = Dispatcher.InvokeAsync(() =>
    {
        if (!_store.IsStopping)
        {
            Reload(null);
        }
    });

    internal void Reload(Guid? selected)
    {
        Title = Strings.Current["ImportedCalendars"].TrimEnd('…', '.');
        _list.Children.Clear();

        if (_store.Sources.Count == 0 && _store.FailedSources.Count == 0)
        {
            _list.Children.Add(new TextBlock { Text = Strings.Current["NoImportedCalendars"], TextWrapping = TextWrapping.Wrap });
        }

        foreach (var source in _store.Sources)
        {
            var panel = new StackPanel();

            panel.Children.Add(new TextBlock { Text = source.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock
            {
                Text = source.FileName + "\n" + source.ImportedAt.ToLocalTime().ToString("g", Strings.Current.Culture)
                    + " · " + string.Format(Strings.Current.Culture, Strings.Current["CalendarEventCount"], source.EventCount),
                Margin = new Thickness(0, 4, 0, 8),
                TextWrapping = TextWrapping.Wrap,
            });

            var actions = new StackPanel { Orientation = Orientation.Horizontal };

            actions.Children.Add(ActionButton("ReplaceCalendar", () => CalendarImportActions.ImportAsync(this, source.Id)));
            actions.Children.Add(ActionButton("RemoveCalendar", async () =>
            {
                if (CalendarMessageWindow.ConfirmRemoval(this,
                    string.Format(Strings.Current.Culture, Strings.Current["RemoveCalendarConfirm"], source.Name)))
                {
                    await _store.RemoveAsync(source.Id);
                }
            }));
            panel.Children.Add(actions);

            var border = new Border { Child = panel, Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };

            border.SetResourceReference(Border.BorderBrushProperty, source.Id == selected
                ? "AccentBrush"
                : "BorderBrush");
            _list.Children.Add(border);
        }

        foreach (var failure in _store.FailedSources)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            panel.Children.Add(new TextBlock { Text = failure.FileName, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = Strings.Current[failure.Error], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
            panel.Children.Add(ActionButton("RemoveCalendar", async () =>
            {
                if (CalendarMessageWindow.ConfirmRemoval(this,
                    string.Format(Strings.Current.Culture, Strings.Current["RemoveCalendarConfirm"], failure.FileName)))
                {
                    await _store.RemoveFailedAsync(failure.FileName);
                }
            }));
            _list.Children.Add(panel);
        }
    }
}
