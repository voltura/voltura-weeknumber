using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using VolturaWeekNumber.Features.Settings;
using VolturaWeekNumber.Features.Updates;
using Xunit;
using CheckBox = System.Windows.Controls.CheckBox;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class RuntimeUpdateActionTests(WpfTestFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticUpdateToggleWaitsForManualCheckThenSaves(bool checkFails)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "VolturaWeekNumber-tests",
            Guid.NewGuid().ToString("N")
        );
        AppRuntime? runtime = null;
        CheckBox? checkbox = null;
        SemaphoreSlim? actions = null;
        using var handler = new DelayedReleaseHandler(checkFails);
        var token = TestContext.Current.CancellationToken;
        try
        {
            Task start = Task.CompletedTask;
            fixture.Run(() =>
            {
                runtime = new AppRuntime(new(root, false, true));
                start = runtime.StartAsync(true);
            });
            await start;
            fixture.Run(() =>
            {
                // Keep the normal profile and registry untouched while exercising the
                // real runtime action and checkbox with a controlled update response.
                var updates = Field<UpdateService>(runtime!, "_updates");
                Field<HttpClient>(updates, "_http").Dispose();
                typeof(UpdateService)
                    .GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(updates, new HttpClient(handler));
                typeof(UpdateService)
                    .GetField(
                        "<Eligible>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                    .SetValue(updates, true);
                actions = Field<SemaphoreSlim>(runtime!, "_actions");
                runtime!.Window.Open(MainPage.About);
                runtime.Window.UpdateState(new(UpdateStatus.Idle), true);
                checkbox = (CheckBox)runtime.Window.FindName("AutomaticUpdateCheck");
                Assert.True(checkbox.IsEnabled);
                typeof(AppRuntime)
                    .GetMethod("ActionRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(runtime, ["check-update"]);
                // Disable synchronously, before the updater publishes its busy state.
                Assert.False(checkbox.IsEnabled);
            });
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            fixture.Run(() =>
            {
                var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(checkbox!);
                Assert.Throws<ElementNotEnabledException>(() => toggle.Toggle());
                Assert.True(checkbox!.IsChecked);
                Assert.True(runtime!.Model.Editor.AutomaticUpdates);
            });
            handler.Release.TrySetResult();
            Assert.True(await actions!.WaitAsync(TimeSpan.FromSeconds(10), token));
            actions.Release();
            fixture.Run(() =>
            {
                Assert.Equal(
                    checkFails ? UpdateStatus.UpdateCheckFailed : UpdateStatus.Current,
                    Field<UpdateService>(runtime!, "_updates").State.Status
                );
                Assert.True(checkbox!.IsEnabled);
                ((IToggleProvider)new ToggleButtonAutomationPeer(checkbox)).Toggle();
            });
            Assert.True(await actions.WaitAsync(TimeSpan.FromSeconds(10), token));
            actions.Release();
            fixture.Run(() =>
            {
                Assert.True(checkbox!.IsEnabled);
                Assert.False(checkbox.IsChecked);
                Assert.False(runtime!.Model.Editor.AutomaticUpdates);
                Assert.False(runtime.Model.Editor.HasChanges);
            });
            Assert.False(
                (
                    await SettingsStore.ReadAsync(Path.Combine(root, "settings.json"))
                ).AutomaticUpdates
            );
        }
        finally
        {
            handler.Release.TrySetResult();
            Task cleanup = Task.CompletedTask;
            fixture.Run(() =>
            {
                if (runtime is not null)
                {
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

    private static T Field<T>(object instance, string name) =>
        (T)
            instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(instance)!;

    private sealed class DelayedReleaseHandler(bool fail) : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            var version = typeof(AppRuntime).Assembly.GetName().Version!.ToString(3);
            return new(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"tag_name":"v{{version}}","draft":false,"prerelease":false}"""
                ),
            };
        }
    }
}
