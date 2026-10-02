namespace TimeOnScreen.Core;

internal static class CoreText
{
    internal static string SettingsUnreadable(AppLanguage language) => language == AppLanguage.Chinese
        ? "无法读取设置文件，已使用默认设置。原文件会保留至再次保存。"
        : "The settings file could not be read. Defaults are in use. The original file is kept until you save again.";
    internal static string DailyTime(string token, AppLanguage language) => language == AppLanguage.Chinese
        ? $"每天提醒时间“{token}”无效，请使用 24 小时制，例如 01:29 或 13:29。"
        : $"Invalid daily time '{token}'. Use 24-hour time, such as 01:29 or 13:29.";
    internal static string HourlyMinute(string token, AppLanguage language) => language == AppLanguage.Chinese
        ? $"每小时分钟“{token}”无效，请填 0～59 之间的整数，例如 45。"
        : $"Invalid hourly minute '{token}'. Enter a whole number from 0 to 59, such as 45.";
    internal static string NoRules(AppLanguage language) => language == AppLanguage.Chinese
        ? "请填写自定义提醒时间，或选择整点／半小时提醒。"
        : "Add a custom reminder time, or choose hourly or half-hourly reminders.";
    internal static string NoActiveRules(AppLanguage language) => language == AppLanguage.Chinese
        ? "指定时段内没有可用的提醒时间，请调整时段或提醒规则。"
        : "No reminders fall within these hours. Adjust the hours or reminder rules.";
}
