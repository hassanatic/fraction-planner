using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FractionPlanner.Api;
using FractionPlanner.ReadModel;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FractionPlanner.Tests;

public class ApiTests
{
    private static async Task<CourseView> CreateCourse(HttpClient client, int fractions = 5)
    {
        var response = await client.PostAsJsonAsync("/api/courses",
            new CreateCourseRequest("P-001", fractions, new DateOnly(2026, 11, 2)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CourseView>())!;
    }

    [Fact]
    public async Task Creating_a_course_returns_the_planned_sessions()
    {
        using var factory = new WebApplicationFactory<Program>();
        var course = await CreateCourse(factory.CreateClient());

        Assert.Equal(5, course.Sessions.Count);
        Assert.Equal("not started", course.Status);
        Assert.All(course.Sessions, s => Assert.Equal("planned", s.Status));
        Assert.Equal(new DateOnly(2026, 11, 6), course.Sessions[^1].Date);
    }

    [Fact]
    public async Task Invalid_input_is_a_bad_request()
    {
        using var factory = new WebApplicationFactory<Program>();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/courses",
            new CreateCourseRequest("", 5, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deliver_then_miss_updates_the_read_model()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var course = await CreateCourse(client);

        await client.PostAsJsonAsync($"/api/courses/{course.Id}/sessions/1/deliver", new DeliverRequest(null));
        var afterMiss = await (await client.PostAsync($"/api/courses/{course.Id}/sessions/2/miss", null))
            .Content.ReadFromJsonAsync<CourseView>();

        Assert.NotNull(afterMiss);
        Assert.Equal(1, afterMiss.Delivered);
        Assert.Equal(1, afterMiss.Missed);
        Assert.Equal("in progress", afterMiss.Status);
        Assert.Equal(6, afterMiss.Sessions.Count);
        Assert.Equal(new DateOnly(2026, 11, 9), afterMiss.Sessions[^1].Date);
    }

    [Fact]
    public async Task Breaking_a_rule_is_a_conflict_with_a_reason()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var course = await CreateCourse(client);

        var response = await client.PostAsJsonAsync($"/api/courses/{course.Id}/sessions/3/deliver", new DeliverRequest(null));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("session 1 comes first", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unknown_course_is_not_found()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/courses/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/courses/{Guid.NewGuid()}/sessions/1/miss", null)).StatusCode);
    }

    [Fact]
    public async Task The_event_history_is_an_audit_trail()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var course = await CreateCourse(client, fractions: 2);

        await client.PostAsync($"/api/courses/{course.Id}/sessions/1/miss", null);
        var events = await client.GetFromJsonAsync<List<EventView>>($"/api/courses/{course.Id}/events");

        Assert.Equal(new[] { "CourseCreated", "FractionMissed", "MakeupSessionScheduled" }, events!.Select(e => e.Type));
    }

    [Fact]
    public async Task OpenApi_document_describes_the_api()
    {
        using var factory = new WebApplicationFactory<Program>();
        var json = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"/api/courses\"", json);
        Assert.Contains("/api/courses/{id}/sessions/{number}/deliver", json);
        Assert.Contains("CourseView", json);
    }
}
