using System.Text.Json;
using System.Text.Json.Serialization;

namespace VolturaWeekNumber.Features.Settings;

public sealed class SettingsStore(string directory) : IDisposable
{
    public const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string DirectoryPath { get; } = directory;
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public AppSettings Current { get; private set; } = new();
    public async Task LoadAsync()
    {
        if (File.Exists(FilePath))
        {
            Current = await ReadAsync(FilePath);
        }
    }

    public static async Task<AppSettings> ReadAsync(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            true
        );

        if (stream.Length > MaximumBytes)
        {
            throw new InvalidDataException("Settings file is too large.");
        }

        var bytes = new byte[MaximumBytes + 1];
        var count = 0;

        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count));

            if (read == 0)
            {
                break;
            }

            count += read;
        }

        if (count > MaximumBytes)
        {
            throw new InvalidDataException("Settings file is too large.");
        }

        var settings =
            JsonSerializer.Deserialize<AppSettings>(bytes.AsSpan(0, count), JsonOptions)
            ?? throw new InvalidDataException("Settings file is empty.");

        settings.Validate();

        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Validate();
        await _gate.WaitAsync();
        try
        {
            await WriteAsync(FilePath, settings);
            Current = settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task WriteAsync(string path, AppSettings settings)
    {
        settings.Validate();
        var full = Path.GetFullPath(path);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".pending";

        try
        {
            await using (
                var file = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough
                )
            )
            {
                await JsonSerializer.SerializeAsync(file, settings, JsonOptions);
                await file.FlushAsync();
            }
            File.Move(temporary, full, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public void Dispose() => _gate.Dispose();
}
