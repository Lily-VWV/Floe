using System;
using System.IO;
using Microsoft.Win32;

namespace TimeOnScreen;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "TimeOnScreen";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(Name) is string;
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException(UiText.Get("MissingExecutable"));
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(UiText.Get("StartupExecutable"));
            key.SetValue(Name, $"\"{executable}\" --background");
        }
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
