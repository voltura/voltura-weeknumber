using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using VolturaWeekNumber.Features.Updates;
using VolturaWeekNumber.Platform;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));
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
        private const string Installer = "VolturaWeekNumber-Setup-1.1.0-win-x64.exe";
        private readonly byte[] _manifest;
        private readonly byte[] _signature;
        private readonly string _failure;
        public ReleaseHandler(RSA key, string failure = "")
        {
            _failure = failure;
            _manifest = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, version = "1.1.0", assets = new[] { new { name = Installer, size = 3, sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })) } } });
            _signature = key.SignData(_manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_failure == "offline") throw new HttpRequestException("Simulated offline.");
            var name = Path.GetFileName(request.RequestUri!.AbsolutePath);
            byte[] body;
            if (name == "latest")
            {
                body = _failure == "malformed" ? "{}"u8.ToArray() : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    tag_name = "v1.1.0", draft = false, prerelease = false,
                    assets = new[] { Installer, "VolturaWeekNumber-Update-1.1.0.json", "VolturaWeekNumber-Update-1.1.0.sig" }.Select(asset => new
                    { name = asset, browser_download_url = $"https://{(_failure == "bad-origin" ? "example.com" : "github.com")}/voltura/voltura-weeknumber/releases/download/v1.1.0/{asset}" }).ToArray()
                });
            }
            else if (name.EndsWith(".json", StringComparison.Ordinal)) body = _manifest;
            else if (name.EndsWith(".sig", StringComparison.Ordinal)) body = _signature;
            else body = _failure switch { "corrupt" => [4, 5, 6], "oversized" => [1, 2, 3, 4], _ => [1, 2, 3] };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
}
