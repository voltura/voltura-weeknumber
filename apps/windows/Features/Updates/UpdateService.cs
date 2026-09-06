using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Features.Updates;

internal sealed class UpdateService : IAsyncDisposable
{
    public const string ProjectUrl = "https://github.com/voltura/voltura-weeknumber";
    private readonly AppPaths _paths;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _scheduleChanged = new(0, 1);
    private Task? _worker;
    private bool _automatic;
    private string? _installer;
    private Process? _installerProcess;
    private readonly string? _publicKey;
    private readonly bool _full;
    public bool Eligible { get; }
    public bool Ready => _installer is not null;
    public string Status { get; private set; } = string.Empty;
    public event Action? Changed;
    private string Pending => Path.Combine(_paths.Data, "Updates", "pending");
    private static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new(1, 0, 0);

    internal UpdateService(AppPaths paths, HttpMessageHandler? handler = null, string? publicKey = null, bool? eligible = null, bool? full = null)
    {
        _paths = paths;
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromMinutes(5) };
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VolturaWeekNumber");
        var location = key?.GetValue("InstallLocation") as string;
        Eligible = eligible ?? (!paths.Portable && !paths.Isolated && location is not null &&
            Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar).Equals(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
        _full = full ?? (key?.GetValue("PackageVariant") as string == "full");
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("VolturaWeekNumber.Features.Updates.update-signing-public.pem");
        if (resource is not null) { using var reader = new StreamReader(resource); _publicKey = reader.ReadToEnd(); }
        if (publicKey is not null) _publicKey = publicKey;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("VolturaWeekNumber/1.0");
        if (!Eligible) Status = "ManualUpdates";
    }

    public void Start(bool automatic)
    {
        var changed = _automatic != automatic;
        _automatic = automatic;
        if (changed && _scheduleChanged.CurrentCount == 0) _scheduleChanged.Release();
        _worker ??= RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            if (!Eligible) return;
            if (Eligible)
            {
                try { await RestorePendingAsync(); }
                catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
                { SetStatus("UpdateFailed"); } // A corrupt pending package must not stop future checks.
            }
            while (!_stop.IsCancellationRequested)
            {
                if (!_automatic || Ready)
                {
                    await _scheduleChanged.WaitAsync(_stop.Token);
                    continue;
                }
                var stamp = Path.Combine(_paths.Data, "Updates", "last-check.txt");
                var delay = File.Exists(stamp) ? TimeSpan.FromDays(1) - (DateTime.UtcNow - File.GetLastWriteTimeUtc(stamp)) : TimeSpan.FromMinutes(2);
                if (delay > TimeSpan.FromDays(1)) delay = TimeSpan.FromDays(1);
                if (delay > TimeSpan.Zero)
                {
                    using var waitStop = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    var timer = Task.Delay(delay, waitStop.Token);
                    var signal = _scheduleChanged.WaitAsync(waitStop.Token);
                    var completed = await Task.WhenAny(timer, signal);
                    await waitStop.CancelAsync();
                    try { await Task.WhenAll(timer, signal); } catch (OperationCanceledException) when (!_stop.IsCancellationRequested) { }
                    if (completed == signal) continue;
                }
                if (_automatic) await CheckAsync();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or JsonException or InvalidOperationException)
        { SetStatus("UpdateFailed"); }
    }

