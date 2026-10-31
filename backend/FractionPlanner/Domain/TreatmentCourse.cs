namespace FractionPlanner.Domain;

public enum SessionStatus
{
    Planned,
    Delivered,
    Missed,
}

public sealed record Session(int Number, DateOnly Date, SessionStatus Status);

/// <summary>
/// Event-sourced aggregate for a course of fractions. Command methods check the rules against the
/// current state and return the events to append; state only ever changes by applying events, so
/// the same history always rebuilds the same course.
/// </summary>
public sealed class TreatmentCourse
{
    public const int MaxFractions = 60;

    private readonly List<Session> _sessions = new();

    public Guid Id { get; private set; }

    public string PatientRef { get; private set; } = "";

    public int Prescribed { get; private set; }

    public bool IsCompleted { get; private set; }

    /// <summary>Number of events applied; used for optimistic concurrency on append.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<Session> Sessions => _sessions;

    public int Delivered => _sessions.Count(s => s.Status == SessionStatus.Delivered);

    public int Missed => _sessions.Count(s => s.Status == SessionStatus.Missed);

    public static TreatmentCourse FromHistory(IEnumerable<CourseEvent> history)
    {
        var course = new TreatmentCourse();
        foreach (var e in history)
        {
            course.Apply(e);
        }

        return course;
    }

    public static IReadOnlyList<CourseEvent> Create(
        Guid id, string? patientRef, int fractions, DateOnly start, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(patientRef))
            throw new ValidationException("patientRef is required");
        if (fractions is < 1 or > MaxFractions)
            throw new ValidationException($"prescribedFractions must be between 1 and {MaxFractions}");

        return new CourseEvent[]
        {
            new CourseCreated(id, now, patientRef.Trim(), fractions, TreatmentDays.Plan(start, fractions)),
        };
    }

    public IReadOnlyList<CourseEvent> Deliver(int number, DateOnly? date, DateTimeOffset now)
    {
        var session = NextPlanned(number);
        var day = date ?? session.Date;

        if (!TreatmentDays.IsTreatmentDay(day))
            throw new DomainException($"{day:yyyy-MM-dd} is not a treatment day");
        var lastDelivered = _sessions.Where(s => s.Status == SessionStatus.Delivered).Select(s => (DateOnly?)s.Date).Max();
        if (lastDelivered is { } previous && day <= previous)
            throw new DomainException($"a fraction was already delivered on or after {day:yyyy-MM-dd}");

        var events = new List<CourseEvent> { new FractionDelivered(Id, now, number, day) };
        if (Delivered + 1 == Prescribed)
            events.Add(new CourseCompleted(Id, now));
        return events;
    }

    public IReadOnlyList<CourseEvent> Miss(int number, DateTimeOffset now)
    {
        NextPlanned(number);
        var makeup = TreatmentDays.After(_sessions.Max(s => s.Date));
        return new CourseEvent[]
        {
            new FractionMissed(Id, now, number),
            new MakeupSessionScheduled(Id, now, _sessions.Count + 1, makeup),
        };
    }

    private Session NextPlanned(int number)
    {
        if (IsCompleted)
            throw new DomainException("the course is completed");
        var session = _sessions.FirstOrDefault(s => s.Number == number)
            ?? throw new DomainException($"session {number} does not exist");
        if (session.Status != SessionStatus.Planned)
            throw new DomainException($"session {number} is already {session.Status.ToString().ToLowerInvariant()}");
        var next = _sessions.First(s => s.Status == SessionStatus.Planned);
        if (next.Number != number)
            throw new DomainException($"session {next.Number} comes first");
        return session;
    }

    private void Apply(CourseEvent e)
    {
        switch (e)
        {
            case CourseCreated created:
                Id = created.CourseId;
                PatientRef = created.PatientRef;
                Prescribed = created.PrescribedFractions;
                _sessions.AddRange(created.PlannedDates.Select((day, i) => new Session(i + 1, day, SessionStatus.Planned)));
                break;
            case FractionDelivered delivered:
                Replace(delivered.SessionNumber, s => s with { Status = SessionStatus.Delivered, Date = delivered.Date });
                break;
            case FractionMissed missed:
                Replace(missed.SessionNumber, s => s with { Status = SessionStatus.Missed });
                break;
            case MakeupSessionScheduled makeup:
                _sessions.Add(new Session(makeup.SessionNumber, makeup.Date, SessionStatus.Planned));
                break;
            case CourseCompleted:
                IsCompleted = true;
                break;
        }

        Version++;
    }

    private void Replace(int number, Func<Session, Session> change)
    {
        var index = _sessions.FindIndex(s => s.Number == number);
        _sessions[index] = change(_sessions[index]);
    }
}
