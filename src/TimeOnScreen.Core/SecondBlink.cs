using System;

namespace TimeOnScreen.Core;

public static class SecondBlink
{
    // Use the wall-clock second phase: each new second starts at full opacity.
    public static double Opacity(DateTime time, double maximum, BlinkStyle style)
    {
        var phase = (time.Ticks % TimeSpan.TicksPerSecond) / (double)TimeSpan.TicksPerSecond;
        return style switch
        {
            BlinkStyle.Hard => phase < 0.5 ? maximum : 0,
            BlinkStyle.Smooth => maximum * (0.15 + 0.85 * (1 + Math.Cos(2 * Math.PI * phase)) / 2),
            _ => maximum
        };
    }
}
