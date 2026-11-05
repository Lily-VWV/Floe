using System;

namespace TimeOnScreen.Core;

public enum PomodoroPhase { Idle, Work, Rest, Completed }

// Elapsed time comes from a monotonic clock, independent of wall-clock changes.
public sealed class PomodoroTimer
{
    public PomodoroPhase Phase { get; private set; }
    public int Round { get; private set; }
    public int Rounds { get; private set; }
    public TimeSpan Remaining { get; private set; }
    private TimeSpan _work;
    private TimeSpan _rest;
    public bool Running => Phase is PomodoroPhase.Work or PomodoroPhase.Rest;

    public void Start(ClockSettings settings)
    {
        settings = settings.Normalize();
        _work = TimeSpan.FromMinutes(settings.WorkMinutes);
        _rest = TimeSpan.FromMinutes(settings.RestMinutes);
        Rounds = settings.PomodoroRounds;
        Round = 1;
        Phase = PomodoroPhase.Work;
        Remaining = _work;
    }

    public void Stop() { Phase = PomodoroPhase.Idle; Remaining = TimeSpan.Zero; Round = 0; }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        while (Running && elapsed >= Remaining)
        {
            elapsed -= Remaining;
            if (Phase == PomodoroPhase.Work) { Phase = PomodoroPhase.Rest; Remaining = _rest; }
            else if (Round == Rounds) { Phase = PomodoroPhase.Completed; Remaining = TimeSpan.Zero; }
            else { Round++; Phase = PomodoroPhase.Work; Remaining = _work; }
        }
        if (Running) Remaining -= elapsed;
    }

    public string Countdown => $"{(int)Math.Ceiling(Remaining.TotalSeconds) / 60:00}:{(int)Math.Ceiling(Remaining.TotalSeconds) % 60:00}";
}
