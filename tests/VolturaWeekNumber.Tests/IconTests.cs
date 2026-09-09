using System.Windows.Media.Imaging;
using VolturaWeekNumber.Features.Icon;
using VolturaWeekNumber.Features.Settings;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class IconTests
{
    private static readonly bool[] Themes = [false, true];

    [Fact]
    public void EveryWeekAndResolutionRendersAndIcoHasEveryFrame()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var dark in Themes)
                {
                    foreach (var week in Enumerable.Range(1, 53))
                    {
                        var bytes = CalendarIconRenderer.EncodeIco(
                            week,
                            IconAppearance.Resolve(new AppSettings(), dark)
                        );

                        Assert.Equal(12, BitConverter.ToUInt16(bytes, 4));
                        for (var i = 0; i < CalendarIconRenderer.Sizes.Count; i++)
                        {
                            var length = BitConverter.ToInt32(bytes, 6 + i * 16 + 8);
                            var offset = BitConverter.ToInt32(bytes, 6 + i * 16 + 12);
                            using var stream = new MemoryStream(bytes, offset, length);
                            var image = BitmapFrame.Create(
                                stream,
                                BitmapCreateOptions.None,
                                BitmapCacheOption.OnLoad
                            );

                            Assert.Equal(CalendarIconRenderer.Sizes[i], image.PixelWidth);
                            Assert.Equal(image.PixelWidth, image.PixelHeight);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }
}