    public async Task CheckAsync()
    {
        if (!Eligible) { SetStatus("ManualUpdates"); return; }
        if (!await _gate.WaitAsync(0, _stop.Token)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        try
        {
            SetStatus("Checking");
            Directory.CreateDirectory(Path.Combine(_paths.Data, "Updates"));
            await File.WriteAllTextAsync(Path.Combine(_paths.Data, "Updates", "last-check.txt"), DateTimeOffset.UtcNow.ToString("O"), _stop.Token);
            if (_publicKey is null) throw new InvalidDataException("Release verification key is not configured.");
            var metadata = await DownloadBytesAsync(new Uri("https://api.github.com/repos/voltura/voltura-weeknumber/releases/latest"), 256 * 1024, token);
            using var document = JsonDocument.Parse(metadata);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() || !tag.StartsWith('v') || !UpdateVerifier.TryVersion(tag[1..], out var latest)) throw new InvalidDataException("Invalid release.");
            if (latest <= CurrentVersion) { SetStatus("Current"); return; }
            var assets = root.GetProperty("assets").EnumerateArray().ToArray();
            Uri AssetUri(string name)
            {
                var matches = assets.Where(value => value.GetProperty("name").GetString() == name).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Missing release asset.");
                var uri = new Uri(matches[0].GetProperty("browser_download_url").GetString()!);
                if (uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/voltura/voltura-weeknumber/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected release origin.");
                return uri;
            }
            var manifest = await DownloadBytesAsync(AssetUri($"VolturaWeekNumber-Update-{tag[1..]}.json"), 64 * 1024, token);
            var signature = await DownloadBytesAsync(AssetUri($"VolturaWeekNumber-Update-{tag[1..]}.sig"), 1024, token);
            var verified = UpdateVerifier.Verify(manifest, signature, _publicKey, CurrentVersion, _full);
            if (verified.Version != tag[1..]) throw new InvalidDataException("Release version mismatch.");
            SetStatus("Downloading");
            Directory.CreateDirectory(Pending);
            var part = Path.Combine(Pending, "installer.pending");
            await DownloadFileAsync(AssetUri(verified.Asset.Name), part, verified.Asset.Size, token);
            await UpdateVerifier.VerifyFileAsync(part, verified.Asset, token);
            var installer = Path.Combine(Pending, verified.Asset.Name);
            File.Move(part, installer, true);
            await File.WriteAllBytesAsync(Path.Combine(Pending, "signature.sig"), signature, _stop.Token);
            await File.WriteAllBytesAsync(Path.Combine(Pending, "manifest.pending"), manifest, _stop.Token);
            File.Move(Path.Combine(Pending, "manifest.pending"), Path.Combine(Pending, "manifest.json"), true);
            _installer = installer;
            foreach (var obsolete in Directory.EnumerateFiles(Pending, "VolturaWeekNumber-Setup-*-win-x64*.exe"))
                if (!obsolete.Equals(installer, StringComparison.OrdinalIgnoreCase)) File.Delete(obsolete);
            SetStatus("Ready");
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) when (ex is InvalidDataException or HttpRequestException or IOException or UnauthorizedAccessException or JsonException or System.Security.Cryptography.CryptographicException or InvalidOperationException or KeyNotFoundException or FormatException or OperationCanceledException or ArgumentException)
        { SetStatus("UpdateFailed"); }
        finally { _gate.Release(); }
    }

    private async Task RestoreAsync(CancellationToken token)
    {
        _installer = null;
        if (_publicKey is null || !File.Exists(Path.Combine(Pending, "manifest.json"))) return;
        var manifest = await ReadBoundedAsync(Path.Combine(Pending, "manifest.json"), 64 * 1024, token);
        var signature = await ReadBoundedAsync(Path.Combine(Pending, "signature.sig"), 1024, token);
        var verified = UpdateVerifier.Verify(manifest, signature, _publicKey, CurrentVersion, _full);
        var installer = Path.Combine(Pending, verified.Asset.Name);
        await UpdateVerifier.VerifyFileAsync(installer, verified.Asset, token);
        _installer = installer;
        SetStatus("Ready");
    }

    internal async Task RestorePendingAsync()
    {
        await _gate.WaitAsync(_stop.Token);
        try { await RestoreAsync(_stop.Token); }
        finally { _gate.Release(); }
    }

    public async Task<bool> InstallAsync()
    {
        await _gate.WaitAsync(_stop.Token);
        try
        {
            if (_installerProcess is { HasExited: false }) return false;
            _installerProcess?.Dispose();
            _installerProcess = null;
            await RestoreAsync(_stop.Token);
            if (_installer is null) return false;
            _installerProcess = Process.Start(new ProcessStartInfo(_installer) { UseShellExecute = true, Arguments = "/AUTOUPDATE" });
            return _installerProcess is not null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        { SetStatus("UpdateFailed"); return false; }
        finally { _gate.Release(); }
    }

    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        for (var redirects = 0; redirects < 4; redirects++)
        {
            if (uri.Scheme != "https" || (uri.Host is not ("github.com" or "api.github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))) throw new InvalidDataException("Unexpected download origin.");
            var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); response.Dispose(); continue; }
            if (!response.IsSuccessStatusCode) { response.Dispose(); throw new HttpRequestException("Download failed."); }
            return response;
        }
        throw new InvalidDataException("Too many redirects.");
    }
    private async Task<byte[]> DownloadBytesAsync(Uri uri, int maximum, CancellationToken token)
    {
        using var response = await GetAsync(uri, token);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        await CopyBoundedAsync(stream, output, maximum, token);
        return output.ToArray();
    }
    private async Task DownloadFileAsync(Uri uri, string path, long maximum, CancellationToken token)
    {
        using var response = await GetAsync(uri, token);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await CopyBoundedAsync(input, output, maximum, token);
    }
    private static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        using var output = new MemoryStream();
        await CopyBoundedAsync(file, output, maximum, token);
        return output.ToArray();
    }
    private static async Task CopyBoundedAsync(Stream input, Stream output, long maximum, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token);
            if (read == 0) break;
            total += read;
            if (total > maximum) throw new InvalidDataException("Download exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }
    private void SetStatus(string status) { Status = status; Changed?.Invoke(); }
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_worker is not null) await _worker;
        await _gate.WaitAsync();
        _gate.Release();
        _installerProcess?.Dispose();
        _http.Dispose(); _gate.Dispose(); _scheduleChanged.Dispose(); _stop.Dispose();
    }
}
