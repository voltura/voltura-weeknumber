using System.Windows;
using System.Windows.Media;
using VolturaWeekNumber.Platform;

namespace VolturaWeekNumber.Ui;

public partial class ColorPickerWindow : Window
{
    private bool _ready;
    public string SelectedColor { get; private set; }
    public ColorPickerWindow(string title, string value)
    {
        InitializeComponent();
        Title = title;
        SelectedColor = value;
        var color = (Color)ColorConverter.ConvertFromString(value);
        Red.Value = color.R; Green.Value = color.G; Blue.Value = color.B; Alpha.Value = color.A;
        _ready = true;
        RefreshColor();
        WindowWorkAreaPlacement.KeepVisibleAfterDisplayChanges(this);
        Loaded += (_, _) => WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(this);
    }
    private void ChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> args) { if (_ready) RefreshColor(); }
    private void RefreshColor()
    {
        var color = Color.FromArgb((byte)Alpha.Value, (byte)Red.Value, (byte)Green.Value, (byte)Blue.Value);
        SelectedColor = color.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Hex.Text = SelectedColor;
        Swatch.Background = new SolidColorBrush(color);
    }
    private void AcceptClick(object sender, RoutedEventArgs args) => DialogResult = true;
}
