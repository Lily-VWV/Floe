using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TimeOnScreen.Core;

public static class ReminderRules
{
    private static readonly char[] Separators = { ',', '，', ';', '；', '、', ' ', '\t', '\r', '\n' };

    public static bool HasRules(ClockSettings settings) => settings.IntervalMinutes is 30 or 60 ||
        !string.IsNullOrWhiteSpace(settings.DailyReminderTimes) || !string.IsNullOrWhiteSpace(settings.HourlyReminderMinutes);

    public static int[] ParseDailyTimes(string? input, AppLanguage language = AppLanguage.Chinese)
    {
        var result = new SortedSet<int>();
        foreach (var token in Tokens(input))
        {
            var value = token.Replace('：', ':');
            if (!TimeOnly.TryParseExact(value, new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
                throw new ArgumentException(CoreText.DailyTime(token, language));
            result.Add(time.Hour * 60 + time.Minute);
        }
        return result.ToArray();
    }

    public static int[] ParseHourlyMinutes(string? input, AppLanguage language = AppLanguage.Chinese)
    {
        var result = new SortedSet<int>();
        foreach (var token in Tokens(input))
        {
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || minute is < 0 or > 59)
                throw new ArgumentException(CoreText.HourlyMinute(token, language));
            result.Add(minute);
        }
        return result.ToArray();
    }

    public static string NormalizeDailyTimes(string? input, bool strict = true, AppLanguage language = AppLanguage.Chinese)
    {
        try { return string.Join(", ", ParseDailyTimes(input, language).Select(minute => new TimeOnly(minute / 60, minute % 60).ToString("HH:mm", CultureInfo.InvariantCulture))); }
        catch (ArgumentException) when (!strict) { return ""; }
    }

    public static string NormalizeHourlyMinutes(string? input, bool strict = true, AppLanguage language = AppLanguage.Chinese)
    {
        try { return string.Join(", ", ParseHourlyMinutes(input, language).Select(minute => minute.ToString("00", CultureInfo.InvariantCulture))); }
        catch (ArgumentException) when (!strict) { return ""; }
    }

    internal static bool[] EligibleMinutes(ClockSettings settings)
    {
        var minutes = new bool[1440];
        if (settings.IntervalMinutes is 30 or 60)
            for (var minute = 0; minute < minutes.Length; minute += settings.IntervalMinutes) minutes[minute] = true;
        foreach (var minute in ParseDailyTimes(settings.DailyReminderTimes, settings.Language)) minutes[minute] = true;
        foreach (var minute in ParseHourlyMinutes(settings.HourlyReminderMinutes, settings.Language))
            for (var hour = 0; hour < 24; hour++) minutes[hour * 60 + minute] = true;
        if (settings.ActiveHoursEnabled)
        {
            var day = new DateTime(2000, 1, 1);
            for (var minute = 0; minute < minutes.Length; minute++)
                if (minutes[minute] && !settings.IsActiveAt(day.AddMinutes(minute))) minutes[minute] = false;
        }
        return minutes;
    }

    private static string[] Tokens(string? input) => string.IsNullOrWhiteSpace(input) ? Array.Empty<string>() :
        input.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
