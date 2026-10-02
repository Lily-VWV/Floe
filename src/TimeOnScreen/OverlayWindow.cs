using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TimeOnScreen.Core;
using Forms = System.Windows.Forms;

namespace TimeOnScreen;

internal sealed class OverlayWindow : Window
{
    private readonly TextBlock _digits = new();
    private readonly Border _content;
    private readonly DispatcherTimer _hideTimer = new();
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Func<DateTime> _now;
    private ClockSettings? _activeSettings;
    private ClockSettings? _persistentSettings;
    private bool _isReminding;
    private int _generation;
    internal IntPtr Handle => new WindowInteropHelper(this).Handle;
    internal PixelRect Bounds { get; private set; }
    internal double DigitOpacity => _content.Opacity;
    internal string DigitText => _digits.Text;
    internal bool IsReminding => _isReminding;

    internal OverlayWindow(Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.Now);
        Title = "浮光";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Focusable = false;
        IsHitTestVisible = false;
        _digits.FontWeight = FontWeights.SemiBold;
        _digits.TextAlignment = TextAlignment.Center;
        Typography.SetNumeralAlignment(_digits, FontNumeralAlignment.Tabular);
        _content = new Border { Padding = new Thickness(20), Child = _digits, Background = Brushes.Transparent };
        Content = _content;
        SourceInitialized += (_, _) =>
        {
            NativeMethods.SetExtendedStyle(Handle, NativeMethods.GetExtendedStyle(Handle) |
                NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow);
            HwndSource.FromHwnd(Handle)?.AddHook(WindowHook);
        };
        _hideTimer.Tick += (_, _) => FinishReminder();
        _clockTimer.Tick += (_, _) => { UpdateClock(); SetClockInterval(); };
        Closed += (_, _) => Dismiss();
    }

    internal void Play(ClockSettings settings) => Display(settings, reminding: true);

    internal void SetPersistent(ClockSettings? settings)
    {
        _persistentSettings = settings;
        if (settings is null) Dismiss();
        else Display(settings, reminding: false);
    }

    private void FinishReminder()
    {
        if (_persistentSettings is { } baseline) Display(baseline, reminding: false);
        else Dismiss();
    }

    private void Display(ClockSettings settings, bool reminding)
    {
        Dismiss();
        _isReminding = reminding;
        var generation = _generation;
        var screen = SelectScreen(settings.Monitor);
        var area = screen.WorkingArea;
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        // Move onto the target monitor before measuring, so its DPI is used.
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HwndTopmost, area.Left, area.Top, 0, 0, NativeMethods.SwpNoActivate | 0x1);
        var dpi = Math.Max(96u, NativeMethods.GetDpiForWindow(hwnd)) / 96.0;
        // Reserve the full digit width before showing, so changing seconds never
        // moves the overlay or clips a wider digit at a minute/hour rollover.
        _digits.Text = (settings.ShowSeconds ? "88:88:88" : "88:88") + (settings.Use24HourClock ? "" : " AM");
        _digits.FontFamily = new FontFamily(settings.FontFamily);
        _digits.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Color));
        _digits.Effect = settings.Shadow ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 9, ShadowDepth = 2, Opacity = 0.65 } : null;
        _digits.FontSize = settings.FontSize;
        _content.Width = double.NaN;
        _content.Height = double.NaN;
        _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var availableWidth = Math.Max(1, area.Width / dpi - 32);
        var availableHeight = Math.Max(1, area.Height / dpi - 32);
        var fit = Math.Min(1, Math.Min(availableWidth / _content.DesiredSize.Width, availableHeight / _content.DesiredSize.Height));
        if (fit < 1)
        {
            _digits.FontSize = Math.Max(8, settings.FontSize * fit);
            _content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        var measuredWidth = _content.DesiredSize.Width;
        var measuredHeight = _content.DesiredSize.Height;
        _content.Width = measuredWidth;
        _content.Height = measuredHeight;
        _activeSettings = settings;
        UpdateClock();
        Bounds = OverlayPlacement.Place(new PixelRect(area.Left, area.Top, area.Width, area.Height),
            (int)Math.Ceiling(measuredWidth * dpi), (int)Math.Ceiling(measuredHeight * dpi), settings.Position,
            (int)Math.Round(settings.OffsetX * dpi), (int)Math.Round(settings.OffsetY * dpi));
        if (!reminding || !settings.SyncBlinkToSeconds) _content.Opacity = settings.Opacity;
        Show();
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HwndTopmost, Bounds.X, Bounds.Y, 0, 0, NativeMethods.SwpNoActivate | 0x1);
        // WPF owns the HWND size. Position after its initial layout has settled;
        // native sizing would race SizeToContent on high-DPI displays.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (generation != _generation || !IsVisible) return;
            UpdateLayout();
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
            Bounds = OverlayPlacement.Place(new PixelRect(area.Left, area.Top, area.Width, area.Height),
                rect.Right - rect.Left, rect.Bottom - rect.Top, settings.Position,
                (int)Math.Round(settings.OffsetX * dpi), (int)Math.Round(settings.OffsetY * dpi));
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HwndTopmost, Bounds.X, Bounds.Y, 0, 0, NativeMethods.SwpNoActivate | 0x1);
        }));

        if (reminding && !settings.SyncBlinkToSeconds && settings.BlinkStyle == BlinkStyle.Smooth)
        {
            var animation = new DoubleAnimation(settings.Opacity, settings.Opacity * 0.15,
                TimeSpan.FromSeconds(settings.BlinkPeriodSeconds / 2))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            _content.BeginAnimation(OpacityProperty, animation);
        }
        else if (reminding && !settings.SyncBlinkToSeconds && settings.BlinkStyle == BlinkStyle.Hard)
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(settings.BlinkPeriodSeconds), RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(settings.Opacity, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(settings.BlinkPeriodSeconds / 2))));
            _content.BeginAnimation(OpacityProperty, animation);
        }
        if (reminding)
        {
            _hideTimer.Interval = TimeSpan.FromSeconds(settings.DurationSeconds);
            _hideTimer.Start();
        }
        SetClockInterval();
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        if (_activeSettings is not { } settings) return;
        var now = _now();
        var text = settings.FormatTime(now);
        if (_digits.Text != text) _digits.Text = text;
        if (_isReminding && settings.SyncBlinkToSeconds)
            _content.Opacity = SecondBlink.Opacity(now, settings.Opacity, settings.BlinkStyle);
    }

    private void SetClockInterval()
    {
        if (_isReminding && _activeSettings is { SyncBlinkToSeconds: true, BlinkStyle: not BlinkStyle.Steady })
            _clockTimer.Interval = TimeSpan.FromMilliseconds(33);
        else
        {
            // A stationary clock only needs to wake at each second boundary.
            // Re-align on every tick to avoid accumulating timer drift.
            var remainingTicks = TimeSpan.TicksPerSecond - _now().Ticks % TimeSpan.TicksPerSecond;
            _clockTimer.Interval = TimeSpan.FromTicks(Math.Max(TimeSpan.TicksPerMillisecond * 10, remainingTicks));
        }
    }

    internal void Dismiss()
    {
        _generation++;
        _hideTimer.Stop();
        _clockTimer.Stop();
        _activeSettings = null;
        _isReminding = false;
        _content.BeginAnimation(OpacityProperty, null);
        if (IsVisible) Hide();
    }

    private static Forms.Screen SelectScreen(string monitor)
    {
        if (monitor == "cursor") return Forms.Screen.FromPoint(Forms.Cursor.Position);
        foreach (var screen in Forms.Screen.AllScreens)
            if (screen.DeviceName == monitor) return screen;
        return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }

    private static IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        return IntPtr.Zero;
    }
}
