using System;
using System.Globalization;

namespace TimeOnScreen.Core;

public enum ClockPosition { UpperCenter, Center, LowerCenter, TopLeft, TopRight, BottomLeft, BottomRight }
public enum BlinkStyle { Smooth, Hard, Steady }
public enum AppLanguage { English, Chinese }

public sealed record ClockSettings
{
    public int Version { get; init; } = 1;
    public AppLanguage Language { get; init; } = AppLanguage.Chinese;
    public int IntervalMinutes { get; init; } = 60;
    public string DailyReminderTimes { get; init; } = "";
    public string HourlyReminderMinutes { get; init; } = "";
    public bool Use24HourClock { get; init; } = true;
    public bool ShowSeconds { get; init; } = true;
    public bool KeepVisible { get; init; }
    public bool SyncBlinkToSeconds { get; init; } = true;
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 160;
    public string Color { get; init; } = "#FFFFFF";
    public double Opacity { get; init; } = 0.8;
    public double DurationSeconds { get; init; } = 5;
    public double BlinkPeriodSeconds { get; init; } = 1;
    public BlinkStyle BlinkStyle { get; init; } = BlinkStyle.Smooth;
    public bool Shadow { get; init; } = true;
    public ClockPosition Position { get; init; } = ClockPosition.UpperCenter;
    public string Monitor { get; init; } = "primary";
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public bool ActiveHoursEnabled { get; init; }
    public string ActiveStart { get; init; } = "09:00";
    public string ActiveEnd { get; init; } = "22:00";
    public bool StartWithWindows { get; init; }

    public ClockSettings Normalize() => this with
    {
        Version = 1,
        Language = Enum.IsDefined(Language) ? Language : AppLanguage.Chinese,
        IntervalMinutes = IntervalMinutes is 0 or 30 or 60 ? IntervalMinutes : 60,
        DailyReminderTimes = ReminderRules.NormalizeDailyTimes(DailyReminderTimes, strict: false),
        HourlyReminderMinutes = ReminderRules.NormalizeHourlyMinutes(HourlyReminderMinutes, strict: false),
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily.Trim(),
        FontSize = ClampFinite(FontSize, 32, 400, 160),
        Color = IsColor(Color) ? Color.ToUpperInvariant() : "#FFFFFF",
        Opacity = ClampFinite(Opacity, 0.1, 1, 0.8),
        DurationSeconds = ClampFinite(DurationSeconds, 1, 30, 5),
        BlinkPeriodSeconds = ClampFinite(BlinkPeriodSeconds, 0.5, 4, 1),
        BlinkStyle = Enum.IsDefined(BlinkStyle) ? BlinkStyle : BlinkStyle.Smooth,
        Position = Enum.IsDefined(Position) ? Position : ClockPosition.UpperCenter,
        Monitor = string.IsNullOrWhiteSpace(Monitor) ? "primary" : Monitor,
        OffsetX = Math.Clamp(OffsetX, -4000, 4000),
        OffsetY = Math.Clamp(OffsetY, -4000, 4000),
        ActiveStart = TryTime(ActiveStart, out _) ? ActiveStart : "09:00",
        ActiveEnd = TryTime(ActiveEnd, out _) ? ActiveEnd : "22:00"
    };

    public bool IsActiveAt(DateTime localTime)
    {
        if (!ActiveHoursEnabled) return true;
        if (!TryTime(ActiveStart, out var start) || !TryTime(ActiveEnd, out var end)) return true;
        var time = TimeOnly.FromDateTime(localTime);
        if (start == end) return true;
        return start < end ? time >= start && time < end : time >= start || time < end;
    }

    public string FormatTime(DateTime time) => time.ToString(
        Use24HourClock ? (ShowSeconds ? "HH:mm:ss" : "HH:mm") : (ShowSeconds ? "h:mm:ss tt" : "h:mm tt"),
        CultureInfo.InvariantCulture);
    public static bool TryTime(string? value, out TimeOnly result) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    public static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' &&
        int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    private static double ClampFinite(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
