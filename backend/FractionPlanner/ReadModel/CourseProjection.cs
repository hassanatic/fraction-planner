using System.Collections.Concurrent;
using FractionPlanner.Domain;
using FractionPlanner.Infrastructure;

namespace FractionPlanner.ReadModel;

public sealed record SessionView(int Number, DateOnly Date, string Status);

public sealed record CourseView(
    Guid Id,
    string PatientRef,
    int Prescribed,
    int Delivered,
    int Missed,
    string Status,
    IReadOnlyList<SessionView> Sessions);

public sealed record EventView(string Type, DateTimeOffset At, string Summary);

/// <summary>
/// Query side. Views are rebuilt from the event stream whenever events are appended, so reads never
/// touch the aggregate and the write model stays free to change shape.
/// </summary>
public sealed class CourseProjection
{
    private readonly ConcurrentDictionary<Guid, CourseView> _views = new();
    private readonly InMemoryEventStore _store;

    public CourseProjection(InMemoryEventStore store)
    {
        _store = store;
        store.Appended += events =>
        {
            foreach (var id in events.Select(e => e.CourseId).Distinct())
            {
                _views[id] = ToView(TreatmentCourse.FromHistory(_store.Load(id)));
            }
        };
    }

    public CourseView? Get(Guid id) => _views.TryGetValue(id, out var view) ? view : null;

    public IReadOnlyList<CourseView> All() =>
        _views.Values.OrderBy(v => v.PatientRef, StringComparer.Ordinal).ToList();

    public IReadOnlyList<EventView> History(Guid id) =>
        _store.Load(id).Select(Describe).ToList();

    private static CourseView ToView(TreatmentCourse course)
    {
        var status = course.IsCompleted ? "completed"
            : course.Delivered > 0 || course.Missed > 0 ? "in progress"
            : "not started";
        return new CourseView(
            course.Id,
            course.PatientRef,
            course.Prescribed,
            course.Delivered,
            course.Missed,
            status,
            course.Sessions.Select(s => new SessionView(s.Number, s.Date, s.Status.ToString().ToLowerInvariant())).ToList());
    }

    private static EventView Describe(CourseEvent e) => e switch
    {
        CourseCreated c => new EventView(nameof(CourseCreated), c.At, $"{c.PrescribedFractions} fractions planned from {c.PlannedDates[0]:yyyy-MM-dd}"),
        FractionDelivered d => new EventView(nameof(FractionDelivered), d.At, $"session {d.SessionNumber} delivered on {d.Date:yyyy-MM-dd}"),
        FractionMissed m => new EventView(nameof(FractionMissed), m.At, $"session {m.SessionNumber} missed"),
        MakeupSessionScheduled s => new EventView(nameof(MakeupSessionScheduled), s.At, $"session {s.SessionNumber} booked for {s.Date:yyyy-MM-dd}"),
        CourseCompleted c => new EventView(nameof(CourseCompleted), c.At, "all prescribed fractions delivered"),
        _ => new EventView(e.GetType().Name, e.At, ""),
    };
}
