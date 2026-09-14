using System.Text;
using System.Text.Json;

namespace VolturaWeekNumber.Features.Calendar;

internal sealed record FailedCalendarImport(string FileName, string Error);

internal sealed partial class ImportedCalendarStore(string directory) : IDisposable, IAsyncDisposable
{
    private const long MaximumStoredTextBytes = 50 * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private readonly string _directory = Path.Combine(directory, "Calendars");
    private bool _loaded;
    internal IReadOnlyList<ImportedCalendar> Sources { get; private set; } = [];
    internal IReadOnlyList<FailedCalendarImport> FailedSources { get; private set; } = [];
    internal event Action? Changed;

    internal async Task LoadAsync()
    {
        using var operation = BeginOperation();

        await _gate.WaitAsync(ShutdownToken).ConfigureAwait(false);

        try
        {
            if (_loaded)
            {
                return;
            }

            var loaded = await Task.Run(LoadCoreAsync, ShutdownToken).ConfigureAwait(false);

            Sources = loaded.Sources;
            FailedSources = loaded.Failures;
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(ImportedCalendar[] Sources, FailedCalendarImport[] Failures)> LoadCoreAsync()
    {
        var sources = new List<ImportedCalendar>();
        var failures = new List<FailedCalendarImport>();

        if (Directory.Exists(_directory))
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            {
                ShutdownToken.ThrowIfCancellationRequested();

                try
                {
                    if (sources.Count >= 100 || new FileInfo(file).Length > CalendarImport.MaximumBytes * 6L)
                    {
                        throw new InvalidDataException("CalendarImportLimit");
                    }

                    var content = await File.ReadAllTextAsync(file, ShutdownToken).ConfigureAwait(false);
                    var source = JsonSerializer.Deserialize<ImportedCalendar>(content)
                        ?? throw new InvalidDataException("CalendarImportInvalid");

                    if (source.Version != 1 || source.Id == Guid.Empty || Path.GetFileName(file) != source.Id + ".json"
                        || string.IsNullOrWhiteSpace(source.FileName) || string.IsNullOrWhiteSpace(source.Content))
                    {
                        throw new InvalidDataException("CalendarImportInvalid");
                    }

                    ValidateStoredTextSize(sources, source.Content);

                    var parsed = CalendarImport.Parse(source.Content, source.FileName, source.Id);

                    sources.Add(parsed with { ImportedAt = source.ImportedAt });
                }
                catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
                    or ArgumentException or System.Security.SecurityException)
                {
                    failures.Add(new(Path.GetFileName(file), error is InvalidDataException
                        && error.Message == "CalendarImportLimit"
                        ? "CalendarImportLimit"
                        : "CalendarImportFailed"));
                }
            }
        }

        return (sources.ToArray(), failures.ToArray());
    }

    internal async Task ImportAsync(string path, Guid? replace = null)
    {
        using var operation = BeginOperation();

        await Task.Run(async () =>
        {
            await LoadAsync().ConfigureAwait(false);

            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

            if (input.Length > CalendarImport.MaximumBytes)
            {
                throw new InvalidDataException("CalendarImportLimit");
            }

            using var reader = new StreamReader(input, new UTF8Encoding(false, true), true);
            var content = await reader.ReadToEndAsync(ShutdownToken).ConfigureAwait(false);

            await ImportContentAsync(content, path, replace).ConfigureAwait(false);
        }, ShutdownToken).ConfigureAwait(false);
    }

    internal async Task ImportContentAsync(string content, string fileName, Guid? replace = null)
    {
        using var operation = BeginOperation();

        await LoadAsync().ConfigureAwait(false);

        var source = await Task.Run(() => CalendarImport.Parse(content, fileName, replace), ShutdownToken).ConfigureAwait(false);

        await _gate.WaitAsync(ShutdownToken).ConfigureAwait(false);

        try
        {
            if (Sources.Any(item => item.Hash == source.Hash))
            {
                throw new InvalidDataException("CalendarAlreadyImported");
            }

            if (replace is not null && !Sources.Any(item => item.Id == replace))
            {
                throw new InvalidDataException("CalendarImportInvalid");
            }

            if (replace is null && Sources.Count >= 100)
            {
                throw new InvalidDataException("CalendarImportLimit");
            }

            ValidateStoredTextSize(Sources, source.Content, replace);

            await Task.Run(() =>
            {
                Directory.CreateDirectory(_directory);
                CalendarExport.Save(Path.Combine(_directory, source.Id + ".json"), JsonSerializer.Serialize(source));
            }, ShutdownToken).ConfigureAwait(false);
            Sources = Sources.Where(item => item.Id != source.Id).Append(source).ToArray();
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    private static void ValidateStoredTextSize(IEnumerable<ImportedCalendar> sources, string content, Guid? replace = null)
    {
        // Strings are UTF-16. Replacements release the old source instead of adding to it.
        var characters = sources.Where(source => source.Id != replace).Sum(source => (long)source.Content.Length)
            + content.Length;

        if (characters * sizeof(char) > MaximumStoredTextBytes)
        {
            throw new InvalidDataException("CalendarImportLimit");
        }
    }

    internal async Task RemoveAsync(Guid id)
    {
        using var operation = BeginOperation();

        await LoadAsync().ConfigureAwait(false);
        await _gate.WaitAsync(ShutdownToken).ConfigureAwait(false);

        try
        {
            await Task.Run(() => File.Delete(Path.Combine(_directory, id + ".json")), ShutdownToken).ConfigureAwait(false);
            Sources = Sources.Where(item => item.Id != id).ToArray();
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    internal async Task<IReadOnlyList<ImportedOccurrence>> QueryAsync(DateOnly first, DateOnly last, CancellationToken token)
    {
        using var operation = BeginOperation();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, ShutdownToken);

        await _queryGate.WaitAsync(cancellation.Token).ConfigureAwait(false);

        try
        {
            await LoadAsync().ConfigureAwait(false);

            var snapshot = Sources;
            var zone = TimeZoneInfo.Local;

            return await Task.Run(() => CalendarImport.Query(snapshot, first, last, zone, cancellation.Token), cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            _queryGate.Release();
        }
    }

    internal async Task RemoveFailedAsync(string fileName)
    {
        using var operation = BeginOperation();

        await LoadAsync().ConfigureAwait(false);
        await _gate.WaitAsync(ShutdownToken).ConfigureAwait(false);

        try
        {
            if (Path.GetFileName(fileName) != fileName || !FailedSources.Any(item => item.FileName == fileName))
            {
                throw new InvalidDataException("CalendarImportInvalid");
            }

            await Task.Run(() => File.Delete(Path.Combine(_directory, fileName)), ShutdownToken).ConfigureAwait(false);
            FailedSources = FailedSources.Where(item => item.FileName != fileName).ToArray();
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }
}
