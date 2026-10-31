using FractionPlanner.Domain;
using FractionPlanner.Infrastructure;

namespace FractionPlanner.Tests;

public class TreatmentCourseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 11, 2);

    private static TreatmentCourse NewCourse(int fractions = 5, DateOnly? start = null) =>
        TreatmentCourse.FromHistory(TreatmentCourse.Create(Guid.NewGuid(), "P-001", fractions, start ?? Monday, Now));

    [Fact]
    public void Plan_skips_weekends()
    {
        var friday = new DateOnly(2026, 10, 30);
        Assert.Equal(new[] { friday, Monday, Monday.AddDays(1) }, TreatmentDays.Plan(friday, 3));
        Assert.Equal(Monday, TreatmentDays.Plan(new DateOnly(2026, 10, 31), 1)[0]);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("  ", 5)]
    [InlineData("P-001", 0)]
    [InlineData("P-001", 61)]
    public void Create_rejects_invalid_input(string? patientRef, int fractions)
    {
        Assert.Throws<ValidationException>(() => TreatmentCourse.Create(Guid.NewGuid(), patientRef, fractions, Monday, Now));
    }

    [Fact]
    public void Sessions_must_be_delivered_in_order()
    {
        var course = NewCourse();
        var error = Assert.Throws<DomainException>(() => course.Deliver(2, null, Now));
        Assert.Equal("session 1 comes first", error.Message);
    }

    [Fact]
    public void A_delivered_session_cannot_be_delivered_again()
    {
        var id = Guid.NewGuid();
        var course = TreatmentCourse.FromHistory(
            TreatmentCourse.Create(id, "P-001", 5, Monday, Now)
                .Concat(new CourseEvent[] { new FractionDelivered(id, Now, 1, Monday) }));
        var error = Assert.Throws<DomainException>(() => course.Deliver(1, null, Now));
        Assert.Equal("session 1 is already delivered", error.Message);
    }

    [Fact]
    public void Two_fractions_cannot_be_delivered_on_the_same_day()
    {
        var id = Guid.NewGuid();
        var course = TreatmentCourse.FromHistory(
            TreatmentCourse.Create(id, "P-001", 5, Monday, Now)
                .Concat(new CourseEvent[] { new FractionDelivered(id, Now, 1, Monday) }));
        var error = Assert.Throws<DomainException>(() => course.Deliver(2, Monday, Now));
        Assert.Contains("already delivered on or after 2026-11-02", error.Message);
    }

    [Fact]
    public void Delivery_on_a_weekend_is_refused()
    {
        var error = Assert.Throws<DomainException>(() => NewCourse().Deliver(1, new DateOnly(2026, 11, 7), Now));
        Assert.Equal("2026-11-07 is not a treatment day", error.Message);
    }

    [Fact]
    public void A_missed_fraction_books_a_makeup_session_after_the_last_one()
    {
        var course = NewCourse();
        var events = course.Miss(1, Now);

        Assert.IsType<FractionMissed>(events[0]);
        var makeup = Assert.IsType<MakeupSessionScheduled>(events[1]);
        Assert.Equal(6, makeup.SessionNumber);
        Assert.Equal(new DateOnly(2026, 11, 9), makeup.Date); // the plan ends on Friday 6 November
    }

    [Fact]
    public void Course_completes_when_every_prescribed_fraction_is_delivered_despite_a_miss()
    {
        var id = Guid.NewGuid();
        var history = new List<CourseEvent>(TreatmentCourse.Create(id, "P-001", 2, Monday, Now));
        var course = TreatmentCourse.FromHistory(history);

        history.AddRange(course.Miss(1, Now));
        course = TreatmentCourse.FromHistory(history);
        history.AddRange(course.Deliver(2, null, Now));
        course = TreatmentCourse.FromHistory(history);
        var last = course.Deliver(3, null, Now);
        history.AddRange(last);
        course = TreatmentCourse.FromHistory(history);

        Assert.IsType<CourseCompleted>(last[^1]);
        Assert.True(course.IsCompleted);
        Assert.Equal(2, course.Delivered);
        Assert.Equal(1, course.Missed);
        Assert.Throws<DomainException>(() => course.Miss(3, Now));
    }

    [Fact]
    public void Replaying_the_same_history_gives_the_same_course()
    {
        var id = Guid.NewGuid();
        var history = new List<CourseEvent>(TreatmentCourse.Create(id, "P-001", 3, Monday, Now));
        history.AddRange(TreatmentCourse.FromHistory(history).Miss(1, Now));

        var first = TreatmentCourse.FromHistory(history);
        var second = TreatmentCourse.FromHistory(history);

        Assert.Equal(first.Sessions, second.Sessions);
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(3, first.Version);
    }

    [Fact]
    public void Event_store_refuses_an_append_decided_against_an_old_version()
    {
        var store = new InMemoryEventStore();
        var id = Guid.NewGuid();
        store.Append(id, 0, TreatmentCourse.Create(id, "P-001", 3, Monday, Now));

        Assert.Throws<ConcurrencyException>(() =>
            store.Append(id, 0, new CourseEvent[] { new FractionMissed(id, Now, 1) }));
        Assert.Single(store.Load(id));
    }
}
