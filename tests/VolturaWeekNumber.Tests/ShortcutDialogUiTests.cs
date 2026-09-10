using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Platform;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class ShortcutDialogUiTests(WpfTestFixture fixture)
{
    [Fact]
    public void DialogShowsLiveTilesConflictAndDisposesItsTemporaryHook()
    {
        fixture.Run(() =>
        {
            Strings.Current.SetLanguage("en");

            var owner = new MainWindow(new CalendarViewModel());
            var hook = new FakeCaptureHook();
            var dialog = new ShortcutAssignmentWindow(
                ActivationTarget.WeekNumber,
                null,
                (_, candidate) => candidate?.VirtualKey != 0x58,
                callback =>
                {
                    hook.Callback = callback;

                    return hook;
                }
            );

            try
            {
                owner.Show();
                dialog.Owner = owner;
                dialog.Show();
                Idle(dialog);

                Assert.Same(owner, dialog.Owner);
                Assert.True(hook.Active);
                Assert.InRange(
                    Math.Abs((owner.Left + owner.ActualWidth / 2) - (dialog.Left + dialog.ActualWidth / 2)),
                    0,
                    2
                );

                Assert.True(hook.Callback!(0x11, true));
                Assert.True(hook.Callback(0x58, true));
                Idle(dialog);

                Assert.Equal(Visibility.Visible, Element<Border>(dialog, "ConflictBanner").Visibility);
                Assert.Equal(20, Element<Grid>(dialog, "ConflictIcon").Width);
                Assert.Equal(20, Element<Grid>(dialog, "ConflictIcon").Height);
                Assert.False(Element<Button>(dialog, "SaveButton").IsEnabled);
                Assert.Equal(2, Element<ItemsControl>(dialog, "KeyTiles").Items.Count);

                Assert.True(hook.Callback(0x58, false));
                Assert.True(hook.Callback(0x11, false));
                Assert.True(hook.Callback(0x11, true));
                Assert.True(hook.Callback(0x57, true));
                Idle(dialog);

                Assert.Equal(Visibility.Collapsed, Element<Border>(dialog, "ConflictBanner").Visibility);
                Assert.True(Element<Button>(dialog, "SaveButton").IsEnabled);

                Assert.True(hook.Callback(0x57, false));
                Assert.True(hook.Callback(0x11, false));
                Assert.True(hook.Callback(0x08, true));
                Idle(dialog);

                Assert.Equal(Visibility.Visible, Element<Border>(dialog, "InvalidBanner").Visibility);
                Assert.Equal(Visibility.Collapsed, Element<Border>(dialog, "ConflictBanner").Visibility);
                Assert.True(Assert.IsType<bool>(Element<ItemsControl>(dialog, "KeyTiles").Tag));
                Assert.False(Element<Button>(dialog, "SaveButton").IsEnabled);
                Assert.Single(Element<ItemsControl>(dialog, "KeyTiles").Items);
            }
            finally
            {
                dialog.Close();
                owner.Exit();
            }

            Assert.True(hook.IsDisposed);
        });
    }

    [Fact]
    public void ClearCanRemoveAnAssignmentAndResetRestoresTheOpenedValue()
    {
        fixture.Run(() =>
        {
            var initial = new ActivationShortcut(true, false, false, false, 0x43);
            var hook = new FakeCaptureHook();
            var dialog = new ShortcutAssignmentWindow(
                ActivationTarget.Calendar,
                initial,
                (_, _) => true,
                callback =>
                {
                    hook.Callback = callback;

                    return hook;
                }
            );

            try
            {
                dialog.Show();
                Idle(dialog);

                Element<Button>(dialog, "ClearButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Idle(dialog);
                Assert.True(Element<Button>(dialog, "SaveButton").IsEnabled);
                Assert.Empty(Element<ItemsControl>(dialog, "KeyTiles").Items);

                Element<Button>(dialog, "ResetButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Idle(dialog);
                Assert.False(Element<Button>(dialog, "SaveButton").IsEnabled);
                Assert.Equal(2, Element<ItemsControl>(dialog, "KeyTiles").Items.Count);
            }
            finally
            {
                dialog.Close();
            }

            Assert.True(hook.IsDisposed);
        });
    }

    [Fact]
    public void SavingReturnsTheTypedTargetAndDisposesTheHook()
    {
        fixture.Run(() =>
        {
            var hook = new FakeCaptureHook();
            var dialog = new ShortcutAssignmentWindow(
                ActivationTarget.Calendar,
                null,
                (_, _) => true,
                callback =>
                {
                    hook.Callback = callback;

                    return hook;
                }
            );

            dialog.Loaded += (_, _) => _ = dialog.Dispatcher.BeginInvoke(() =>
            {
                _ = hook.Callback!(0x12, true);
                _ = hook.Callback(0x43, true);
                Element<Button>(dialog, "SaveButton").RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent)
                );
            });

            Assert.True(dialog.ShowDialog());
            Assert.Equal(
                new(
                    ActivationTarget.Calendar,
                    new(false, false, true, false, 0x43)
                ),
                dialog.Result
            );
            Assert.True(hook.IsDisposed);
        });
    }

    [Fact]
    public void CaptureInitializationFailureClosesTheDialogWithoutEscapingTheLoadedCallback()
    {
        fixture.Run(() =>
        {
            var failure = new EntryPointNotFoundException("Missing native hook entry point.");
            var dialog = new ShortcutAssignmentWindow(
                ActivationTarget.WeekNumber,
                null,
                (_, _) => true,
                _ => throw failure
            );

            Assert.False(dialog.ShowDialog());
            Assert.Same(failure, dialog.CaptureFailure);
        });
    }

    [Fact]
    public void DialogOpensAndClosesWithTheRealNativeCaptureHook()
    {
        fixture.Run(() =>
        {
            var dialog = new ShortcutAssignmentWindow(
                ActivationTarget.WeekNumber,
                null,
                (_, _) => true
            );

            dialog.ContentRendered += (_, _) => _ = dialog.Dispatcher.BeginInvoke(dialog.Close);

            Assert.False(dialog.ShowDialog());
            Assert.Null(dialog.CaptureFailure);
        });
    }

    private static T Element<T>(FrameworkElement root, string name)
        where T : FrameworkElement => Assert.IsType<T>(root.FindName(name));

    private static void Idle(Window window) =>
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private sealed class FakeCaptureHook : IShortcutCaptureHook
    {
        internal Func<int, bool, bool>? Callback { get; set; }
        public bool Active { get; set; }
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }
}
