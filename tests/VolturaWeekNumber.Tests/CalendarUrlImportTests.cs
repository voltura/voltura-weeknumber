using System.Net;
using System.Net.Http;
using System.Text;
using VolturaWeekNumber.Features.Calendar;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class CalendarUrlImportTests
{
    private const string Calendar = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nX-WR-CALNAME:Helgdagar\r\nBEGIN:VEVENT\r\nUID:holiday\r\nDTSTART;VALUE=DATE:20260101\r\nSUMMARY:Nyårsdagen\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    [Theory]
    [InlineData("https://example.com/calendar.ics?bridge=true", true)]
    [InlineData("http://example.com/feed", true)]
    [InlineData("file:///C:/calendar.ics", false)]
    [InlineData("ftp://example.com/calendar.ics", false)]
    [InlineData("https://user:password@example.com/calendar.ics", false)]
    [InlineData("not a URL", false)]
    public void ValidatesWebAddresses(string value, bool valid) => Assert.Equal(valid, CalendarUrlImport.TryGetUri(value, out _));

    [Fact]
    public async Task PreservesQueryAndImportsUnicodeWithDuplicateAndRestartValidation()
    {
        var uri = new Uri("https://example.com/calendar.ics?bridge=true");
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal(uri, request.RequestUri);

            return new(HttpStatusCode.OK) { Content = new StringContent(Calendar, Encoding.UTF8) };
        }));
        var download = await CalendarUrlImport.DownloadAsync(uri, client, TestContext.Current.CancellationToken);

        Assert.Equal("calendar.ics", download.FileName);

        var root = Path.Combine(Path.GetTempPath(), "VolturaWeekNumber-tests", Guid.NewGuid().ToString("N"));

        try
        {
            using var store = new ImportedCalendarStore(root);

            await store.ImportContentAsync(download.Content, download.FileName);
            Assert.Equal("Helgdagar", Assert.Single(store.Sources).Name);

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportContentAsync(download.Content, download.FileName));

            Assert.Equal("CalendarAlreadyImported", error.Message);

            var id = store.Sources[0].Id;

            await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportContentAsync("<html>Error</html>", "calendar.ics", id));
            Assert.Equal(download.Content, store.Sources[0].Content);
            await store.ImportContentAsync(download.Content.Replace("Nyårsdagen", "Updated holiday"), download.FileName, id);

            using var restarted = new ImportedCalendarStore(root);

            await restarted.LoadAsync();
            Assert.Equal(id, Assert.Single(restarted.Sources).Id);
            Assert.Contains("Updated holiday", restarted.Sources[0].Content);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsExcessiveDeclaredAndStreamedResponses(bool declared)
    {
        using var client = new HttpClient(new Handler(_ =>
        {
            HttpContent content = declared
                ? new ByteArrayContent(new byte[CalendarImport.MaximumBytes + 1])
                : new StreamContent(new NonSeekableStream(new byte[CalendarImport.MaximumBytes + 1]));

            return new(HttpStatusCode.OK) { Content = content };
        }));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CalendarUrlImport.DownloadAsync(new Uri("https://example.com/feed"), client, TestContext.Current.CancellationToken));

        Assert.Equal("CalendarImportLimit", error.Message);
    }

    [Fact]
    public async Task ReportsHttpFailureAndHonorsCancellation()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<HttpRequestException>(() => CalendarUrlImport.DownloadAsync(new Uri("https://example.com/feed"), client, TestContext.Current.CancellationToken));

        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CalendarUrlImport.DownloadAsync(new Uri("https://example.com/feed"), client, cancellation.Token));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(respond(request));
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
