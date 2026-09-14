using System.Net;
using System.Net.Http;
using System.Text;

namespace VolturaWeekNumber.Features.Calendar;

internal static class CalendarUrlImport
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = DecompressionMethods.All,
        UseCookies = false,
    });

    internal static bool TryGetUri(string value, out Uri? uri) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri)
        && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo);

    internal static async Task<(string Content, string FileName)> DownloadAsync(
        Uri uri, HttpClient? client = null, CancellationToken token = default)
    {
        if (!TryGetUri(uri.AbsoluteUri, out _))
        {
            throw new ArgumentException("Only HTTP and HTTPS calendar URLs are supported.", nameof(uri));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);

        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var response = await (client ?? Client).GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > CalendarImport.MaximumBytes)
        {
            throw new InvalidDataException("CalendarImportLimit");
        }

        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int read;

        while ((read = await input.ReadAsync(buffer, timeout.Token)) != 0)
        {
            if (data.Length + read > CalendarImport.MaximumBytes)
            {
                throw new InvalidDataException("CalendarImportLimit");
            }

            data.Write(buffer, 0, read);
        }

        data.Position = 0;

        using var reader = new StreamReader(data, new UTF8Encoding(false, true), true);
        var content = await reader.ReadToEndAsync(timeout.Token);
        var fileName = Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            fileName = "calendar.ics";
        }

        return (content, fileName);
    }
}
