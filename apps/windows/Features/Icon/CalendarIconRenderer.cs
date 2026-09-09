using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VolturaWeekNumber.Features.Settings;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace VolturaWeekNumber.Features.Icon;

public sealed record IconAppearance(Color Foreground, Color Background)
{
    public static IconAppearance Resolve(
        AppSettings settings,
        bool darkTaskbar,
        bool highContrast = false
    )
    {
        if (highContrast)
        {

            return new(SystemColors.WindowTextColor, SystemColors.WindowColor);
        }

        if (!settings.AutomaticIcon)
        {

            return new(Parse(settings.Foreground), Parse(settings.Background));
        }

        return darkTaskbar
            ? new(Parse("#FFF2F5FA"), Parse("#FF202936"))
            : new(Parse("#FF233047"), Parse("#FFF9FBFF"));
    }

    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
}

public static class CalendarIconRenderer
{
    public static IReadOnlyList<int> Sizes { get; } =
        Array.AsReadOnly(new[] { 16, 20, 24, 28, 32, 36, 40, 48, 64, 96, 128, 256 });
    public static BitmapSource Render(int? week, int size, IconAppearance appearance)
    {
        // Leap years in lunisolar calendars can contain up to 385 days.
        // Null represents a date outside the active calendar's supported range.
        if (week is < 1 or > 56)
        {
            throw new ArgumentOutOfRangeException(nameof(week));
        }

        if (size < 16 || size > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        ArgumentNullException.ThrowIfNull(appearance);
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            var stroke = Math.Max(1, Math.Round(size / 32d));
            var inset = stroke / 2;
            var top = Math.Max(2, Math.Round(size * .12));
            var fg = new SolidColorBrush(appearance.Foreground);
            var bg = new SolidColorBrush(appearance.Background);
            var pen = new Pen(fg, stroke);

            dc.DrawRoundedRectangle(
                bg,
                pen,
                new Rect(inset, top + inset, size - stroke, size - top - stroke),
                size < 24 ? 1 : size * .08,
                size < 24 ? 1 : size * .08
            );
            var header = Math.Round(size * .29);

            dc.DrawLine(
                pen,
                new Point(stroke, header + inset),
                new Point(size - stroke, header + inset)
            );

            foreach (var x in new[] { Math.Round(size * .28), Math.Round(size * .72) })
            {
                dc.DrawRoundedRectangle(
                    fg,
                    null,
                    new Rect(x - stroke / 2, 0, stroke, Math.Max(3, size * .21)),
                    stroke / 2,
                    stroke / 2
                );
            }

            var fontSize = size * (size <= 24 ? .69 : .65);
            var text = new FormattedText(
                week?.ToString("D2", CultureInfo.InvariantCulture) ?? "—",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI"),
                    FontStyles.Normal,
                    FontWeights.Bold,
                    FontStretches.Condensed
                ),
                fontSize,
                fg,
                1
            );
            // Centre the glyph ink, not the font's ascent/descent box.
            var geometry = text.BuildGeometry(new Point(0, 0));
            var bounds = geometry.Bounds;
            var maxWidth = size - 2 * stroke - (size <= 20 ? 1 : size * .1);
            var maxHeight = size - header - stroke - Math.Max(1, size * .06);
            var scale = Math.Min(1, Math.Min(maxWidth / bounds.Width, maxHeight / bounds.Height));
            var xOffset = (size - bounds.Width * scale) / 2 - bounds.X * scale;
            var yOffset = header + (size - header - bounds.Height * scale) / 2 - bounds.Y * scale;

            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, xOffset, yOffset));
            dc.DrawGeometry(fg, null, geometry);
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);

        bitmap.Render(visual);
        bitmap.Freeze();

        return bitmap;
    }

    public static byte[] EncodeIco(
        int? week,
        IconAppearance appearance,
        IReadOnlyList<int>? sizes = null
    )
    {
        sizes ??= Sizes;
        var images = sizes.Select(size => Png(Render(week, size, appearance))).ToArray();
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)sizes.Count);
        var offset = 6 + 16 * sizes.Count;

        for (var i = 0; i < sizes.Count; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var image in images)
        {
            writer.Write(image);
        }

        return output.ToArray();
    }

    public static byte[] Png(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();

        encoder.Save(output);

        return output.ToArray();
    }

    public static void CreateReviewSheet(string outputPath)
    {
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1100, 1060));

            foreach (var dark in new[] { false, true })
            {
                var y = dark ? 530 : 0;

                dc.DrawRectangle(
                    dark ? new SolidColorBrush(Color.FromRgb(16, 20, 28)) : Brushes.White,
                    null,
                    new Rect(0, y, 1100, 530)
                );
                var appearance = IconAppearance.Resolve(new(), dark);

                dc.DrawText(
                    new FormattedText(
                        dark ? "Dark taskbar • 01–56" : "Light taskbar • 01–56",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        22,
                        dark ? Brushes.White : Brushes.Black,
                        1
                    ),
                    new Point(20, y + 12)
                );
                for (var week = 1; week <= 56; week++)
                {
                    var col = (week - 1) % 19;
                    var row = (week - 1) / 19;

                    dc.DrawImage(
                        Render(week, 16, appearance),
                        new Rect(16 + col * 57, y + 60 + row * 52, 16, 16)
                    );
                    dc.DrawImage(
                        Render(week, 24, appearance),
                        new Rect(36 + col * 57, y + 56 + row * 52, 24, 24)
                    );
                }
                var x = 20;

                foreach (var size in Sizes)
                {
                    dc.DrawImage(Render(53, size, appearance), new Rect(x, y + 240, size, size));
                    x += size + 12;
                }
            }
        }
        var sheet = new RenderTargetBitmap(1100, 1060, 96, 96, PixelFormats.Pbgra32);

        sheet.Render(visual);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllBytes(outputPath, Png(sheet));
    }
}
