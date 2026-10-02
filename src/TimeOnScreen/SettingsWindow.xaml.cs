using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TimeOnScreen.Core;
using Forms = System.Windows.Forms;

namespace TimeOnScreen;

internal sealed record Choice<T>(string Label, T Value);

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private readonly DispatcherTimer _messageTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private bool _loaded;

    internal SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        _messageTimer.Tick += (_, _) => { _messageTimer.Stop(); MessageText.Visibility = Visibility.Collapsed; };
        Closed += (_, _) => { _messageTimer.Stop(); ColorPopup.IsOpen = false; _app.SetUiLanguage(_app.Settings.Language, refreshSettings: false); };
        LanguageBox.ItemsSource = new[] { new Choice<AppLanguage>("简体中文", AppLanguage.Chinese), new Choice<AppLanguage>("English", AppLanguage.English) };
        LanguageBox.SelectedValue = app.Settings.Language;
        FontBox.ItemsSource = new[] { "Segoe UI", "Bahnschrift", "Arial", "Consolas", "Microsoft YaHei UI" };
        RefreshChoiceLabels();
        LoadValues(app.Settings);
        _loaded = true;
        HoursChanged(this, new RoutedEventArgs());
        SyncBlinkChanged(this, new RoutedEventArgs());
        UpdateColorSwatch();
        UpdateStatus();
        if (app.InitialWarning is not null) ShowMessage(app.InitialWarning);
    }

    private void RefreshChoiceLabels()
    {
        var interval = IntervalBox.SelectedValue ?? _app.Settings.IntervalMinutes;
        var format = FormatBox.SelectedValue ?? _app.Settings.Use24HourClock;
        var blink = BlinkBox.SelectedValue ?? _app.Settings.BlinkStyle;
        var position = PositionBox.SelectedValue ?? _app.Settings.Position;
        IntervalBox.ItemsSource = new[] { new Choice<int>(UiText.Get("Hourly"), 60), new Choice<int>(UiText.Get("HalfHourly"), 30), new Choice<int>(UiText.Get("NoInterval"), 0) };
        FormatBox.ItemsSource = new[] { new Choice<bool>(UiText.Get("Format24"), true), new Choice<bool>(UiText.Get("Format12"), false) };
        BlinkBox.ItemsSource = new[] { new Choice<BlinkStyle>(UiText.Get("Smooth"), BlinkStyle.Smooth), new Choice<BlinkStyle>(UiText.Get("Hard"), BlinkStyle.Hard), new Choice<BlinkStyle>(UiText.Get("Steady"), BlinkStyle.Steady) };
        PositionBox.ItemsSource = new[]
        {
            new Choice<ClockPosition>(UiText.Get("UpperCenter"), ClockPosition.UpperCenter), new Choice<ClockPosition>(UiText.Get("Center"), ClockPosition.Center),
            new Choice<ClockPosition>(UiText.Get("LowerCenter"), ClockPosition.LowerCenter), new Choice<ClockPosition>(UiText.Get("TopLeft"), ClockPosition.TopLeft),
            new Choice<ClockPosition>(UiText.Get("TopRight"), ClockPosition.TopRight), new Choice<ClockPosition>(UiText.Get("BottomLeft"), ClockPosition.BottomLeft),
            new Choice<ClockPosition>(UiText.Get("BottomRight"), ClockPosition.BottomRight)
        };
        IntervalBox.SelectedValue = interval;
        FormatBox.SelectedValue = format;
        BlinkBox.SelectedValue = blink;
        PositionBox.SelectedValue = position;
        RefreshMonitors(MonitorBox.SelectedValue as string ?? _app.Settings.Monitor);
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loaded && LanguageBox.SelectedValue is AppLanguage language) _app.SetUiLanguage(language);
    }

    internal void RefreshLanguage()
    {
        RefreshChoiceLabels();
        _messageTimer.Stop();
        MessageText.Visibility = Visibility.Collapsed;
        UpdateStatus();
    }

    internal void RefreshMonitors(string? selected = null)
    {
        selected ??= MonitorBox.SelectedValue as string ?? "primary";
        var choices = new List<Choice<string>> { new(UiText.Get("PrimaryMonitor"), "primary"), new(UiText.Get("CursorMonitor"), "cursor") };
        var index = 1;
        foreach (var screen in Forms.Screen.AllScreens)
            choices.Add(new Choice<string>(UiText.Format("MonitorChoice", index++, screen.Bounds.Width, screen.Bounds.Height, screen.Primary ? UiText.Get("PrimarySuffix") : ""), screen.DeviceName));
        MonitorBox.ItemsSource = choices;
        MonitorBox.SelectedValue = choices.Exists(c => c.Value == selected) ? selected : "primary";
    }

    private void LoadValues(ClockSettings settings)
    {
        IntervalBox.SelectedValue = settings.IntervalMinutes;
        DailyTimesBox.Text = settings.DailyReminderTimes;
        HourlyMinutesBox.Text = settings.HourlyReminderMinutes;
        FormatBox.SelectedValue = settings.Use24HourClock;
        SecondsBox.IsChecked = settings.ShowSeconds;
        KeepVisibleBox.IsChecked = settings.KeepVisible;
        SyncBlinkBox.IsChecked = settings.SyncBlinkToSeconds;
        FontBox.Text = settings.FontFamily;
        SizeSlider.Value = settings.FontSize;
        DurationBox.Text = Number(settings.DurationSeconds);
        ColorBox.Text = settings.Color;
        OpacitySlider.Value = settings.Opacity * 100;
        PeriodBox.Text = Number(settings.BlinkPeriodSeconds);
        BlinkBox.SelectedValue = settings.BlinkStyle;
        ShadowBox.IsChecked = settings.Shadow;
        PositionBox.SelectedValue = settings.Position;
        XBox.Text = settings.OffsetX.ToString(CultureInfo.InvariantCulture);
        YBox.Text = settings.OffsetY.ToString(CultureInfo.InvariantCulture);
        ActiveHoursBox.IsChecked = settings.ActiveHoursEnabled;
        StartBox.Text = settings.ActiveStart;
        EndBox.Text = settings.ActiveEnd;
        StartupBox.IsChecked = settings.StartWithWindows;
    }

    private ClockSettings ReadValues()
    {
        var duration = ReadNumber(DurationBox, UiText.Get("Duration"), 1, 30);
        var size = SizeSlider.Value;
        var period = ReadNumber(PeriodBox, UiText.Get("BlinkPeriod"), 0.5, 4);
        var x = ReadNumber(XBox, UiText.Get("OffsetX"), -4000, 4000);
        var y = ReadNumber(YBox, UiText.Get("OffsetY"), -4000, 4000);
        if (x != Math.Truncate(x) || y != Math.Truncate(y)) throw new ArgumentException(UiText.Get("IntegerOffsets"));
        var color = ColorBox.Text.Trim();
        if (!ClockSettings.IsColor(color)) throw new ArgumentException(UiText.Get("InvalidColor"));
        if (string.IsNullOrWhiteSpace(FontBox.Text)) throw new ArgumentException(UiText.Get("MissingFont"));
        var start = StartBox.Text.Trim();
        var end = EndBox.Text.Trim();
        if (!ClockSettings.TryTime(start, out _) || !ClockSettings.TryTime(end, out _))
            throw new ArgumentException(UiText.Get("InvalidHours"));
        if (ActiveHoursBox.IsChecked == true && start == end) throw new ArgumentException(UiText.Get("EqualHours"));
        var settings = _app.Settings with
        {
            Language = UiText.Language,
            IntervalMinutes = (int)(IntervalBox.SelectedValue ?? 60), Use24HourClock = (bool)(FormatBox.SelectedValue ?? true),
            DailyReminderTimes = ReminderRules.NormalizeDailyTimes(DailyTimesBox.Text, language: UiText.Language),
            HourlyReminderMinutes = ReminderRules.NormalizeHourlyMinutes(HourlyMinutesBox.Text, language: UiText.Language),
            ShowSeconds = SecondsBox.IsChecked == true, SyncBlinkToSeconds = SyncBlinkBox.IsChecked == true,
            KeepVisible = KeepVisibleBox.IsChecked == true,
            FontFamily = FontBox.Text.Trim(), FontSize = size, Color = color.ToUpperInvariant(), Opacity = OpacitySlider.Value / 100,
            DurationSeconds = duration, BlinkPeriodSeconds = period, BlinkStyle = (BlinkStyle)(BlinkBox.SelectedValue ?? BlinkStyle.Smooth),
            Shadow = ShadowBox.IsChecked == true, Monitor = MonitorBox.SelectedValue as string ?? "primary",
            Position = (ClockPosition)(PositionBox.SelectedValue ?? ClockPosition.UpperCenter), OffsetX = (int)x, OffsetY = (int)y,
            ActiveHoursEnabled = ActiveHoursBox.IsChecked == true, ActiveStart = start, ActiveEnd = end,
            StartWithWindows = StartupBox.IsChecked == true
        };
        if (!settings.KeepVisible || ReminderRules.HasRules(settings)) _ = ScheduleEngine.NextReminder(DateTime.Now, settings);
        return settings;
    }

    private static double ReadNumber(TextBox box, string label, double min, double max)
    {
        if (!(double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) ||
            !double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException(UiText.Format("NumberRange", label, min, max));
        return value;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try { _app.Preview(ReadValues()); ShowMessage(UiText.Get("Previewing"), false); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { ShowMessage(ex.Message); }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _app.SaveSettings(ReadValues());
            DailyTimesBox.Text = _app.Settings.DailyReminderTimes;
            HourlyMinutesBox.Text = _app.Settings.HourlyReminderMinutes;
            ShowMessage(UiText.Get("Saved"), false);
            UpdateColorSwatch();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { ShowMessage(UiText.Get("SaveFailed") + ex.Message); }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _app.TogglePause();
    private void ColorPicker_Click(object sender, RoutedEventArgs e)
    {
        var color = ColorBox.Text.Trim();
        if (ClockSettings.IsColor(color)) ColorPalette.SelectedColor = (Color)ColorConverter.ConvertFromString(color);
        ColorPopup.IsOpen = true;
    }
    private void ColorPopup_Opened(object sender, EventArgs e) => ColorPalette.FocusPalette();
    private void ColorPicker_Changed(object? sender, EventArgs e)
    {
        var color = ColorPalette.SelectedColor;
        ColorBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
    private void ColorPicker_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        ColorPopup.IsOpen = false;
        ColorPickerButton.Focus();
        e.Handled = true;
    }
    private void ColorChanged(object sender, TextChangedEventArgs e) { if (_loaded) UpdateColorSwatch(); }
    private void SizeSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SizeValue is not null) SizeValue.Text = Number(e.NewValue);
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel is not null) OpacityLabel.Text = $"{e.NewValue:0}%";
    }
    private void HoursChanged(object sender, RoutedEventArgs e) { if (HoursPanel is not null) HoursPanel.IsEnabled = ActiveHoursBox.IsChecked == true; }
    private void SyncBlinkChanged(object sender, RoutedEventArgs e) { if (PeriodBox is not null) PeriodBox.IsEnabled = SyncBlinkBox.IsChecked != true; }
    private void UpdateColorSwatch()
    {
        if (ClockSettings.IsColor(ColorBox.Text.Trim()))
        {
            SelectedColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorBox.Text.Trim()));
        }
    }

    internal void UpdateStatus()
    {
        StatusText.Text = _app.IsPaused ? UiText.Get("Paused") : _app.Settings.KeepVisible ? UiText.Get("AlwaysVisible") : UiText.Get("Enabled");
        PauseButton.Content = _app.IsPaused ? UiText.Get("Resume") : UiText.Get("Pause");
        if (_app.IsPaused) NextText.Text = _app.Settings.KeepVisible ? UiText.Get("ClockContinues") : "";
        else if (_app.Settings.KeepVisible && !ReminderRules.HasRules(_app.Settings)) NextText.Text = UiText.Get("ClockOnly");
        else
        {
            try { NextText.Text = UiText.Format("NextReminder", ScheduleEngine.NextReminder(DateTime.Now, _app.Settings)); }
            catch (InvalidOperationException) { NextText.Text = UiText.Get("NoReminders"); }
        }
    }

    internal void ShowMessage(string message, bool error = true)
    {
        _messageTimer.Stop();
        MessageText.Text = message;
        MessageText.Foreground = new SolidColorBrush(error ? Color.FromRgb(181, 71, 50) : Color.FromRgb(83, 104, 223));
        MessageText.Visibility = Visibility.Visible;
        if (!error) _messageTimer.Start();
    }
    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
