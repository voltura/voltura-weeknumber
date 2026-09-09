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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UpdateCheckKeepsWindowResponsiveAndToggleWaitsThenSaves(bool checkFails, bool aboutEntry)
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
                // Open before enabling the test updater for manual-button scenarios.

                if (!aboutEntry)
                {
                    runtime!.Window.Open(MainPage.About);
                }

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
                runtime!.Window.UpdateState(new(UpdateStatus.Idle), true);
                checkbox = (CheckBox)runtime.Window.FindName("AutomaticUpdateCheck");
                Assert.True(checkbox.IsEnabled);

                if (aboutEntry)
                {
                    runtime.Window.Open(MainPage.About);
                }
                else
                {
                    RequestCheck(runtime);
                }

                // Disable synchronously, before the updater publishes its busy state.

                Assert.False(checkbox.IsEnabled);
            });
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            FlushUi();
            fixture.Run(() =>
            {
                Assert.Equal((int)MainPage.About,
                    ((System.Windows.Controls.TabControl)runtime!.Window.FindName("Tabs")).SelectedIndex);
                Assert.Equal(VolturaWeekNumber.Ui.Strings.Current["Checking"],
                    ((System.Windows.Controls.TextBlock)runtime!.Window.FindName("UpdateStatus")).Text);
                // The dispatcher still processes navigation while HTTP is held open.
                runtime.Window.Open(MainPage.WeekNumber);
                runtime.Window.Open(MainPage.About);
                RequestCheck(runtime);

                var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(checkbox!);

                Assert.Throws<ElementNotEnabledException>(() => toggle.Toggle());
                Assert.True(checkbox!.IsChecked);
                Assert.True(runtime!.Model.Editor.AutomaticUpdates);
            });
            // The background scheduler uses this same service entrypoint.
            await Field<UpdateService>(runtime!, "_updates").CheckAsync();
            Assert.Equal(1, handler.Requests);
            handler.Release.TrySetResult();
            Assert.True(await actions!.WaitAsync(TimeSpan.FromSeconds(10), token));
            actions.Release();
            FlushUi();
            fixture.Run(() =>
            {
                Assert.Equal(
                    checkFails
                        ? UpdateStatus.UpdateCheckFailed
                        : UpdateStatus.Current,
                    Field<UpdateService>(runtime!, "_updates").State.Status
                );
                Assert.True(checkbox!.IsEnabled);
                Assert.Equal(VolturaWeekNumber.Ui.Strings.Current[checkFails
                    ? "UpdateCheckFailed"
                    : "Current"],
                    ((System.Windows.Controls.TextBlock)runtime!.Window.FindName("UpdateStatus")).Text);
                runtime.Window.Open(MainPage.About);
                Assert.Equal(1, handler.Requests);
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
            fixture.Run(() =>
            {
                runtime!.Window.Open(MainPage.WeekNumber);
                runtime.Window.Open(MainPage.About);
            });
            Assert.Equal(1, handler.Requests);
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

    [Fact]
    public async Task AboutEntryDownloadsVerifiedUpdateAndReentryChecksAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
        using var key = System.Security.Cryptography.RSA.Create(2048);
        using var handler = new UpdateServiceTests.ReleaseHandler(key);

        handler.SetRelease(key, "99.0.0");

        AppRuntime? runtime = null;

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
                var updates = Field<UpdateService>(runtime!, "_updates");

                Field<HttpClient>(updates, "_http").Dispose();

                void Set(string name, object value) => typeof(UpdateService)
                    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(updates, value);
                Set("_http", new HttpClient(handler));
                Set("_publicKey", key.ExportSubjectPublicKeyInfoPem());
                Set("<Eligible>k__BackingField", true);
                Set("_full", false);
            });

            var actions = Field<SemaphoreSlim>(runtime!, "_actions");

            for (var visit = 1; visit <= 2; visit++)
            {
                fixture.Run(() =>
                {
                    runtime!.Window.Open(MainPage.WeekNumber);
                    runtime.Window.Open(MainPage.About);
                });
                Assert.True(await actions.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
                actions.Release();
                FlushUi();
                fixture.Run(() =>
                {
                    Assert.True(Field<UpdateService>(runtime!, "_updates").State.Ready);
                    Assert.Equal(VolturaWeekNumber.Ui.Strings.Current["Ready"],
                        ((System.Windows.Controls.TextBlock)runtime!.Window.FindName("UpdateStatus")).Text);
                    Assert.Equal(System.Windows.Visibility.Visible,
                        ((System.Windows.Controls.Button)runtime.Window.FindName("InstallButton")).Visibility);
                    runtime.Window.Open(MainPage.About);
                });
                Assert.Equal(visit, handler.Requests.Count(name => name == "latest"));
                Assert.Single(handler.Requests, name => name.EndsWith(".exe", StringComparison.Ordinal));
            }
        }
        finally
        {
            Task cleanup = Task.CompletedTask;

            fixture.Run(() => cleanup = runtime?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await cleanup;

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private void FlushUi() => fixture.Run(() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { }, System.Windows.Threading.DispatcherPriority.Background));

    private static void RequestCheck(AppRuntime runtime) =>
        typeof(AppRuntime)
            .GetMethod("ActionRequested", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(runtime, ["check-update"]);

    private static T Field<T>(object instance, string name) =>
        (T)
            instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(instance)!;

    private sealed class DelayedReleaseHandler(bool fail) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            var version = typeof(AppRuntime).Assembly.GetName().Version!.ToString(3);

            return new(fail
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"tag_name":"v{{version}}","draft":false,"prerelease":false}"""
                ),
            };
        }
    }
}
