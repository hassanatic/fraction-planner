using FractionPlanner.Domain;
using FractionPlanner.ReadModel;

namespace FractionPlanner.Api;

public sealed record CreateCourseRequest(string? PatientRef, int PrescribedFractions, DateOnly? StartDate);

public sealed record DeliverRequest(DateOnly? Date);

public sealed record CreatedCourse(Guid Id);

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/courses").WithTags("Courses");

        api.MapGet("", (CourseProjection views) => Results.Ok(views.All()))
            .WithName("ListCourses")
            .Produces<IReadOnlyList<CourseView>>();

        api.MapGet("/{id:guid}", (Guid id, CourseProjection views) =>
                views.Get(id) is { } view ? Results.Ok(view) : Results.NotFound())
            .WithName("GetCourse")
            .Produces<CourseView>()
            .Produces(StatusCodes.Status404NotFound);

        api.MapGet("/{id:guid}/events", (Guid id, CourseProjection views) =>
                views.Get(id) is null ? Results.NotFound() : Results.Ok(views.History(id)))
            .WithName("GetCourseEvents")
            .Produces<IReadOnlyList<EventView>>();

        api.MapPost("", (CreateCourseRequest request, CourseCommands commands, CourseProjection views, TimeProvider clock) =>
                Handle(() =>
                {
                    var start = request.StartDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                    var id = commands.Create(request.PatientRef, request.PrescribedFractions, start);
                    return Results.Created($"/api/courses/{id}", views.Get(id));
                }))
            .WithName("CreateCourse")
            .Produces<CourseView>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        api.MapPost("/{id:guid}/sessions/{number:int}/deliver", (Guid id, int number, DeliverRequest? request, CourseCommands commands, CourseProjection views) =>
                Handle(() =>
                {
                    commands.Deliver(id, number, request?.Date);
                    return Results.Ok(views.Get(id));
                }))
            .WithName("DeliverFraction")
            .Produces<CourseView>()
            .Produces(StatusCodes.Status409Conflict);

        api.MapPost("/{id:guid}/sessions/{number:int}/miss", (Guid id, int number, CourseCommands commands, CourseProjection views) =>
                Handle(() =>
                {
                    commands.Miss(id, number);
                    return Results.Ok(views.Get(id));
                }))
            .WithName("MissFraction")
            .Produces<CourseView>()
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static IResult Handle(Func<IResult> action)
    {
        try
        {
            return action();
        }
        catch (ValidationException e)
        {
            return Results.BadRequest(new { error = e.Message });
        }
        catch (DomainException e)
        {
            return Results.Conflict(new { error = e.Message });
        }
        catch (ConcurrencyException e)
        {
            return Results.Conflict(new { error = e.Message });
        }
        catch (CourseNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
