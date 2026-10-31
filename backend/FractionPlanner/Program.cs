using FractionPlanner.Api;
using FractionPlanner.Infrastructure;
using FractionPlanner.ReadModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryEventStore>();
builder.Services.AddSingleton<CourseProjection>();
builder.Services.AddSingleton<CourseCommands>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new() { Title = "fraction-planner", Version = "v1" }));

var app = builder.Build();

// The projection subscribes to the store when it is created, so create it before any command runs.
app.Services.GetRequiredService<CourseProjection>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapCourseEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program
{
}
