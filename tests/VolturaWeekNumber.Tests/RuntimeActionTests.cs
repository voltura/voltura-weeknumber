using System.Reflection;
using System.Windows.Threading;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class RuntimeActionTests(WpfTestFixture fixture)
{
    [Theory]
    [InlineData("foreground", "#FFFFFFFF")]
    [InlineData("background", "#FF151B26")]
    public async Task IncompleteColorStartsPickerFromDefaultWithoutEscapingTheDispatcher(
        string action,
        string expectedColor
    )
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        DispatcherUnhandledExceptionEventHandler unhandled = (_, args) =>
        {
            args.Handled = true;
            completed.TrySetException(args.Exception);
        };

        try
        {
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                runtime.Window.Dispatcher.UnhandledException += unhandled;
                runtime.Window.Open(MainPage.Preferences);
                runtime.Model.Editor.Foreground = "#";
                runtime.Model.Editor.Background = "#";

                _ = runtime.Window.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var picker = Assert.Single(
                            runtime.Window.OwnedWindows.OfType<ColorPickerWindow>()
                        );

                        Assert.Equal(expectedColor, picker.SelectedColor);
                        picker.Close();
                        completed.TrySetResult(true);
                    }
                    catch (Exception error)
                    {
                        foreach (var picker in runtime.Window.OwnedWindows.OfType<ColorPickerWindow>())
                        {
                            picker.Close();
                        }

                        completed.TrySetException(error);
                    }
                }, DispatcherPriority.ApplicationIdle);

                typeof(AppRuntime)
                    .GetMethod("ActionRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [action]);
            });

            await completed.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken
            );

            fixture.Run(() => Assert.Equal(string.Empty, runtime!.Model.Status));
        }
        finally
        {
            Task cleanup = Task.CompletedTask;

            fixture.Run(() =>
            {
                if (runtime is not null)
                {
                    runtime.Window.Dispatcher.UnhandledException -= unhandled;
                    cleanup = runtime.DisposeAsync().AsTask();
                }
            });
            await cleanup;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
