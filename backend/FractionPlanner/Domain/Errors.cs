namespace FractionPlanner.Domain;

/// <summary>Input that can never be valid, whatever the course's state.</summary>
public sealed class ValidationException(string message) : Exception(message);

/// <summary>A command that breaks a rule given the course's current state.</summary>
public sealed class DomainException(string message) : Exception(message);

public sealed class CourseNotFoundException(Guid id) : Exception($"course {id} not found");

public sealed class ConcurrencyException(string message) : Exception(message);
