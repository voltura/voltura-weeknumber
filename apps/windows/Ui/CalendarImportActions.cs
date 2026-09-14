using System.Windows;
using VolturaWeekNumber.Features.Calendar;

namespace VolturaWeekNumber.Ui;

internal static class CalendarImportActions
{
    internal static readonly DependencyProperty StoreProperty = DependencyProperty.RegisterAttached(
        "Store", typeof(ImportedCalendarStore), typeof(CalendarImportActions), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));
    internal static ImportedCalendarStore? Store(DependencyObject owner) => (ImportedCalendarStore?)owner.GetValue(StoreProperty);

    internal static async Task ImportAsync(Window owner, Guid? replace = null, ICalendarImportDialog? dialog = null)
    {
        using var interaction = Group(owner)?.BeginCalendarInteraction();

        dialog ??= new CalendarImportDialog();

        var store = Store(owner);

        if (store is null || store.IsStopping)
        {
            return;
        }

        try
        {
            if (dialog.ChoosePath(owner) is { } path)
            {
                using (store.BeginOperation())
                {
                    if (CalendarUrlImport.TryGetUri(path, out var uri))
                    {
                        var cursor = owner.Cursor;

                        owner.Cursor = System.Windows.Input.Cursors.Wait;

                        try
                        {
                            var download = await CalendarUrlImport.DownloadAsync(uri!, token: store.ShutdownToken);

                            await store.ImportContentAsync(download.Content, download.FileName, replace);
                        }
                        finally
                        {
                            owner.Cursor = cursor;
                        }
                    }
                    else
                    {
                        await store.ImportAsync(path, replace);
                    }
                }

                if (!store.IsStopping)
                {
                    dialog.ShowSuccess(owner, Strings.Current["CalendarImportSuccessful"]);
                }
            }
        }
        catch (Exception error) when (IsImportError(error))
        {
            if (!store.IsStopping)
            {
                dialog.ShowError(owner, ErrorText(error));
            }
        }
    }

    internal static async Task ManageAsync(Window owner, Guid? source = null)
    {
        using var interaction = Group(owner)?.BeginCalendarInteraction();

        if (Store(owner) is not { } store || store.IsStopping)
        {
            return;
        }

        try
        {
            using var operation = store.BeginOperation();

            await store.LoadAsync();

            if (store.IsStopping)
            {
                return;
            }

            var managerOwner = Group(owner) as Window ?? owner;
            var manager = managerOwner.OwnedWindows.OfType<ImportedCalendarsWindow>().FirstOrDefault();

            if (manager is null)
            {
                manager = new ImportedCalendarsWindow(store, source) { Owner = managerOwner, Topmost = managerOwner.Topmost };

                manager.SetValue(StoreProperty, store);
            }

            manager.Reload(source);
            manager.Show();
            manager.Activate();
        }
        catch (Exception error) when (IsImportError(error))
        {
            if (!store.IsStopping)
            {
                ShowError(owner, error);
            }
        }
    }

    internal static TrayCalendarWindow? Group(Window? owner)
    {
        while (owner is not null)
        {
            if (owner is TrayCalendarWindow tray)
            {
                return tray;
            }

            owner = owner.Owner;
        }

        return null;
    }

    internal static bool IsImportError(Exception error) => error is InvalidDataException or IOException or UnauthorizedAccessException
        or System.Text.Json.JsonException or ArgumentException or System.Security.SecurityException
        or System.Net.Http.HttpRequestException or OperationCanceledException;

    internal static string ErrorText(Exception error) => Strings.Current[error is System.Net.Http.HttpRequestException or OperationCanceledException
        ? "CalendarDownloadFailed"
        : error is InvalidDataException
        && error.Message is "CalendarImportInvalid" or "CalendarImportLimit" or "CalendarAlreadyImported"
            ? error.Message
            : "CalendarImportFailed"];

    internal static void ShowError(Window owner, Exception error) => CalendarMessageWindow.ShowMessage(owner, ErrorText(error));
}
