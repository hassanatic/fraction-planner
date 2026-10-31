namespace FractionPlanner.Domain;

public abstract record CourseEvent(Guid CourseId, DateTimeOffset At);

public sealed record CourseCreated(
    Guid CourseId,
    DateTimeOffset At,
    string PatientRef,
    int PrescribedFractions,
    IReadOnlyList<DateOnly> PlannedDates) : CourseEvent(CourseId, At);

public sealed record FractionDelivered(Guid CourseId, DateTimeOffset At, int SessionNumber, DateOnly Date)
    : CourseEvent(CourseId, At);

public sealed record FractionMissed(Guid CourseId, DateTimeOffset At, int SessionNumber)
    : CourseEvent(CourseId, At);

public sealed record MakeupSessionScheduled(Guid CourseId, DateTimeOffset At, int SessionNumber, DateOnly Date)
    : CourseEvent(CourseId, At);

public sealed record CourseCompleted(Guid CourseId, DateTimeOffset At) : CourseEvent(CourseId, At);
