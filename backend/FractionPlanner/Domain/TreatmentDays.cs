namespace FractionPlanner.Domain;

/// <summary>Sessions run on weekdays; weekends are skipped when planning and rebooking.</summary>
public static class TreatmentDays
{
    public static bool IsTreatmentDay(DateOnly day) =>
        day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static DateOnly OnOrAfter(DateOnly day)
    {
        while (!IsTreatmentDay(day))
        {
            day = day.AddDays(1);
        }

        return day;
    }

    public static DateOnly After(DateOnly day) => OnOrAfter(day.AddDays(1));

    public static IReadOnlyList<DateOnly> Plan(DateOnly start, int count)
    {
        var days = new List<DateOnly>(count);
        var day = OnOrAfter(start);
        for (var i = 0; i < count; i++)
        {
            days.Add(day);
            day = After(day);
        }

        return days;
    }
}
