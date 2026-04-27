namespace ClaudeUsageWidgetProvider;

internal static class Pace
{

    public static int CountWorkingDays(DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from) return 0;
        int count = 0;
        var d = from.Date;
        var end = to.Date;
        while (d <= end)
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
            d = d.AddDays(1);
        }
        return count;
    }

    /// <summary>
    /// Working days remaining in the month, with the current day counted as a fraction
    /// based on what portion of the workday window (09:00–17:00 local) is still ahead.
    /// </summary>
    public static double WorkingDaysRemainingFractional(DateTimeOffset now, DateTimeOffset monthEnd)
    {
        double todayCapacity = TodayRemainingCapacity(now);

        int fullDays = 0;
        var d = now.Date.AddDays(1);
        var end = monthEnd.Date.AddDays(-1); // monthEnd is start of next month
        while (d <= end)
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                fullDays++;
            d = d.AddDays(1);
        }
        return todayCapacity + fullDays;
    }

    /// <summary>
    /// Calendar days remaining (incl. weekends), with current day as fraction of 24h.
    /// </summary>
    public static double CalendarDaysRemainingFractional(DateTimeOffset now, DateTimeOffset monthEnd)
    {
        double todayFraction = 1.0 - (now.Hour + now.Minute / 60.0) / 24.0;
        if (todayFraction < 0) todayFraction = 0;
        if (todayFraction > 1) todayFraction = 1;

        int fullDays = 0;
        var d = now.Date.AddDays(1);
        var end = monthEnd.Date.AddDays(-1);
        while (d <= end) { fullDays++; d = d.AddDays(1); }
        return todayFraction + fullDays;
    }

    private static double TodayRemainingCapacity(DateTimeOffset now)
    {
        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            return 0;
        var settings = SettingsStore.Instance;
        double start = settings.WorkdayStartHour;
        double end = settings.WorkdayEndHour;
        if (end <= start) return 0; // misconfigured
        double currentHour = now.Hour + now.Minute / 60.0;
        if (currentHour <= start) return 1.0;
        if (currentHour >= end) return 0;
        return (end - currentHour) / (end - start);
    }
}
