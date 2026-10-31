using FractionPlanner.Domain;
using FractionPlanner.Infrastructure;

namespace FractionPlanner.Api;

/// <summary>Command side: load the stream, let the aggregate decide, append what it returned.</summary>
public sealed class CourseCommands(InMemoryEventStore store, TimeProvider clock)
{
    public Guid Create(string? patientRef, int fractions, DateOnly start)
    {
        var id = Guid.NewGuid();
        store.Append(id, 0, TreatmentCourse.Create(id, patientRef, fractions, start, clock.GetUtcNow()));
        return id;
    }

    public void Deliver(Guid id, int number, DateOnly? date)
    {
        var course = Load(id);
        store.Append(id, course.Version, course.Deliver(number, date, clock.GetUtcNow()));
    }

    public void Miss(Guid id, int number)
    {
        var course = Load(id);
        store.Append(id, course.Version, course.Miss(number, clock.GetUtcNow()));
    }

    private TreatmentCourse Load(Guid id)
    {
        var history = store.Load(id);
        if (history.Count == 0)
            throw new CourseNotFoundException(id);
        return TreatmentCourse.FromHistory(history);
    }
}
