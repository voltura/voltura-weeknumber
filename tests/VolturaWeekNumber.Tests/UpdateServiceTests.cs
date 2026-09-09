using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using Xunit;

namespace VolturaWeekNumber.Tests;

[Collection("WPF")]
public sealed class UpdateServiceTests(WpfTestFixture fixture) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task LockedOverdueStampDoesNotSpinAndManualCheckCanRecover()
    {
        using var key = RSA.Create(2048);
        var handler = new ReleaseHandler(key);
        await using var service = new UpdateService(new(_root, false, true), handler, key.ExportSubjectPublicKeyInfoPem(), true, false);
        var updates = Path.Combine(_root, "Updates");
        Directory.CreateDirectory(updates);
        var stamp = Path.Combine(updates, "last-check.txt");
        await File.WriteAllTextAsync(stamp, "old", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(stamp, DateTime.UtcNow.AddDays(-2));
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failures = 0;
        service.Changed += () =>
        {
            if (service.Status != "UpdateFailed") return;
            Interlocked.Increment(ref failures);
            failed.TrySetResult();
        };
        using (File.Open(stamp, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fixture.Run(() => service.Start(true));
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            fixture.Run(() => { service.Start(false); service.Start(true); });
            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.Equal(1, Volatile.Read(ref failures));
            Assert.Empty(handler.Requests);
        }
        // The automatic retry pause must not delay a user-initiated check after the file is available.
        await service.CheckAsync();
        Assert.Equal("Ready", service.Status);
        Assert.True(service.Ready);
    }

    [Fact]
    public async Task RechecksReuseReadyInstallerButDownloadNewerOrMissingPackages()
    {
        using var key = RSA.Create(2048);
        var handler = new ReleaseHandler(key);
        await using var service = new UpdateService(new(_root, false, true), handler, key.ExportSubjectPublicKeyInfoPem(), true, false);
        await service.CheckAsync();
        var installer = Directory.GetFiles(Path.Combine(_root, "Updates", "pending"), "*.exe").Single();
        // Rechecking must neither read/hash nor overwrite an already verified installer.
        using (File.Open(installer, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await service.CheckAsync();
            Assert.Equal("Ready", service.Status);
        }
        Assert.Single(handler.Requests, name => name.EndsWith(".exe", StringComparison.Ordinal));
        File.Delete(installer);
        await service.CheckAsync();
        Assert.True(service.Ready);
        Assert.True(File.Exists(installer));
        Assert.Equal(2, handler.Requests.Count(name => name.EndsWith(".exe", StringComparison.Ordinal)));
        handler.SetRelease(key, "1.2.0");
        await service.CheckAsync();
        Assert.Equal("Ready", service.Status);
        Assert.EndsWith("1.2.0-win-x64.exe", Directory.GetFiles(Path.Combine(_root, "Updates", "pending"), "*.exe").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAndRestoreKeepIoAndVerificationOffDispatcher()
    {
        using var key = RSA.Create(2048);
        var handler = new ReleaseHandler(key);
        await using var service = new UpdateService(new(_root, false, true), handler, key.ExportSubjectPublicKeyInfoPem(), true, false);
        Task check = Task.CompletedTask;
        var dispatcherThread = 0;
        var callbackThreads = new List<int>();
        service.Changed += () => callbackThreads.Add(Environment.CurrentManagedThreadId);
        fixture.Run(() =>
        {
            dispatcherThread = Environment.CurrentManagedThreadId;
            check = service.CheckAsync();
        });
        await check;
        Assert.True(service.Ready);
        Assert.NotEmpty(callbackThreads);
        Assert.All(callbackThreads, thread => Assert.NotEqual(dispatcherThread, thread));
        Assert.All(handler.RequestThreads, thread => Assert.NotEqual(dispatcherThread, thread));
        callbackThreads.Clear();
        fixture.Run(() => check = service.RestorePendingAsync());
        await check;
        Assert.Equal("Ready", service.Status);
        Assert.Single(callbackThreads);
        Assert.NotEqual(dispatcherThread, callbackThreads[0]);
    }

    [Fact]
    public async Task ShutdownCancelsInFlightCheckAndConcurrentCheckDoesNotDuplicateRequest()
    {
        using var handler = new WaitingHandler();
        var service = new UpdateService(new(_root, false, true), handler, eligible: true);
        Task check = Task.CompletedTask;
        try
        {
            fixture.Run(() => check = service.CheckAsync());
            await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            await service.CheckAsync();
            Assert.Equal(1, handler.Requests);
        }
        finally
        {
            Task shutdown = Task.CompletedTask;
            fixture.Run(() => shutdown = service.DisposeAsync().AsTask());
            await shutdown.WaitAsync(TestContext.Current.CancellationToken);
        }
        await check;
        Assert.False(service.Ready);
    }
    [Fact]
    public async Task VerifiedDownloadRestoresAfterRestartAndCorruptContentIsRejected()
    {
        using var key = RSA.Create(2048);
        var paths = new AppPaths(_root, false, true);
        await using (var service = new UpdateService(paths, new ReleaseHandler(key), key.ExportSubjectPublicKeyInfoPem(), true, false))
        {
            await service.CheckAsync();
            Assert.True(service.Ready); Assert.Equal("Ready", service.Status);
        }
        await using var restored = new UpdateService(paths, new ReleaseHandler(key), key.ExportSubjectPublicKeyInfoPem(), true, false);
        await restored.RestorePendingAsync();
        Assert.True(restored.Ready);
        var installer = Directory.GetFiles(Path.Combine(_root, "Updates", "pending"), "*.exe").Single();
        await File.WriteAllBytesAsync(installer, [9, 9, 9], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => restored.RestorePendingAsync());
        Assert.False(restored.Ready);
    }
    [Theory]
    [InlineData("corrupt")]
    [InlineData("oversized")]
    [InlineData("bad-origin")]
    [InlineData("malformed")]
    [InlineData("offline")]
    public async Task FailedDownloadNeverBecomesReady(string failure)
    {
        using var key = RSA.Create(2048);
        await using var service = new UpdateService(new(_root, false, true), new ReleaseHandler(key, failure), key.ExportSubjectPublicKeyInfoPem(), true, false);
        await service.CheckAsync();
        Assert.False(service.Ready); Assert.Equal("UpdateFailed", service.Status);
        Assert.False(File.Exists(Path.Combine(_root, "Updates", "pending", "manifest.json")));
    }
    [Fact]
    public async Task InterruptedMetadataCannotRestoreButCanBeDownloadedAgain()
    {
        using var key = RSA.Create(2048);
        var pending = Path.Combine(_root, "Updates", "pending");
        Directory.CreateDirectory(pending);
        await File.WriteAllTextAsync(Path.Combine(pending, "manifest.pending"), "incomplete", TestContext.Current.CancellationToken);
        await using var service = new UpdateService(new(_root, false, true), new ReleaseHandler(key), key.ExportSubjectPublicKeyInfoPem(), true, false);
        await service.RestorePendingAsync(); Assert.False(service.Ready);
        await service.CheckAsync(); Assert.True(service.Ready);
    }
    [Fact]
    public async Task PortableCopyCannotDownloadOrBecomeEligible()
    {
        using var key = RSA.Create(2048);
        await using var service = new UpdateService(new(_root, true, false), new ReleaseHandler(key), key.ExportSubjectPublicKeyInfoPem());
        await service.CheckAsync(); Assert.False(service.Eligible); Assert.False(service.Ready);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class ReleaseHandler : HttpMessageHandler
    {
        private string _version = "1.1.0";
        private string Installer => $"VolturaWeekNumber-Setup-{_version}-win-x64.exe";
        private byte[] _manifest = [];
        private byte[] _signature = [];
        private readonly string _failure;
        public List<string> Requests { get; } = [];
        public List<int> RequestThreads { get; } = [];
        public ReleaseHandler(RSA key, string failure = "")
        {
            _failure = failure;
            SetRelease(key, _version);
        }
        public void SetRelease(RSA key, string version)
        {
            _version = version;
            _manifest = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, version = _version, assets = new[] { new { name = Installer, size = 3, sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })) } } });
            _signature = key.SignData(_manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_failure == "offline") throw new HttpRequestException("Simulated offline.");
            var name = Path.GetFileName(request.RequestUri!.AbsolutePath);
            Requests.Add(name);
            RequestThreads.Add(Environment.CurrentManagedThreadId);
            byte[] body;
            if (name == "latest")
            {
                body = _failure == "malformed" ? "{}"u8.ToArray() : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    tag_name = "v" + _version, draft = false, prerelease = false,
                    assets = new[] { Installer, $"VolturaWeekNumber-Update-{_version}.json", $"VolturaWeekNumber-Update-{_version}.sig" }.Select(asset => new
                    { name = asset, browser_download_url = $"https://{(_failure == "bad-origin" ? "example.com" : "github.com")}/voltura/voltura-weeknumber/releases/download/v{_version}/{asset}" }).ToArray()
                });
            }
            else if (name.EndsWith(".json", StringComparison.Ordinal)) body = _manifest;
            else if (name.EndsWith(".sig", StringComparison.Ordinal)) body = _signature;
            else body = _failure switch { "corrupt" => [4, 5, 6], "oversized" => [1, 2, 3, 4], _ => [1, 2, 3] };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
