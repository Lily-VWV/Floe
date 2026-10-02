using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TimeOnScreen.Core;

namespace TimeOnScreen;

internal static class UiText
{
    private static readonly Dictionary<string, (string English, string Chinese)> Text = new()
    {
        ["ProductName"] = ("Floe", "浮光"),
        ["SettingsTitle"] = ("Floe · Settings", "浮光 · 设置"),
        ["Settings"] = ("Settings", "设置"),
        ["Display"] = ("Display", "显示"),
        ["TimeFormat"] = ("Time format", "时间格式"),
        ["AlwaysVisible"] = ("Always visible", "常驻显示"),
        ["AlwaysVisibleTip"] = ("Keep the clock visible, flash at reminder times, then return to a steady display. Pausing reminders keeps the clock visible.", "平时稳定显示，到点闪烁后恢复常驻；暂停提醒仍显示时钟"),
        ["ShowSeconds"] = ("Show seconds", "显示秒数"),
        ["Reminders"] = ("Reminders", "提醒"),
        ["Interval"] = ("Repeat", "固定周期"),
        ["DurationLabel"] = ("Duration (seconds)", "持续时间（秒）"),
        ["Duration"] = ("Duration", "持续时间"),
        ["DurationName"] = ("Reminder duration", "提醒持续时间"),
        ["DurationTip"] = ("1–30 seconds; applies to reminders and previews", "1～30 秒，只影响提醒和预览"),
        ["DailyLabel"] = ("Daily times (24-hour)", "每日时间（24 小时制）"),
        ["DailyName"] = ("Daily reminder times", "每日提醒时间"),
        ["DailyTip"] = ("Remind at these times each day. Separate times with commas; leave blank to disable.", "每天在这些时间提醒；多个时间用逗号分隔，留空不设置"),
        ["HourlyLabel"] = ("Minutes past each hour", "每小时分钟"),
        ["HourlyName"] = ("Hourly reminder minutes", "每小时提醒分钟"),
        ["HourlyTip"] = ("Remind at these minutes each hour (0–59). Separate minutes with commas; leave blank to disable.", "每小时的这些分钟提醒；范围 0～59，多个分钟用逗号分隔，留空不设置"),
        ["LimitHours"] = ("Limit reminder hours", "限定提醒时段"),
        ["StartTip"] = ("Start time, in 24-hour format", "开始时间，24 小时制"),
        ["StartName"] = ("Reminder start time", "提醒开始时间"),
        ["EndTip"] = ("End time; may be after midnight", "结束时间，可以跨午夜"),
        ["EndName"] = ("Reminder end time", "提醒结束时间"),
        ["Appearance"] = ("Appearance", "外观"),
        ["Font"] = ("Font", "字体"),
        ["Color"] = ("Color", "颜色"),
        ["ColorPicker"] = ("Color picker", "色盘选色"),
        ["ColorName"] = ("Digit color", "数字颜色"),
        ["ColorTip"] = ("Hex color, such as #FFFFFF", "十六进制颜色，例如 #FFFFFF"),
        ["FontSize"] = ("Font size", "字号"),
        ["FontSizeTip"] = ("32–400; use arrow keys for fine adjustment", "32～400，方向键可微调"),
        ["Opacity"] = ("Opacity", "不透明度"),
        ["OpacityName"] = ("Digit opacity", "数字不透明度"),
        ["BlinkStyle"] = ("Blink style", "闪烁方式"),
        ["BlinkStyleName"] = ("Reminder blink style", "提醒闪烁方式"),
        ["BlinkPeriodLabel"] = ("Blink period (seconds)", "闪烁周期（秒）"),
        ["BlinkPeriod"] = ("Blink period", "闪烁周期"),
        ["BlinkPeriodTip"] = ("0.5–4 seconds; fixed to one second when synced to seconds", "0.5～4 秒；开启每秒同步后固定为每秒一个周期"),
        ["SyncSeconds"] = ("Sync to seconds", "每秒同步"),
        ["SyncSecondsTip"] = ("Brighten at the start of each second and complete one blink per second", "数字换秒时变亮，每秒完成一次闪烁"),
        ["TextShadow"] = ("Text shadow", "文字阴影"),
        ["MoreSettings"] = ("More settings", "更多设置"),
        ["Monitor"] = ("Display", "显示器"),
        ["Position"] = ("Position", "位置"),
        ["PositionName"] = ("Screen position", "屏幕位置"),
        ["OffsetX"] = ("Horizontal offset", "水平偏移"),
        ["OffsetXTip"] = ("Positive moves right; negative moves left. Range: −4000 to 4000.", "正值向右，负值向左；范围 -4000～4000"),
        ["OffsetY"] = ("Vertical offset", "垂直偏移"),
        ["OffsetYTip"] = ("Positive moves down; negative moves up. Range: −4000 to 4000.", "正值向下，负值向上；范围 -4000～4000"),
        ["Startup"] = ("Start with Windows", "开机启动"),
        ["StartupTip"] = ("Run in the background when you sign in to Windows", "登录 Windows 后在后台运行"),
        ["Pause"] = ("Pause reminders", "暂停提醒"),
        ["Resume"] = ("Resume reminders", "恢复提醒"),
        ["Preview"] = ("Preview", "预览"),
        ["PreviewTip"] = ("Preview the current form without saving changes", "按当前表单预览，不保存修改"),
        ["Save"] = ("Save", "保存"),
        ["Exit"] = ("Exit", "退出"),
        ["LanguageName"] = ("Language", "语言"),
        ["LanguageTip"] = ("Choose a language. Click Save to remember your choice.", "选择界面语言，点击保存后记住选择"),
        ["Hourly"] = ("Every hour", "每个整点"),
        ["HalfHourly"] = ("Every half hour", "每半小时"),
        ["NoInterval"] = ("No fixed interval", "不使用固定周期"),
        ["Format24"] = ("24-hour", "24 小时制"),
        ["Format12"] = ("12-hour", "12 小时制"),
        ["Smooth"] = ("Smooth", "柔和闪烁"),
        ["Hard"] = ("On / off", "明灭闪烁"),
        ["Steady"] = ("No blink", "不闪烁"),
        ["UpperCenter"] = ("Upper center", "中央偏上"),
        ["Center"] = ("Center", "屏幕中央"),
        ["LowerCenter"] = ("Lower center", "中央偏下"),
        ["TopLeft"] = ("Top left", "左上角"),
        ["TopRight"] = ("Top right", "右上角"),
        ["BottomLeft"] = ("Bottom left", "左下角"),
        ["BottomRight"] = ("Bottom right", "右下角"),
        ["PrimaryMonitor"] = ("Primary display", "主显示器"),
        ["CursorMonitor"] = ("Display under pointer", "鼠标所在显示器"),
        ["MonitorChoice"] = ("Display {0} · {1}×{2}{3}", "显示器 {0} · {1}×{2}{3}"),
        ["PrimarySuffix"] = (" · Primary", " · 主屏"),
        ["IntegerOffsets"] = ("Enter whole numbers for position offsets.", "位置偏移请填写整数。"),
        ["InvalidColor"] = ("Enter a color as #RRGGBB, such as #FFFFFF.", "颜色请填写 #RRGGBB，例如 #FFFFFF。"),
        ["MissingFont"] = ("Choose or enter a font.", "请选择或填写字体。"),
        ["InvalidHours"] = ("Use HH:mm for reminder hours, such as 09:00.", "提醒时段请使用 HH:mm 格式，例如 09:00。"),
        ["EqualHours"] = ("Start and end times must differ. Turn off the hour limit for all-day reminders.", "开始时间与结束时间需要不同；全天提醒请取消时段限制。"),
        ["NumberRange"] = ("{0}: enter a number from {1} to {2}.", "{0}请填写 {1}～{2} 之间的数字。"),
        ["Previewing"] = ("Previewing", "正在预览"),
        ["Saved"] = ("Saved", "已保存"),
        ["SaveFailed"] = ("Could not save: ", "保存失败："),
        ["Paused"] = ("Reminders paused", "提醒已暂停"),
        ["Enabled"] = ("Reminders on", "提醒已开启"),
        ["ClockContinues"] = ("Clock remains visible", "时钟继续显示"),
        ["ClockOnly"] = ("Clock only", "仅显示时钟"),
        ["NextReminder"] = ("Next reminder: {0:MM-dd HH:mm}", "下次提醒：{0:MM-dd HH:mm}"),
        ["NoReminders"] = ("No valid reminders", "未设置有效提醒"),
        ["TrayPaused"] = ("Floe · Reminders paused", "浮光 · 提醒已暂停"),
        ["UnexpectedError"] = ("An error was written to the local log. Try Preview again or restart Floe.", "遇到错误，详情已记录到本地日志。请重新预览或重启应用。"),
        ["StartupReadError"] = ("Could not read the startup setting. Other features are available.", "无法读取开机启动状态；其他功能仍可使用。"),
        ["StoreNotReady"] = ("Settings storage is not initialized.", "设置管理尚未初始化。"),
        ["MissingExecutable"] = ("Could not find the application path.", "无法获取程序路径。"),
        ["StartupExecutable"] = ("Use TimeOnScreen.exe from the published folder to enable startup.", "请使用发布目录里的 TimeOnScreen.exe 设置开机启动。"),
        ["PaletteTip"] = ("Drag to choose a color. Arrow keys fine-tune; hold Shift for larger steps.", "拖动选色；方向键微调，按住 Shift 加快调节"),
        ["PaletteName"] = ("Saturation and brightness palette", "饱和度与明暗选色盘"),
        ["Hue"] = ("Hue", "色相")
    };

    internal static AppLanguage Language { get; private set; } = AppLanguage.Chinese;
    internal static string Get(string key) => Language == AppLanguage.Chinese ? Text[key].Chinese : Text[key].English;
    internal static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    internal static void SetLanguage(AppLanguage language)
    {
        Language = Enum.IsDefined(language) ? language : AppLanguage.Chinese;
        foreach (var key in Text.Keys) Application.Current.Resources[key] = Get(key);
        Application.Current.Resources["UiFont"] = new FontFamily(Language == AppLanguage.Chinese ? "Microsoft YaHei UI" : "Segoe UI");
    }
}
