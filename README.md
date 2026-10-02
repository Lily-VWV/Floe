# Floe

English | [简体中文](README.zh-CN.md)

A transparent desktop clock for Windows. Keep the time visible or show blinking digits at scheduled times, without taking focus or blocking mouse clicks.

Simplified Chinese is the default language. English is available from the language selector at the top of Settings. Click **Save** / **保存** to remember your choice.

## Features

- Live time with optional seconds and 12-hour or 24-hour display.
- Hourly, half-hourly, daily custom times, and custom minutes past every hour.
- An always-visible clock that briefly blinks for reminders, then returns to a steady display.
- Smooth, on/off, or steady reminders, with optional synchronization to seconds.
- A color picker, HEX input, font selection, a 32–400 font size slider, opacity, and text shadow.
- Display selection, screen position, offsets, reminder hours, and optional startup with Windows.
- English and Simplified Chinese interfaces, including tray menus, tooltips, and validation messages.

## Run

Requires Windows x64. Download [Floe v0.7.1](https://github.com/Lily-VWV/Floe/releases/tag/v0.7.1), extract one of the following ZIPs, and run `TimeOnScreen.exe` from the extracted folder:

| Download | Requirement |
| --- | --- |
| [Floe-v0.7.1-win-x64.zip](https://github.com/Lily-VWV/Floe/releases/download/v0.7.1/Floe-v0.7.1-win-x64.zip) | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0); keep all extracted files together |
| [Floe-v0.7.1-win-x64-standalone.zip](https://github.com/Lily-VWV/Floe/releases/download/v0.7.1/Floe-v0.7.1-win-x64-standalone.zip) | Includes the runtime; no separate .NET installation required |

Both ZIPs include English and Chinese usage guides, the MIT license, and notices for included .NET components. `SHA256SUMS.txt` on the release page lists the download checksums. The automatic **Source code** downloads are for building the application, rather than running it directly.

To build from source, follow the instructions below. Local outputs are:

| Version | Executable | Requirement |
| --- | --- | --- |
| Framework-dependent | `artifacts/app/TimeOnScreen.exe` | .NET 10 Desktop Runtime; keep all files in the folder together |
| Self-contained | `artifacts/standalone/TimeOnScreen.exe` | Includes the runtime; the executable can be copied on its own |

The self-contained executable is larger and extracts runtime components on first launch. Keep the license and notice files when redistributing either version. Release executables are excluded from source control.

1. Open the executable to show Settings.
2. Adjust display, reminders, and appearance. **Preview** uses the current form without saving.
3. Click **Save** to apply changes. Display selection, position, offsets, and startup are under **More settings**.
4. Close Settings to keep Floe in the system tray. Double-click its tray icon to reopen Settings; right-click to preview, pause, or exit.

The default reminder appears every hour for five seconds: white digits, 160-point font, 80% opacity, upper-center position, 24-hour time with seconds, and smooth blinking synchronized to each second.

## Reminder rules

- **Repeat:** every hour, every half hour, or no fixed interval.
- **Daily times:** `01:29, 13:29` reminds every day at those times. Always enter daily times in 24-hour format, independently of the clock display format.
- **Minutes past each hour:** `45` reminds at minute 45 every hour; `15, 45` reminds twice per hour. Values must be whole numbers from 0 to 59.
- Separate multiple entries with commas. Saving sorts and removes duplicates. Blank inputs disable that rule.
- Rules can be combined. Overlapping times produce one reminder.
- To remind only at minute 45, choose **No fixed interval**, leave Daily times blank, enter `45` for Minutes past each hour, then save.
- **Limit reminder hours** applies to all rules and supports overnight periods. The end time is excluded. At least one reminder must fall within the selected hours.

Reminders trigger at second zero of their scheduled minute. The digits show the current time and continue updating while visible. Floe waits for the next scheduled time after startup or saving; it does not replay missed reminders. After sleep, only a reminder within the current minute's five-second grace period may fire. Locking or suspending hides the clock and suppresses reminders; unlocking or resuming restores an enabled persistent clock.

## Always visible

Enable **Always visible** and save to keep a steady clock on screen. At a reminder time, it uses the selected blink style for the configured duration, then returns to its steady display. Appearance and position settings apply to both modes.

For a clock without reminders, enable Always visible, choose No fixed interval, leave both custom rule inputs blank, and save. Reminder hours do not limit the persistent clock. Pausing reminders keeps it visible; pause lasts until the application exits.

## Appearance and language

- Click the color swatch to open the palette. Drag to choose saturation and brightness; the rainbow slider adjusts hue. You can also enter `#RRGGBB`. Click outside or press Esc to close it.
- Font size ranges from 32 to 400; arrow keys adjust it by one point. Use the bottom **Preview** button to see the actual desktop size.
- **Sync to seconds** starts a new blink with each system second. Turn it off to use a custom blink period. The persistent clock stays steady between reminders.
- Choose **English** or **简体中文** from the language selector. The interface and tray update immediately; click Save to persist the choice. Closing Settings without saving restores the saved language.
- New settings, settings without a language field, and invalid language values use Simplified Chinese. A previously saved language choice is retained, along with clock and reminder values.
- **Start with Windows** is off by default and does not require administrator privileges. After moving an application with startup enabled, turn this option off and on again to update its path.

Floe targets ordinary desktop windows. Exclusive full-screen applications, the secure desktop, and other topmost windows may obscure the clock.

## Settings and privacy

Settings are stored in `%LocalAppData%/TimeOnScreen/settings.json`; errors are logged to `errors.log` in the same folder. Unreadable settings fall back to defaults, with the original file preserved until the next save. The application uses no network services and collects no usage data.

The internal executable and storage names remain `TimeOnScreen` to preserve existing settings and startup entries. The English product name is **Floe**; the Chinese name is **浮光**.

## Build and verify

Requires Windows x64 and the .NET 10 SDK. No external NuGet libraries are used. Run from the repository root:

```powershell
./build.ps1                 # Build, run logic checks, publish the framework-dependent version
./build.ps1 -SelfContained  # Build, run logic checks, publish the self-contained version
```

Desktop integration checks require an interactive Windows session and PowerShell 7:

```powershell
pwsh -NoProfile -File ./verify.ps1
pwsh -NoProfile -File ./verify.ps1 -Package standalone
pwsh -NoProfile -File ./verify.ps1 -Package build
```

The desktop check opens test windows, briefly moves the mouse to verify palette dragging, and restores its position. It uses isolated settings and does not change normal user settings or startup registration. Lock and suspend checks simulate application states. Reports and screenshots are written to `artifacts/verification*`.

Checks cover scheduling, configuration recovery, language persistence and switching, localized tray and validation text, color selection, font size, previews, transparency, placement, click-through behavior, blinking, automatic hiding, and persistent-clock transitions.

## Project layout

- `src/TimeOnScreen.Core`: settings, reminder rules, scheduling, and placement.
- `src/TimeOnScreen`: WPF UI, localization, transparent overlay, tray, and system integration.
- `tests/TimeOnScreen.Tests`: dependency-free logic regression checks.

Window implementation references: [WPF windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/) and [layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features).

## License

Floe is licensed under the [MIT License](LICENSE). Copyright (c) 2026 Lily Ren.

Included .NET components retain their own licenses and notices in `licenses/`.
