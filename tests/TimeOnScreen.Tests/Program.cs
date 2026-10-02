using System;
using System.Collections.Generic;
using System.IO;
using TimeOnScreen.Core;

var tests = new List<(string Name, Action Run)>();
DateTime At(int hour, int minute, double seconds = 0) => new DateTime(2026, 10, 1, hour, minute, 0).AddSeconds(seconds);
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}");
}
void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
var settings = new ClockSettings();
tests.Add(("Chinese is the default for new and legacy settings", () =>
{
    Equal(AppLanguage.Chinese, settings.Language);
    var legacy = System.Text.Json.JsonSerializer.Deserialize<ClockSettings>("{\"FontSize\":96,\"KeepVisible\":true}")!;
    Equal(AppLanguage.Chinese, legacy.Language);
    Equal(96.0, legacy.FontSize);
    Assert(legacy.KeepVisible);
    Equal(AppLanguage.Chinese, (settings with { Language = (AppLanguage)99 }).Normalize().Language);
}));
tests.Add(("Either saved language persists without changing clock or reminder settings", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "Floe-language-tests-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        foreach (var language in new[] { AppLanguage.Chinese, AppLanguage.English })
        {
            var localized = settings with { Language = language, KeepVisible = true, DailyReminderTimes = "13:29", Color = "#A7F3D0" };
            store.Save(localized);
            Equal(localized, store.Load());
            Equal("12:00:01", localized.FormatTime(At(12, 0, 1)));
            Equal(ScheduleEngine.NextReminder(At(13, 1), localized with { Language = AppLanguage.Chinese }), ScheduleEngine.NextReminder(At(13, 1), localized));
        }
    }
    finally { if (Directory.Exists(directory)) { foreach (var item in Directory.GetFiles(directory)) File.Delete(item); Directory.Delete(directory); } }
}));
tests.Add(("Reminder validation uses the selected language", () =>
{
    foreach (var language in new[] { AppLanguage.English, AppLanguage.Chinese })
    {
        try { ReminderRules.ParseDailyTimes("25:29", language); throw new Exception("Expected invalid daily time"); }
        catch (ArgumentException ex) { Assert(ex.Message.Contains(language == AppLanguage.English ? "24-hour" : "24 小时制", StringComparison.Ordinal)); }
        try { ReminderRules.ParseHourlyMinutes("60", language); throw new Exception("Expected invalid hourly minute"); }
        catch (ArgumentException ex) { Assert(ex.Message.Contains(language == AppLanguage.English ? "0 to 59" : "0～59", StringComparison.Ordinal)); }
        try { ScheduleEngine.NextReminder(At(12, 0), settings with { Language = language, IntervalMinutes = 0 }); throw new Exception("Expected empty schedule"); }
        catch (InvalidOperationException ex) { Assert(ex.Message.StartsWith(language == AppLanguage.English ? "Add a custom" : "请填写自定义", StringComparison.Ordinal)); }
    }
}));
tests.Add(("On-time with timer jitter; no duplicate", () =>
{
    var engine = new ScheduleEngine(At(14, 59, 58), settings);
    Equal<DateTime?>(null, engine.Poll(At(14, 59, 59.9), settings));
    Equal<DateTime?>(At(15, 0), engine.Poll(At(15, 0, 0.7), settings));
    Equal<DateTime?>(null, engine.Poll(At(15, 0, 1.2), settings));
}));
tests.Add(("Launching at a boundary waits for next hour", () =>
{
    var engine = new ScheduleEngine(At(15, 0), settings);
    Equal<DateTime?>(null, engine.Poll(At(15, 0, 1), settings));
    Equal<DateTime?>(At(16, 0), engine.Poll(At(16, 0, 1), settings));
}));
tests.Add(("Late resume skips missed hours", () =>
{
    var engine = new ScheduleEngine(At(10, 15), settings);
    Equal<DateTime?>(null, engine.Poll(At(14, 20), settings));
    Equal<DateTime?>(At(15, 0), engine.Poll(At(15, 0, 2), settings));
}));
tests.Add(("Resume near latest boundary only announces that hour", () =>
{
    var engine = new ScheduleEngine(At(10, 15), settings);
    Equal<DateTime?>(At(14, 0), engine.Poll(At(14, 0, 3), settings));
    Equal<DateTime?>(null, engine.Poll(At(14, 0, 4), settings));
}));
tests.Add(("Grace limit and midnight", () =>
{
    var engine = new ScheduleEngine(At(23, 58), settings);
    var midnight = At(0, 0).AddDays(1);
    Equal<DateTime?>(midnight, engine.Poll(midnight.AddSeconds(5), settings));
    var late = new ScheduleEngine(At(14, 0, 1), settings);
    Equal<DateTime?>(null, late.Poll(At(15, 0, 5.1), settings));
}));
tests.Add(("Half-hour interval", () =>
{
    var half = settings with { IntervalMinutes = 30 };
    var engine = new ScheduleEngine(At(14, 20), half);
    Equal<DateTime?>(At(14, 30), engine.Poll(At(14, 30, 0.5), half));
    Equal(At(15, 0), ScheduleEngine.NextReminder(At(14, 30), half));
}));
tests.Add(("Paused or locked boundaries are consumed", () =>
{
    var engine = new ScheduleEngine(At(14, 59), settings);
    Equal<DateTime?>(null, engine.Poll(At(15, 0, 0.5), settings, suppressed: true));
    Equal<DateTime?>(null, engine.Poll(At(15, 0, 2), settings));
}));
tests.Add(("Active hours include start, exclude end", () =>
{
    var active = settings with { ActiveHoursEnabled = true, ActiveStart = "09:00", ActiveEnd = "22:00" };
    Assert(active.IsActiveAt(At(9, 0)));
    Assert(!active.IsActiveAt(At(22, 0)));
    Equal(At(9, 0).AddDays(1), ScheduleEngine.NextReminder(At(21, 30), active));
}));
tests.Add(("Overnight active hours", () =>
{
    var active = settings with { ActiveHoursEnabled = true, ActiveStart = "22:00", ActiveEnd = "07:00" };
    Assert(active.IsActiveAt(At(23, 0)) && active.IsActiveAt(At(0, 0)) && !active.IsActiveAt(At(12, 0)));
    Equal(At(22, 0), ScheduleEngine.NextReminder(At(6, 30), active));
}));
tests.Add(("Time moves back without duplicate announcements", () =>
{
    var engine = new ScheduleEngine(At(14, 59), settings);
    Equal<DateTime?>(At(15, 0), engine.Poll(At(15, 0, 1), settings));
    Equal<DateTime?>(null, engine.Poll(At(14, 59), settings));
    Equal<DateTime?>(null, engine.Poll(At(15, 0, 2), settings));
    engine.Recalculate(At(16, 15), settings);
    Equal<DateTime?>(At(17, 0), engine.Poll(At(17, 0, 1), settings));
}));
tests.Add(("Invalid active window with no reminder boundary is rejected", () =>
{
    var active = settings with { ActiveHoursEnabled = true, ActiveStart = "09:10", ActiveEnd = "09:20" };
    try { ScheduleEngine.NextReminder(At(8, 0), active); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected invalid schedule");
}));
tests.Add(("Secondary monitor negative coordinates and clamping", () =>
{
    var area = new PixelRect(-1920, 0, 1920, 1040);
    var centered = OverlayPlacement.Place(area, 500, 200, ClockPosition.Center, 0, 0);
    Equal(new PixelRect(-1210, 420, 500, 200), centered);
    var clamped = OverlayPlacement.Place(area, 500, 200, ClockPosition.TopRight, 4000, -4000);
    Equal(new PixelRect(-500, 0, 500, 200), clamped);
}));
tests.Add(("Oversized display stays inside tiny monitor", () =>
{
    Equal(new PixelRect(20, 30, 100, 80), OverlayPlacement.Place(new PixelRect(20, 30, 100, 80), 1000, 800, ClockPosition.BottomRight, 0, 0));
}));
tests.Add(("Corrupt configuration recovery and persistence", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "TimeOnScreen-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, "settings.json");
    try
    {
        File.WriteAllText(file, "{broken");
        var store = new SettingsStore(file);
        Equal(new ClockSettings(), store.Load());
        Assert(store.LoadWarning is not null);
        Equal("{broken", File.ReadAllText(file));
        var custom = settings with { KeepVisible = true, BlinkStyle = BlinkStyle.Hard, Color = "#A7F3D0", DurationSeconds = 9, Monitor = @"\\.\DISPLAY2", DailyReminderTimes = "01:29, 13:29", HourlyReminderMinutes = "15, 45" };
        store.Save(custom);
        Equal(custom, new SettingsStore(file).Load());
        Assert(!File.Exists(file + ".tmp"));
        File.WriteAllText(file, "{\"IntervalMinutes\":30,\"Color\":\"#A7F3D0\"}");
        var legacy = store.Load();
        Equal(30, legacy.IntervalMinutes);
        Equal("#A7F3D0", legacy.Color);
        Equal("", legacy.DailyReminderTimes);
        Equal("", legacy.HourlyReminderMinutes);
        Assert(!legacy.KeepVisible);
    }
    finally { foreach (var item in Directory.GetFiles(directory)) File.Delete(item); Directory.Delete(directory); }
}));
tests.Add(("Untrusted configuration is normalized", () =>
{
    var normalized = (settings with { Color = "oops", Opacity = double.NaN, FontSize = 900, IntervalMinutes = 7, Position = (ClockPosition)99 }).Normalize();
    Equal("#FFFFFF", normalized.Color); Equal(0.8, normalized.Opacity); Equal(400.0, normalized.FontSize); Equal(60, normalized.IntervalMinutes);
}));
tests.Add(("Time format is explicit", () =>
{
    Equal("12:00:01", settings.FormatTime(At(12, 0, 1)));
    Equal("15:00:00", settings.FormatTime(At(15, 0)));
    Equal("3:00:01 PM", (settings with { Use24HourClock = false }).FormatTime(At(15, 0, 1)));
    Equal("15:00", (settings with { ShowSeconds = false }).FormatTime(At(15, 0, 1)));
    Equal("3:00 PM", (settings with { ShowSeconds = false, Use24HourClock = false }).FormatTime(At(15, 0, 1)));
}));
tests.Add(("Seconds roll over with actual wall time", () =>
{
    Equal("12:00:59", settings.FormatTime(At(12, 0, 59)));
    Equal("12:01:00", settings.FormatTime(At(12, 0, 59).AddSeconds(1)));
    Equal("00:00:00", settings.FormatTime(At(23, 59, 59).AddSeconds(1)));
}));
tests.Add(("Each second starts bright, fades or switches off, then restarts", () =>
{
    Equal(0.8, SecondBlink.Opacity(At(12, 0, 1), 0.8, BlinkStyle.Smooth));
    Assert(Math.Abs(SecondBlink.Opacity(At(12, 0, 1.5), 0.8, BlinkStyle.Smooth) - 0.12) < 0.0001);
    Equal(0.8, SecondBlink.Opacity(At(12, 0, 2), 0.8, BlinkStyle.Smooth));
    Equal(0.8, SecondBlink.Opacity(At(12, 0, 1.499), 0.8, BlinkStyle.Hard));
    Equal(0.0, SecondBlink.Opacity(At(12, 0, 1.5), 0.8, BlinkStyle.Hard));
    Equal(0.8, SecondBlink.Opacity(At(12, 0, 2), 0.8, BlinkStyle.Hard));
    Equal(0.8, SecondBlink.Opacity(At(12, 0, 1.5), 0.8, BlinkStyle.Steady));
}));
tests.Add(("Custom rule parsing handles Chinese separators, sorts and deduplicates", () =>
{
    Equal("01:29, 13:29", ReminderRules.NormalizeDailyTimes("13:29，1：29; 13:29"));
    Equal("05, 15, 45", ReminderRules.NormalizeHourlyMinutes("45、5，15,45"));
    Equal("", ReminderRules.NormalizeDailyTimes("  "));
}));
tests.Add(("Invalid custom times and minutes are rejected", () =>
{
    foreach (var value in new[] { "24:00", "13:60", "13:29:01", "bad" })
    {
        var rejected = false;
        try { ReminderRules.ParseDailyTimes(value); } catch (ArgumentException) { rejected = true; }
        Assert(rejected);
    }
    foreach (var value in new[] { "60", "-1", "1.5", "13:29" })
    {
        var rejected = false;
        try { ReminderRules.ParseHourlyMinutes(value); } catch (ArgumentException) { rejected = true; }
        Assert(rejected);
    }
}));
tests.Add(("Daily 01:29 and 13:29 fire on their precise minute", () =>
{
    var custom = settings with { IntervalMinutes = 0, DailyReminderTimes = "01:29, 13:29" };
    var engine = new ScheduleEngine(At(1, 28, 59), custom);
    Equal<DateTime?>(At(1, 29), engine.Poll(At(1, 29, 0.7), custom));
    Equal<DateTime?>(null, engine.Poll(At(2, 0, 1), custom));
    Equal(At(13, 29), ScheduleEngine.NextReminder(At(1, 29), custom));
    Equal<DateTime?>(At(13, 29), engine.Poll(At(13, 29, 1), custom));
}));
tests.Add(("Hourly 45 fires every hour and custom-only skips the hour", () =>
{
    var custom = settings with { IntervalMinutes = 0, HourlyReminderMinutes = "45" };
    var engine = new ScheduleEngine(At(12, 44, 59), custom);
    Equal<DateTime?>(At(12, 45), engine.Poll(At(12, 45, 1), custom));
    Equal<DateTime?>(null, engine.Poll(At(13, 0, 1), custom));
    Equal<DateTime?>(At(13, 45), engine.Poll(At(13, 45, 1), custom));
    Equal(At(14, 45), ScheduleEngine.NextReminder(At(13, 45), custom));
}));
tests.Add(("Multiple hourly minutes", () =>
{
    var custom = settings with { IntervalMinutes = 0, HourlyReminderMinutes = "15, 45" };
    var engine = new ScheduleEngine(At(12, 14), custom);
    Equal<DateTime?>(At(12, 15), engine.Poll(At(12, 15, 1), custom));
    Equal(At(12, 45), ScheduleEngine.NextReminder(At(12, 15), custom));
    Equal<DateTime?>(At(12, 45), engine.Poll(At(12, 45, 1), custom));
}));
tests.Add(("Daily, hourly and fixed rules combine chronologically", () =>
{
    var custom = settings with { DailyReminderTimes = "13:29", HourlyReminderMinutes = "45" };
    Equal(At(13, 0), ScheduleEngine.NextReminder(At(12, 46), custom));
    Equal(At(13, 29), ScheduleEngine.NextReminder(At(13, 0), custom));
    Equal(At(13, 45), ScheduleEngine.NextReminder(At(13, 29), custom));
    Equal(At(14, 0), ScheduleEngine.NextReminder(At(13, 45), custom));
}));
tests.Add(("Overlapping rules cause a single reminder", () =>
{
    var custom = settings with { IntervalMinutes = 30, DailyReminderTimes = "13:30, 13:30", HourlyReminderMinutes = "30, 30" };
    var engine = new ScheduleEngine(At(13, 29), custom);
    Equal<DateTime?>(At(13, 30), engine.Poll(At(13, 30, 1), custom));
    Equal<DateTime?>(null, engine.Poll(At(13, 30, 2), custom));
    Equal(At(14, 0), ScheduleEngine.NextReminder(At(13, 30), custom));
}));
tests.Add(("Daily custom rules repeat on the next day and at midnight", () =>
{
    var custom = settings with { IntervalMinutes = 0, DailyReminderTimes = "00:00, 01:29" };
    Equal(At(0, 0).AddDays(1), ScheduleEngine.NextReminder(At(23, 59), custom));
    var engine = new ScheduleEngine(At(23, 59), custom);
    Equal<DateTime?>(At(0, 0).AddDays(1), engine.Poll(At(0, 0).AddDays(1).AddSeconds(1), custom));
    var daily = custom with { DailyReminderTimes = "01:29" };
    Equal(At(1, 29).AddDays(1), ScheduleEngine.NextReminder(At(1, 29), daily));
}));
tests.Add(("Custom reminders honor pauses and do not replay after sleep", () =>
{
    var custom = settings with { IntervalMinutes = 0, HourlyReminderMinutes = "45" };
    var engine = new ScheduleEngine(At(12, 44), custom);
    Equal<DateTime?>(null, engine.Poll(At(12, 45, 1), custom, suppressed: true));
    Equal<DateTime?>(null, engine.Poll(At(12, 45, 2), custom));
    Equal<DateTime?>(null, engine.Poll(At(15, 50), custom));
    Equal<DateTime?>(At(16, 45), engine.Poll(At(16, 45, 2), custom));
}));
tests.Add(("Daily custom rule has the same short resume grace", () =>
{
    var custom = settings with { IntervalMinutes = 0, DailyReminderTimes = "13:29" };
    var engine = new ScheduleEngine(At(10, 0), custom);
    Equal<DateTime?>(At(13, 29), engine.Poll(At(13, 29, 3), custom));
    var late = new ScheduleEngine(At(10, 0), custom);
    Equal<DateTime?>(null, late.Poll(At(13, 29, 6), custom));
}));
tests.Add(("Custom reminders honor daytime and overnight active windows", () =>
{
    var custom = settings with { IntervalMinutes = 0, DailyReminderTimes = "01:29, 13:29", ActiveHoursEnabled = true };
    Equal(At(13, 29), ScheduleEngine.NextReminder(At(0, 0), custom));
    var overnight = custom with { DailyReminderTimes = "", HourlyReminderMinutes = "45", ActiveStart = "22:00", ActiveEnd = "07:00" };
    Equal(At(6, 45), ScheduleEngine.NextReminder(At(6, 40), overnight));
    Equal(At(22, 45), ScheduleEngine.NextReminder(At(6, 45), overnight));
}));
tests.Add(("Clock rollback does not repeat custom reminder", () =>
{
    var custom = settings with { IntervalMinutes = 0, DailyReminderTimes = "13:29" };
    var engine = new ScheduleEngine(At(13, 28), custom);
    Equal<DateTime?>(At(13, 29), engine.Poll(At(13, 29, 1), custom));
    Equal<DateTime?>(null, engine.Poll(At(13, 28), custom));
    Equal<DateTime?>(null, engine.Poll(At(13, 29, 1), custom));
}));
tests.Add(("Saving changed custom rules rebases the pending minute", () =>
{
    var original = settings with { IntervalMinutes = 0, HourlyReminderMinutes = "45" };
    var engine = new ScheduleEngine(At(13, 28), original);
    var changed = original with { HourlyReminderMinutes = "29" };
    engine.Recalculate(At(13, 28, 50), changed);
    Equal<DateTime?>(At(13, 29), engine.Poll(At(13, 29, 1), changed));
}));
tests.Add(("Empty custom-only rules and rules outside active window are rejected", () =>
{
    foreach (var custom in new[]
    {
        settings with { IntervalMinutes = 0 },
        settings with { IntervalMinutes = 0, DailyReminderTimes = "01:29", ActiveHoursEnabled = true }
    })
    {
        var rejected = false;
        try { ScheduleEngine.NextReminder(At(12, 0), custom); } catch (InvalidOperationException) { rejected = true; }
        Assert(rejected);
        Equal<DateTime?>(null, new ScheduleEngine(At(12, 0), custom).Poll(At(13, 0, 1), custom));
    }
    var normalized = (settings with { IntervalMinutes = 0, DailyReminderTimes = "bad", HourlyReminderMinutes = "99" }).Normalize();
    Equal(0, normalized.IntervalMinutes); Equal("", normalized.DailyReminderTimes); Equal("", normalized.HourlyReminderMinutes);
}));
tests.Add(("Clock-only mode never schedules a reminder", () =>
{
    var clockOnly = settings with { KeepVisible = true, IntervalMinutes = 0 };
    Assert(!ReminderRules.HasRules(clockOnly));
    Equal<DateTime?>(null, new ScheduleEngine(At(12, 59), clockOnly).Poll(At(13, 0, 1), clockOnly));
    Assert(ReminderRules.HasRules(clockOnly with { DailyReminderTimes = "13:29" }));
    Assert(ReminderRules.HasRules(clockOnly with { HourlyReminderMinutes = "45" }));
}));
tests.Add(("Persistent display preserves reminder schedules and active hours", () =>
{
    var persistent = settings with { KeepVisible = true, IntervalMinutes = 0, HourlyReminderMinutes = "45",
        ActiveHoursEnabled = true, ActiveStart = "09:00", ActiveEnd = "22:00" };
    Equal(At(13, 45), ScheduleEngine.NextReminder(At(13, 30), persistent));
    var engine = new ScheduleEngine(At(21, 44), persistent);
    Equal<DateTime?>(At(21, 45), engine.Poll(At(21, 45, 1), persistent));
    Equal<DateTime?>(null, engine.Poll(At(22, 45, 1), persistent));
    Equal(At(9, 45).AddDays(1), ScheduleEngine.NextReminder(At(22, 30), persistent));
}));
var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
