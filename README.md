# fraction-planner

A small full-stack exercise in domain-driven design: planning and tracking a course of treatment
sessions ("fractions") as an event-sourced aggregate, with a CQRS read model, an OpenAPI-described
REST API in .NET 8, a React and TypeScript single-page app, and Playwright end-to-end tests.

> This is a learning project with simplified, made-up scheduling rules. It is not clinical software
> and says nothing about how real treatment is planned.

## The domain

A course has a patient reference, a prescribed number of fractions and a start date.

- Sessions are planned one per weekday from the start date; weekends are skipped.
- Fractions are delivered in order, one per day, never on a weekend and never before the previous one.
- A missed fraction books a make-up session on the next weekday after the last planned session, so the
  prescribed number can still be reached.
- The course completes when every prescribed fraction has been delivered; after that it accepts no
  commands.

## How it is built

```
backend/FractionPlanner
  Domain/          TreatmentCourse aggregate, events, weekday rules
  Infrastructure/  in-memory event store with optimistic concurrency
  ReadModel/       projection that rebuilds course views from the event stream
  Api/             commands and minimal-API endpoints, OpenAPI via Swashbuckle
frontend/          React + TypeScript (Vite), built into the API's wwwroot
e2e/               Playwright tests against the running API and SPA
```

- **Write side.** A command loads the course's events, the aggregate checks its rules and returns new
  events, and the store appends them only if the stream is still at the version the decision was made
  against. State changes only by applying events, so the same history always rebuilds the same course.
- **Read side.** Queries never touch the aggregate. A projection subscribes to appended events and keeps
  a view per course; `GET /api/courses/{id}/events` returns the history as an audit trail.
- **API.** `POST /api/courses`, `GET /api/courses`, `GET /api/courses/{id}`,
  `POST /api/courses/{id}/sessions/{n}/deliver`, `POST /api/courses/{id}/sessions/{n}/miss`.
  Invalid input is a 400, a broken rule is a 409 with the reason, and the OpenAPI document is at
  `/swagger/v1/swagger.json` (Swagger UI at `/swagger`).

## Running it

```bash
cd frontend && npm install && npm run build && cd ..
dotnet run --project backend/FractionPlanner --urls http://localhost:5080
# open http://localhost:5080
```

## Tests

```bash
dotnet test backend/FractionPlanner.Tests/FractionPlanner.Tests.csproj   # domain + API (WebApplicationFactory)
cd e2e && npm install && npx playwright install chromium && npx playwright test
```

CI runs both: the xUnit suite, then the SPA build and the Playwright tests against the real server.

## Not in scope

Persistence beyond memory, authentication, multiple users editing the same course (the concurrency
check is there, the UI does not surface it), and anything clinical.
