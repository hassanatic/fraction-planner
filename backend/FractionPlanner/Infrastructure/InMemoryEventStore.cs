using FractionPlanner.Domain;

namespace FractionPlanner.Infrastructure;

/// <summary>
/// Append-only event streams, one per course, with optimistic concurrency: an append states the
/// version it was decided against and is refused if the stream moved on in the meantime.
/// </summary>
public sealed class InMemoryEventStore
{
    private readonly Dictionary<Guid, List<CourseEvent>> _streams = new();
    private readonly object _gate = new();

    public event Action<IReadOnlyList<CourseEvent>>? Appended;

    public IReadOnlyList<CourseEvent> Load(Guid courseId)
    {
        lock (_gate)
        {
            return _streams.TryGetValue(courseId, out var stream) ? stream.ToList() : new List<CourseEvent>();
        }
    }

    public void Append(Guid courseId, int expectedVersion, IReadOnlyList<CourseEvent> events)
    {
        lock (_gate)
        {
            _streams.TryGetValue(courseId, out var stream);
            var current = stream?.Count ?? 0;
            if (current != expectedVersion)
                throw new ConcurrencyException($"course {courseId} is at version {current}, not {expectedVersion}");
            if (stream is null)
            {
                stream = new List<CourseEvent>();
                _streams[courseId] = stream;
            }

            stream.AddRange(events);
        }

        Appended?.Invoke(events);
    }
}
