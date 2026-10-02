using System;

namespace TimeOnScreen.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public static class OverlayPlacement
{
    public static PixelRect Place(PixelRect area, int width, int height, ClockPosition position, int offsetX, int offsetY)
    {
        width = Math.Clamp(width, 1, Math.Max(1, area.Width));
        height = Math.Clamp(height, 1, Math.Max(1, area.Height));
        var marginX = Math.Min(32, Math.Max(0, (area.Width - width) / 2));
        var marginY = Math.Min(32, Math.Max(0, (area.Height - height) / 2));
        var x = position switch
        {
            ClockPosition.TopLeft or ClockPosition.BottomLeft => area.X + marginX,
            ClockPosition.TopRight or ClockPosition.BottomRight => area.X + area.Width - width - marginX,
            _ => area.X + (area.Width - width) / 2
        };
        var y = position switch
        {
            ClockPosition.TopLeft or ClockPosition.TopRight => area.Y + marginY,
            ClockPosition.BottomLeft or ClockPosition.BottomRight => area.Y + area.Height - height - marginY,
            ClockPosition.UpperCenter => area.Y + (int)((area.Height - height) * 0.24),
            ClockPosition.LowerCenter => area.Y + (int)((area.Height - height) * 0.76),
            _ => area.Y + (area.Height - height) / 2
        };
        return new PixelRect(Math.Clamp(x + offsetX, area.X, area.X + area.Width - width),
            Math.Clamp(y + offsetY, area.Y, area.Y + area.Height - height), width, height);
    }
}
