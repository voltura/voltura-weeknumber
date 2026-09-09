using System.Reflection;
using System.Windows.Threading;
using VolturaWeekNumber.Ui;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class RuntimeActionTests(WpfTestFixture fixture)
{
    [Theory]
    [InlineData("foreground")]
    [InlineData("background")]
    public async Task IncompleteColorShowsValidationInsteadOfEscapingTheDispatcher(string action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        var completed = new TaskCompletionSource<string>(
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
                runtime.Model.Editor.Foreground = "#";
                runtime.Model.Editor.Background = "#";
                runtime.Model.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CalendarViewModel.Status))
                    {
                        completed.TrySetResult(runtime.Model.Status);
                    }
                };
                typeof(AppRuntime)
                    .GetMethod("ActionRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, [action]);
            });
            var status = await completed.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken
            );
            Assert.StartsWith(Strings.Current["Invalid"], status, StringComparison.Ordinal);
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
