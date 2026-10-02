using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using TimeOnScreen.Core;
using Forms = System.Windows.Forms;

namespace TimeOnScreen;

// An opt-in integration check. Never loads/saves the user's settings or changes startup registration.
internal static class Verification
{
    internal static async Task RunAsync(App app, string directory)
    {
        var results = new List<object>();
        SettingsWindow? settingsWindow = null;
        OverlayWindow? overlay = null;
        OverlayWindow? controlledClock = null;
        var passed = true;
        void Check(string name, bool value)
        {
            results.Add(new { name, passed = value });
            passed &= value;
        }
        try
        {
            Directory.CreateDirectory(directory);
            settingsWindow = app.CurrentSettingsWindow;
            settingsWindow.Activate();
            await Task.Delay(400);
            Check("Chinese is the default UI and tray language", app.Settings.Language == AppLanguage.Chinese &&
                settingsWindow.Title == "浮光 · 设置" && app.TrayTitle == "浮光" &&
                string.Join(",", app.TrayMenuText) == "预览,设置,暂停提醒,退出");
            var language = (ComboBox)settingsWindow.FindName("LanguageBox");
            var languageDuration = (TextBox)settingsWindow.FindName("DurationBox");
            var languageInterval = (ComboBox)settingsWindow.FindName("IntervalBox");
            var languageSave = (Button)settingsWindow.FindName("SaveButton");
            languageDuration.Text = "3";
            languageInterval.SelectedValue = 30;
            language.SelectedValue = AppLanguage.English;
            await Task.Delay(100);
            Check("English switch updates window, buttons, choices and tray", settingsWindow.Title == "Floe · Settings" &&
                languageSave.Content is "Save" && ((Choice<int>)languageInterval.SelectedItem).Label == "Every half hour" &&
                app.TrayTitle == "Floe" && string.Join(",", app.TrayMenuText) == "Preview,Settings,Pause reminders,Exit");
            language.SelectedValue = AppLanguage.Chinese;
            await Task.Delay(100);
            Check("Chinese switch updates window, buttons, choices and tray", settingsWindow.Title == "浮光 · 设置" &&
                languageSave.Content is "保存" && ((Choice<int>)languageInterval.SelectedItem).Label == "每半小时" &&
                app.TrayTitle == "浮光" && string.Join(",", app.TrayMenuText) == "预览,设置,暂停提醒,退出");
            Check("Switching language preserves draft values without saving", languageDuration.Text == "3" &&
                languageInterval.SelectedValue is 30 && app.Settings.Language == AppLanguage.Chinese && app.Settings.IntervalMinutes == 60);
            languageDuration.Text = "-1";
            languageSave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Chinese validation remains localized", ((TextBlock)settingsWindow.FindName("MessageText")).Text.StartsWith("保存失败：持续时间", StringComparison.Ordinal));
            languageDuration.Text = "3";
            languageSave.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Saving Chinese persists language with form values", app.Settings.Language == AppLanguage.Chinese &&
                app.Settings.IntervalMinutes == 30 && new SettingsStore(Path.Combine(directory, "test-settings.json")).Load().Language == AppLanguage.Chinese);
            app.TogglePause();
            Check("Paused tray is localized in Chinese", app.TrayTitle == "浮光 · 提醒已暂停" && app.TrayMenuText[2] == "恢复提醒");
            app.TogglePause();
            Capture(settingsWindow, Path.Combine(directory, "settings-zh-CN.png"));
            settingsWindow.Close();
            app.OpenSettings();
            settingsWindow = app.CurrentSettingsWindow;
            language = (ComboBox)settingsWindow.FindName("LanguageBox");
            Check("Reopening settings restores the saved language", settingsWindow.Title == "浮光 · 设置" && language.SelectedValue is AppLanguage.Chinese);
            language.SelectedValue = AppLanguage.English;
            settingsWindow.Close();
            Check("Closing an unsaved language change restores saved tray language", UiText.Language == AppLanguage.Chinese && app.TrayTitle == "浮光");
            app.OpenSettings();
            settingsWindow = app.CurrentSettingsWindow;
            language = (ComboBox)settingsWindow.FindName("LanguageBox");
            language.SelectedValue = AppLanguage.English;
            ((TextBox)settingsWindow.FindName("DurationBox")).Text = "5";
            ((ComboBox)settingsWindow.FindName("IntervalBox")).SelectedValue = 60;
            ((Button)settingsWindow.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Switching back saves English and restores all tray labels", app.Settings.Language == AppLanguage.English &&
                settingsWindow.Title == "Floe · Settings" && string.Join(",", app.TrayMenuText) == "Preview,Settings,Pause reminders,Exit");
            await Task.Delay(100);
            Capture(settingsWindow, Path.Combine(directory, "settings.png"));
            Check("Settings window has a rendered layout", settingsWindow.ActualWidth >= 700 && settingsWindow.ActualHeight >= 590);
            var format = (ComboBox)settingsWindow.FindName("FormatBox");
            format.ApplyTemplate();
            var formatToggle = (ToggleButton)format.Template.FindName("DropDownToggle", format);
            ((IToggleProvider)new ToggleButtonAutomationPeer(formatToggle).GetPattern(PatternInterface.Toggle)).Toggle();
            await Task.Delay(150);
            var formatPopup = (Popup)format.Template.FindName("PART_Popup", format);
            Check("Styled dropdown opens through its toggle", format.IsDropDownOpen && formatPopup.IsOpen &&
                ((FrameworkElement)formatPopup.Child).ActualWidth >= format.ActualWidth - 1);
            Capture((FrameworkElement)formatPopup.Child, Path.Combine(directory, "format-menu.png"));
            var twelveHour = new ComboBoxAutomationPeer(format).GetChildren().FindLast(peer => peer.GetPattern(PatternInterface.SelectionItem) is not null)
                ?? throw new InvalidOperationException("Format selection was not exposed");
            ((ISelectionItemProvider)twelveHour.GetPattern(PatternInterface.SelectionItem)).Select();
            format.IsDropDownOpen = false;
            Check("Styled dropdown selection updates time format", format.SelectedValue is false);
            format.SelectedValue = true;
            var font = (ComboBox)settingsWindow.FindName("FontBox");
            font.ApplyTemplate();
            ((TextBox)font.Template.FindName("PART_EditableTextBox", font)).Text = "Consolas";
            Check("Styled editable font input updates selected text", font.Text == "Consolas");
            font.Text = "Segoe UI";
            var size = (Slider)settingsWindow.FindName("SizeSlider");
            size.Value = 240;
            Check("Font size slider updates its value label", ((TextBlock)settingsWindow.FindName("SizeValue")).Text == "240");
            Slider.IncreaseSmall.Execute(null, size);
            Check("Font size slider supports precise one-point adjustment", size.Value == 241);
            size.Value = 1000;
            Check("Font size slider constrains maximum", size.Value == 400);
            size.Value = 1;
            Check("Font size slider constrains minimum", size.Value == 32);
            size.Value = 160;
            var colorBox = (TextBox)settingsWindow.FindName("ColorBox");
            colorBox.Text = "#A7F3D0";
            var pickerButton = (Button)settingsWindow.FindName("ColorPickerButton");
            pickerButton.BringIntoView();
            await Task.Delay(100);
            pickerButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(100);
            var colorPopup = (Popup)settingsWindow.FindName("ColorPopup");
            var picker = (ColorPicker)settingsWindow.FindName("ColorPalette");
            var palette = (Grid)picker.FindName("SaturationValuePanel");
            var hue = (Slider)picker.FindName("HueSlider");
            Check("Color palette opens with the current hexadecimal color", colorPopup.IsOpen && picker.SelectedColor == Color.FromRgb(167, 243, 208) && palette.ActualWidth >= 260);
            Capture((FrameworkElement)colorPopup.Child, Path.Combine(directory, "color-picker.png"));
            hue.Value = 0;
            picker.SelectAt(new Point(palette.ActualWidth, 0));
            Check("Palette selection updates hexadecimal color and swatch", colorBox.Text == "#FF0000" &&
                ((SolidColorBrush)((Border)settingsWindow.FindName("SelectedColorSwatch")).Background).Color == Colors.Red);
            hue.Value = 120;
            Check("Hue slider selects green", colorBox.Text == "#00FF00");
            hue.Value = 240;
            Check("Hue slider selects blue", colorBox.Text == "#0000FF");
            await DragPaletteAsync(palette);
            var draggedColor = picker.SelectedColor;
            Check("Mouse drag changes color and keeps palette open", colorPopup.IsOpen && !palette.IsMouseCaptured &&
                draggedColor.R is >= 40 and <= 42 && draggedColor.G is >= 40 and <= 42 && draggedColor.B is >= 203 and <= 205);
            picker.SelectAt(new Point(palette.ActualWidth / 2, palette.ActualHeight / 2));
            Check("Palette selects a known saturation and brightness", colorBox.Text == "#404080");
            picker.SelectAt(new Point(-10, -10));
            Check("Palette clamps dragging beyond its edges to white", colorBox.Text == "#FFFFFF");
            picker.SelectAt(new Point(palette.ActualWidth + 10, palette.ActualHeight + 10));
            Check("Palette clamps dragging beyond its edges to black", colorBox.Text == "#000000");
            SendKey(palette, Key.End);
            Check("Palette keyboard control restores pure hue from black", colorBox.Text == "#0000FF");
            SendKey(palette, Key.Left);
            Check("Palette keyboard arrows fine-tune color", colorBox.Text == "#0303FF");
            Check("Appearance changes remain unsaved until save", app.Settings.FontSize == 160 && app.Settings.Color == "#FFFFFF");
            SendKey(palette, Key.Escape, preview: true);
            Check("Escape dismisses color palette", !colorPopup.IsOpen);
            colorBox.Text = "#FFFFFF";
            await Task.Delay(100);
            Capture(settingsWindow, Path.Combine(directory, "appearance.png"));
            var opacity = (Slider)settingsWindow.FindName("OpacitySlider");
            opacity.ApplyTemplate();
            Slider.IncreaseLarge.Execute(null, opacity);
            Check("Styled slider retains range and command behavior", opacity.Value > 80 &&
                ((Track)opacity.Template.FindName("PART_Track", opacity)).Value == opacity.Value);
            opacity.Value = 80;
            var scroll = (ScrollViewer)settingsWindow.FindName("SettingsScroll");
            var advanced = (Expander)settingsWindow.FindName("AdvancedExpander");
            advanced.ApplyTemplate();
            var advancedToggle = (ToggleButton)advanced.Template.FindName("HeaderToggle", advanced);
            ((IToggleProvider)new ToggleButtonAutomationPeer(advancedToggle).GetPattern(PatternInterface.Toggle)).Toggle();
            scroll.ScrollToEnd();
            await Task.Delay(100);
            Check("More settings expands and reveals position controls", advanced.IsExpanded && ((ComboBox)settingsWindow.FindName("MonitorBox")).IsVisible);
            Capture(settingsWindow, Path.Combine(directory, "settings-lower.png"));
            advanced.IsExpanded = false;
            ((TextBox)settingsWindow.FindName("DurationBox")).Text = "3";
            ((TextBox)settingsWindow.FindName("ColorBox")).Text = "#A7F3D0";
            var save = (Button)settingsWindow.FindName("SaveButton");
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Save button applies and persists settings", app.Settings.DurationSeconds == 3 && app.Settings.Color == "#A7F3D0" &&
                new SettingsStore(Path.Combine(directory, "test-settings.json")).Load() == app.Settings);
            ((TextBox)settingsWindow.FindName("DurationBox")).Text = "-1";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Invalid input does not overwrite saved settings", app.Settings.DurationSeconds == 3 &&
                ((TextBlock)settingsWindow.FindName("MessageText")).Text.StartsWith("Could not save", StringComparison.Ordinal));
            ((TextBox)settingsWindow.FindName("DurationBox")).Text = "3";
            var interval = (ComboBox)settingsWindow.FindName("IntervalBox");
            var daily = (TextBox)settingsWindow.FindName("DailyTimesBox");
            var hourly = (TextBox)settingsWindow.FindName("HourlyMinutesBox");
            interval.SelectedValue = 0;
            daily.Text = "1:29，13:29,13:29";
            hourly.Text = "45,45";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var customSettings = app.Settings;
            Check("Custom daily and hourly rules save from settings UI", customSettings.IntervalMinutes == 0 &&
                customSettings.DailyReminderTimes == "01:29, 13:29" && customSettings.HourlyReminderMinutes == "45" &&
                new SettingsStore(Path.Combine(directory, "test-settings.json")).Load() == customSettings);
            Check("Saved custom inputs are normalized", daily.Text == "01:29, 13:29" && hourly.Text == "45");
            var rulesPanel = (FrameworkElement)settingsWindow.FindName("CustomRulesPanel");
            rulesPanel.BringIntoView();
            await Task.Delay(100);
            Capture(settingsWindow, Path.Combine(directory, "custom-reminders.png"));
            Check("Next reminder status includes custom rules", ((TextBlock)settingsWindow.FindName("NextText")).Text ==
                $"Next reminder: {ScheduleEngine.NextReminder(DateTime.Now, customSettings):MM-dd HH:mm}");
            hourly.Text = "60";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Invalid hourly minute cannot overwrite settings", app.Settings == customSettings &&
                ((TextBlock)settingsWindow.FindName("MessageText")).Text.Contains("0 to 59", StringComparison.Ordinal));
            hourly.Text = "45";
            daily.Text = "25:29";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Invalid daily time cannot overwrite settings", app.Settings == customSettings &&
                ((TextBlock)settingsWindow.FindName("MessageText")).Text.Contains("24-hour", StringComparison.Ordinal));
            daily.Text = customSettings.DailyReminderTimes;
            app.ProcessScheduledReminders(new DateTime(2026, 10, 1, 13, 28, 59));
            Check("Custom daily reminder waits until its minute", !app.CurrentOverlay.IsVisible);
            app.ProcessScheduledReminders(new DateTime(2026, 10, 1, 13, 29, 1));
            Check("13:29 custom rule displays the real overlay", app.CurrentOverlay.IsVisible);
            app.CurrentOverlay.Dismiss();
            app.ProcessScheduledReminders(new DateTime(2026, 10, 1, 13, 29, 2));
            Check("Custom daily reminder does not display twice", !app.CurrentOverlay.IsVisible);
            app.ProcessScheduledReminders(new DateTime(2026, 10, 1, 13, 44, 59));
            app.ProcessScheduledReminders(new DateTime(2026, 10, 1, 13, 45, 1));
            Check("Hourly 45 rule displays the real overlay", app.CurrentOverlay.IsVisible);
            app.CurrentOverlay.Dismiss();
            interval.SelectedValue = 60;
            daily.Text = "";
            hourly.Text = "";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((TextBox)settingsWindow.FindName("DurationBox")).Text = "1";
            ((Button)settingsWindow.FindName("PreviewButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            overlay = app.CurrentOverlay;
            await Task.Delay(100);
            Check("Preview button shows overlay", overlay.IsVisible);
            ((Button)settingsWindow.FindName("PauseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Pause button pauses and dismisses overlay", app.IsPaused && !overlay.IsVisible);
            ((Button)settingsWindow.FindName("PauseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Resume button restores reminders", !app.IsPaused);
            var settings = new ClockSettings { DurationSeconds = 1, Position = ClockPosition.Center, BlinkStyle = BlinkStyle.Steady };
            var foreground = NativeMethods.GetForegroundWindow();
            overlay.Play(settings);
            await Task.Delay(100);
            var bounds = overlay.Bounds;
            overlay.Dismiss();
            var point = new NativeMethods.Point { X = bounds.X + bounds.Width / 2, Y = bounds.Y + bounds.Height / 2 };
            var underlying = NativeMethods.WindowFromPoint(point);
            overlay.Play(settings);
            await Task.Delay(150);
            bounds = overlay.Bounds;
            var style = NativeMethods.GetExtendedStyle(overlay.Handle);
            Check("Layered transparent window", (style & NativeMethods.WsExLayered) != 0);
            Check("Topmost", (style & NativeMethods.WsExTopmost) != 0);
            Check("No activation", (style & NativeMethods.WsExNoActivate) != 0 && NativeMethods.GetForegroundWindow() == foreground);
            Check("Click-through hit testing", (style & NativeMethods.WsExTransparent) != 0 && NativeMethods.WindowFromPoint(point) == underlying);
            Check("Visible during reminder", NativeMethods.IsWindowVisible(overlay.Handle));
            Check("Digits have nonzero layout", overlay.ActualHeight > 50 && overlay.ActualWidth > 100);
            Check("Window rectangle follows placement", NativeMethods.GetWindowRect(overlay.Handle, out var rect) && Math.Abs(rect.Left - bounds.X) <= 1 && Math.Abs(rect.Top - bounds.Y) <= 1);
            results.Add(new { name = "Placement details", expected = bounds, actual = new { rect.Left, rect.Top, rect.Right, rect.Bottom }, overlay.ActualWidth, overlay.ActualHeight, dpi = NativeMethods.GetDpiForWindow(overlay.Handle) });
            var rendered = Capture(overlay, Path.Combine(directory, "overlay.png"));
            var pixels = new byte[rendered.PixelWidth * rendered.PixelHeight * 4];
            rendered.CopyPixels(pixels, rendered.PixelWidth * 4, 0);
            var maxAlpha = 0;
            for (var i = 3; i < pixels.Length; i += 4) maxAlpha = Math.Max(maxAlpha, pixels[i]);
            Check("Background pixels are transparent", pixels[3] == 0);
            Check("Digits render at configured opacity", maxAlpha >= 195 && maxAlpha <= 210);
            await Task.Delay(1100);
            Check("Automatically hidden after duration", !NativeMethods.IsWindowVisible(overlay.Handle));
            foreach (var blink in new[] { BlinkStyle.Smooth, BlinkStyle.Hard })
            {
                overlay.Play(settings with { BlinkStyle = blink, SyncBlinkToSeconds = false });
                var minimum = overlay.DigitOpacity;
                var maximum = minimum;
                for (var sample = 0; sample < 8; sample++)
                {
                    await Task.Delay(100);
                    minimum = Math.Min(minimum, overlay.DigitOpacity);
                    maximum = Math.Max(maximum, overlay.DigitOpacity);
                }
                Check(blink + " changes digit opacity", maximum - minimum > 0.3);
                await Task.Delay(400);
                Check(blink + " animation completes", !NativeMethods.IsWindowVisible(overlay.Handle));
            }
            overlay.Play(settings);
            await Task.Delay(700);
            overlay.Play(settings);
            await Task.Delay(500);
            Check("Repeated preview restarts duration", NativeMethods.IsWindowVisible(overlay.Handle));
            await Task.Delay(650);
            Check("Repeated preview also hides", !NativeMethods.IsWindowVisible(overlay.Handle));
            var wallTime = new DateTime(2026, 10, 1, 12, 0, 58);
            controlledClock = new OverlayWindow(() => wallTime);
            controlledClock.Play(settings with { DurationSeconds = 5, BlinkStyle = BlinkStyle.Hard });
            await Task.Delay(100);
            var stableBounds = controlledClock.Bounds;
            Check("Seconds shown with HH:mm:ss format", controlledClock.DigitText == "12:00:58");
            Check("New second begins bright", controlledClock.DigitOpacity == settings.Opacity);
            wallTime = wallTime.AddSeconds(1.5);
            await WaitForAsync(() => controlledClock.DigitText == "12:00:59" && controlledClock.DigitOpacity == 0);
            Check("Displayed seconds follow wall clock", controlledClock.DigitText == "12:00:59");
            Check("Synchronized blink switches off halfway through second", controlledClock.DigitOpacity == 0);
            wallTime = wallTime.AddSeconds(0.5);
            await WaitForAsync(() => controlledClock.DigitText == "12:01:00" && controlledClock.DigitOpacity == settings.Opacity);
            Check("Minute rollover updates without changing position", controlledClock.DigitText == "12:01:00" && controlledClock.Bounds == stableBounds);
            Check("Next second starts bright again", controlledClock.DigitOpacity == settings.Opacity);
            controlledClock.Play(settings with { DurationSeconds = 5, BlinkStyle = BlinkStyle.Smooth });
            wallTime = wallTime.AddSeconds(0.5);
            await WaitForAsync(() => Math.Abs(controlledClock.DigitOpacity - 0.12) < 0.001);
            Check("Smooth blink tracks the same wall-clock second", Math.Abs(controlledClock.DigitOpacity - 0.12) < 0.001);
            controlledClock.Dismiss();
            var hiddenText = controlledClock.DigitText;
            wallTime = wallTime.AddMinutes(1);
            await Task.Delay(100);
            Check("Hidden overlay stops updating clock", !controlledClock.IsVisible && controlledClock.DigitText == hiddenText);
            var persistent = settings with { KeepVisible = true, BlinkStyle = BlinkStyle.Hard,
                ActiveHoursEnabled = true, ActiveStart = "20:00", ActiveEnd = "21:00" };
            controlledClock.SetPersistent(persistent);
            await Task.Delay(100);
            Check("Persistent clock stays steady outside reminder hours", controlledClock.IsVisible &&
                !controlledClock.IsReminding && controlledClock.DigitOpacity == persistent.Opacity);
            var persistentBounds = controlledClock.Bounds;
            wallTime = wallTime.AddSeconds(1);
            await Task.Delay(1100);
            Check("Persistent clock updates seconds and outlasts reminder duration", controlledClock.IsVisible &&
                controlledClock.DigitText == "12:02:01" && controlledClock.Bounds == persistentBounds);
            controlledClock.Play(persistent);
            Check("Reminder temporarily flashes the persistent clock", controlledClock.IsReminding && controlledClock.DigitOpacity == 0);
            await Task.Delay(1200);
            Check("Reminder returns to steady persistent clock", controlledClock.IsVisible && !controlledClock.IsReminding &&
                controlledClock.DigitOpacity == persistent.Opacity && controlledClock.Bounds == persistentBounds);
            controlledClock.Play(persistent with { SyncBlinkToSeconds = false });
            await Task.Delay(1200);
            Check("Unsynchronized animation also returns to steady persistent clock", controlledClock.IsVisible &&
                !controlledClock.IsReminding && controlledClock.DigitOpacity == persistent.Opacity);
            controlledClock.Play(persistent);
            controlledClock.SetPersistent(null);
            await Task.Delay(1100);
            Check("Disabling persistence cancels pending restoration", !controlledClock.IsVisible && !controlledClock.IsReminding);
            ((CheckBox)settingsWindow.FindName("SecondsBox")).IsChecked = false;
            ((CheckBox)settingsWindow.FindName("SyncBlinkBox")).IsChecked = false;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Seconds and blink synchronization remain optional", !app.Settings.ShowSeconds && !app.Settings.SyncBlinkToSeconds &&
                ((TextBox)settingsWindow.FindName("PeriodBox")).IsEnabled);
            var keepVisible = (CheckBox)settingsWindow.FindName("KeepVisibleBox");
            Check("Existing settings keep persistence disabled", keepVisible.IsChecked == false && !app.Settings.KeepVisible);
            keepVisible.IsChecked = true;
            ((CheckBox)settingsWindow.FindName("SecondsBox")).IsChecked = true;
            ((CheckBox)settingsWindow.FindName("SyncBlinkBox")).IsChecked = true;
            interval.SelectedValue = 0;
            daily.Text = "";
            hourly.Text = "";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(100);
            Check("Settings allow and persist clock-only mode", app.Settings.KeepVisible && !ReminderRules.HasRules(app.Settings) &&
                new SettingsStore(Path.Combine(directory, "test-settings.json")).Load() == app.Settings && overlay.IsVisible && !overlay.IsReminding);
            Check("Clock-only status clearly explains no reminders", ((TextBlock)settingsWindow.FindName("NextText")).Text == "Clock only");
            keepVisible.BringIntoView();
            await Task.Delay(100);
            Capture(settingsWindow, Path.Combine(directory, "persistent-settings.png"));
            ((Button)settingsWindow.FindName("PauseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Pausing reminders keeps persistent time visible", app.IsPaused && overlay.IsVisible && !overlay.IsReminding &&
                overlay.DigitOpacity == app.Settings.Opacity);
            ((Button)settingsWindow.FindName("PauseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Resuming reminders preserves persistent display", !app.IsPaused && overlay.IsVisible);
            app.SetSessionLocked(true);
            Check("Session lock hides persistent clock", !overlay.IsVisible);
            app.ProcessScheduledReminders(new DateTime(2026, 10, 2, 13, 45, 1));
            Check("Locked session does not redisplay clock", !overlay.IsVisible);
            app.SetSessionLocked(false);
            Check("Session unlock restores persistent clock", overlay.IsVisible && !overlay.IsReminding);
            app.SetSuspended(true);
            Check("Suspend hides persistent clock", !overlay.IsVisible);
            app.SetSuspended(false);
            Check("Resume from suspend restores persistent clock", overlay.IsVisible && !overlay.IsReminding);
            hourly.Text = "45";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            app.ProcessScheduledReminders(new DateTime(2026, 10, 2, 13, 44, 59));
            app.ProcessScheduledReminders(new DateTime(2026, 10, 2, 13, 45, 1));
            Check("Custom schedule starts reminder while clock is persistent", overlay.IsVisible && overlay.IsReminding);
            await Task.Delay(1200);
            Check("Scheduled reminder restores persistent clock", overlay.IsVisible && !overlay.IsReminding && overlay.DigitOpacity == app.Settings.Opacity);
            var originalWidth = overlay.Bounds.Width;
            size.Value = 96;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(100);
            Check("Saving appearance immediately updates persistent clock", app.Settings.FontSize == 96 &&
                new SettingsStore(Path.Combine(directory, "test-settings.json")).Load().FontSize == 96 &&
                overlay.IsVisible && overlay.Bounds.Width < originalWidth && !overlay.IsReminding);
            keepVisible.IsChecked = false;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Disabling persistence hides clock and preserves reminders", !app.Settings.KeepVisible && !overlay.IsVisible &&
                app.Settings.HourlyReminderMinutes == "45" && !new SettingsStore(Path.Combine(directory, "test-settings.json")).Load().KeepVisible);
        }
        catch (Exception ex)
        {
            passed = false;
            results.Add(new { name = "Unhandled verification error", passed = false, error = ex.ToString() });
        }
        finally
        {
            controlledClock?.Dismiss();
            controlledClock?.Close();
            try { File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { passed, results }, new JsonSerializerOptions { WriteIndented = true })); }
            catch { passed = false; }
            app.Shutdown(passed ? 0 : 1);
        }
    }

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);

    private static async Task DragPaletteAsync(FrameworkElement palette)
    {
        var original = Forms.Cursor.Position;
        var start = palette.PointToScreen(new Point(palette.ActualWidth * 0.25, palette.ActualHeight * 0.25));
        var source = System.Windows.Interop.HwndSource.FromVisual(palette) as System.Windows.Interop.HwndSource;
        var startPoint = new NativeMethods.Point { X = (int)Math.Round(start.X), Y = (int)Math.Round(start.Y) };
        if (source is null || NativeMethods.WindowFromPoint(startPoint) != source.Handle)
            throw new InvalidOperationException("Palette is covered; pointer check cannot safely run");
        try
        {
            Forms.Cursor.Position = new System.Drawing.Point(startPoint.X, startPoint.Y);
            mouse_event(0x1, 0, 0, 0, UIntPtr.Zero);
            var expectedStart = new Point(palette.ActualWidth * 0.25, palette.ActualHeight * 0.25);
            await WaitForAsync(() => (Mouse.GetPosition(palette) - expectedStart).Length < 2);
            if ((Mouse.GetPosition(palette) - expectedStart).Length >= 2 || NativeMethods.WindowFromPoint(startPoint) != source.Handle)
                throw new InvalidOperationException($"Pointer could not reach the palette safely: expected {expectedStart}; WPF {Mouse.GetPosition(palette)}; screen {Forms.Cursor.Position}; target {startPoint.X},{startPoint.Y}; window {source.Handle}; under pointer {NativeMethods.WindowFromPoint(startPoint)}");
            mouse_event(0x2, 0, 0, 0, UIntPtr.Zero);
            await WaitForAsync(() => palette.IsMouseCaptured);
            if (!palette.IsMouseCaptured) throw new InvalidOperationException("Palette did not receive pointer down");
            var end = palette.PointToScreen(new Point(palette.ActualWidth * 0.8, palette.ActualHeight * 0.2));
            Forms.Cursor.Position = new System.Drawing.Point((int)Math.Round(end.X), (int)Math.Round(end.Y));
            mouse_event(0x1, 0, 0, 0, UIntPtr.Zero);
            await WaitForAsync(() => (Mouse.GetPosition(palette) - new Point(palette.ActualWidth * 0.8, palette.ActualHeight * 0.2)).Length < 2);
            mouse_event(0x4, 0, 0, 0, UIntPtr.Zero);
            await WaitForAsync(() => !palette.IsMouseCaptured);
        }
        finally
        {
            mouse_event(0x4, 0, 0, 0, UIntPtr.Zero);
            Forms.Cursor.Position = original;
        }
    }

    private static void SendKey(UIElement target, Key key, bool preview = false)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Key target is not displayed");
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        { RoutedEvent = preview ? Keyboard.PreviewKeyDownEvent : Keyboard.KeyDownEvent });
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        // DispatcherTimer runs below async continuations in the UI queue.
        // Observe the update with a bounded wait instead of racing a fixed sleep.
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromMilliseconds(750)) await Task.Delay(25);
    }

    private static RenderTargetBitmap Capture(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return bitmap;
    }
}
