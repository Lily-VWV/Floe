using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeOnScreen.Core;

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string? LoadWarning { get; private set; }

    public ClockSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(path)) return new ClockSettings();
        try
        {
            return (JsonSerializer.Deserialize<ClockSettings>(File.ReadAllText(path), Options) ??
                throw new JsonException("Empty settings")).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            var defaults = new ClockSettings();
            LoadWarning = CoreText.SettingsUnreadable(defaults.Language);
            return defaults;
        }
    }

    public void Save(ClockSettings settings)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Settings path must have a directory");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), Options));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
