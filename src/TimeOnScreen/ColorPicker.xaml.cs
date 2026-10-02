using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TimeOnScreen;

public partial class ColorPicker : UserControl
{
    private double _hue;
    private double _saturation;
    private double _value = 1;
    private Color _selectedColor = Colors.White;
    private bool _updating;
    public event EventHandler? SelectedColorChanged;

    public ColorPicker()
    {
        InitializeComponent();
        RefreshPalette();
    }

    internal Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            var red = value.R / 255.0;
            var green = value.G / 255.0;
            var blue = value.B / 255.0;
            var maximum = Math.Max(red, Math.Max(green, blue));
            var minimum = Math.Min(red, Math.Min(green, blue));
            var delta = maximum - minimum;
            _value = maximum;
            _saturation = maximum == 0 ? 0 : delta / maximum;
            // Grey has no hue. Keep the previous hue for subsequent palette edits.
            if (delta > 0)
            {
                var sector = maximum == red ? (green - blue) / delta :
                    maximum == green ? (blue - red) / delta + 2 : (red - green) / delta + 4;
                _hue = (sector * 60 + 360) % 360;
            }
            _selectedColor = Color.FromRgb(value.R, value.G, value.B);
            _updating = true;
            try { HueSlider.Value = _hue; }
            finally { _updating = false; }
            RefreshPalette();
        }
    }

    internal void FocusPalette() => SaturationValuePanel.Focus();

    private void Palette_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SaturationValuePanel.Focus();
        SaturationValuePanel.CaptureMouse();
        SelectAt(e.GetPosition(SaturationValuePanel));
        e.Handled = true;
    }

    private void Palette_MouseMove(object sender, MouseEventArgs e)
    {
        if (SaturationValuePanel.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            SelectAt(e.GetPosition(SaturationValuePanel));
    }

    private void Palette_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!SaturationValuePanel.IsMouseCaptured) return;
        SelectAt(e.GetPosition(SaturationValuePanel));
        SaturationValuePanel.ReleaseMouseCapture();
        e.Handled = true;
    }

    internal void SelectAt(Point point)
    {
        if (SaturationValuePanel.ActualWidth <= 0 || SaturationValuePanel.ActualHeight <= 0) return;
        _saturation = Math.Clamp(point.X / SaturationValuePanel.ActualWidth, 0, 1);
        _value = 1 - Math.Clamp(point.Y / SaturationValuePanel.ActualHeight, 0, 1);
        CommitSelection();
    }

    private void Palette_KeyDown(object sender, KeyEventArgs e)
    {
        var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.05 : 0.01;
        switch (e.Key)
        {
            case Key.Left: _saturation = Math.Max(0, _saturation - step); break;
            case Key.Right: _saturation = Math.Min(1, _saturation + step); break;
            case Key.Up: _value = Math.Min(1, _value + step); break;
            case Key.Down: _value = Math.Max(0, _value - step); break;
            case Key.Home: _saturation = 0; _value = 1; break;
            case Key.End: _saturation = 1; _value = 1; break;
            default: return;
        }
        CommitSelection();
        e.Handled = true;
    }

    private void Hue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || HueSurface is null) return;
        _hue = e.NewValue;
        CommitSelection();
    }

    private void Palette_SizeChanged(object sender, SizeChangedEventArgs e) => PositionMarker();

    private void CommitSelection()
    {
        var color = FromHsv(_hue, _saturation, _value);
        var changed = color != _selectedColor;
        _selectedColor = color;
        RefreshPalette();
        if (changed) SelectedColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshPalette()
    {
        HueSurface.Background = new SolidColorBrush(FromHsv(_hue, 1, 1));
        PositionMarker();
    }

    private void PositionMarker()
    {
        if (SelectionMarker is null) return;
        Canvas.SetLeft(SelectionMarker, _saturation * SaturationValuePanel.ActualWidth - SelectionMarker.Width / 2);
        Canvas.SetTop(SelectionMarker, (1 - _value) * SaturationValuePanel.ActualHeight - SelectionMarker.Height / 2);
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var sector = (hue % 360) / 60;
        var chroma = value * saturation;
        var secondary = chroma * (1 - Math.Abs(sector % 2 - 1));
        var offset = value - chroma;
        var (red, green, blue) = sector switch
        {
            < 1 => (chroma, secondary, 0.0),
            < 2 => (secondary, chroma, 0.0),
            < 3 => (0.0, chroma, secondary),
            < 4 => (0.0, secondary, chroma),
            < 5 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary)
        };
        static byte Channel(double channel) => (byte)Math.Clamp(Math.Round(channel * 255, MidpointRounding.AwayFromZero), 0, 255);
        return Color.FromRgb(Channel(red + offset), Channel(green + offset), Channel(blue + offset));
    }
}
