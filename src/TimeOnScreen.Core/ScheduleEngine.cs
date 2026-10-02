using System;
using System.Collections.Generic;

namespace TimeOnScreen.Core;

// Work in local wall time. OS time/timezone changes explicitly rebase the schedule.
public sealed class ScheduleEngine
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);
    private DateTime _nextMinute;
    private bool[] _eligibleMinutes = new bool[1440];
    private DateTime _lastPoll;
    private readonly HashSet<DateTime> _announced = new();
    private readonly Queue<DateTime> _history = new();

    public ScheduleEngine(DateTime now, ClockSettings settings) => Recalculate(now, settings);

    public void Recalculate(DateTime now, ClockSettings settings)
    {
        _nextMinute = FloorMinute(now).AddMinutes(1);
        _eligibleMinutes = ReminderRules.EligibleMinutes(settings);
        _lastPoll = now;
    }

    public DateTime? Poll(DateTime now, ClockSettings settings, bool suppressed = false)
    {
        if (now < _lastPoll) Recalculate(now, settings);
        _lastPoll = now;
        if (now < _nextMinute) return null;

        var scheduled = FloorMinute(now);
        var earliestEligible = _nextMinute;
        _nextMinute = scheduled.AddMinutes(1);
        if (scheduled < earliestEligible || now - scheduled > Grace || suppressed ||
            !_eligibleMinutes[scheduled.Hour * 60 + scheduled.Minute] || !_announced.Add(scheduled)) return null;

        _history.Enqueue(scheduled);
        if (_history.Count > 2880) _announced.Remove(_history.Dequeue());
        return scheduled;
    }

    public static DateTime NextReminder(DateTime now, ClockSettings settings)
    {
        var eligible = ReminderRules.EligibleMinutes(settings);
        var candidate = FloorMinute(now).AddMinutes(1);
        // All rules repeat daily. A minute shared by multiple rules appears once.
        for (var i = 0; i < 1440; i++, candidate = candidate.AddMinutes(1))
            if (eligible[candidate.Hour * 60 + candidate.Minute]) return candidate;
        if (settings.IntervalMinutes == 0 && string.IsNullOrWhiteSpace(settings.DailyReminderTimes) && string.IsNullOrWhiteSpace(settings.HourlyReminderMinutes))
            throw new InvalidOperationException(CoreText.NoRules(settings.Language));
        throw new InvalidOperationException(CoreText.NoActiveRules(settings.Language));
    }

    private static DateTime FloorMinute(DateTime now) => new(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind);
}
