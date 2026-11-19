---
name: dotnet-vertical-slice
description: >-
  Guides the design and implementation of cohesive Vertical Slices in ASP.NET Core .NET 10 Minimal APIs.
  Enforces feature grouping under Features/, thin endpoints returning TypedResults, isolated Request/Response contracts,
  CQRS Command/Handler encapsulation without MediatR, and zero domain entity leaking to HTTP clients.
---

# Vertical Slice Architecture in ASP.NET Core .NET 10 Minimal APIs

This skill provides an authoritative guide for implementing features using **Vertical Slice Architecture** in ASP.NET Core .NET 10 Minimal APIs. 

In this architecture, code is organized around business use cases rather than technical layers. Every slice encapsulates its HTTP endpoint, input/output contracts, CQRS command or query, and orchestration handler.

---

## 1. Mandatory Implementation Principles

1. **Feature Folder per Use Case**: Each distinct operation resides in its own subfolder inside `Features/<FeatureGroup>/<UseCase>/`.
2. **Cohesive Colocation**: Request, Response, and Endpoint must be colocated in that specific use case folder.
3. **Command & Handler Integration**: If an operation mutates state, it must contain a `Command` record and a custom `Handler`.
4. **Thin Endpoints**: The endpoint route handler is strictly an HTTP adapter:
   - Receives and validates HTTP inputs.
   - Instantiates the Command or Query.
   - Invokes the Handler.
   - Transforms `Result<T>` or `Result` into typed HTTP responses via `TypedResults`.
5. **No Business Rules in Endpoints**: Business invariants belong exclusively to Domain Entities and Value Objects. Orchestration belongs to the Handler. Never execute calculations, validations, or state mutations in route delegates.
6. **Route Grouping with `MapGroup`**: Aggregate feature routes using `MapGroup("/api/events")` with OpenAPI tags and group-level metadata.
7. **Explicit TypedResults Return Types**: Declare exact return types using `Results<TSuccess, ProblemHttpResult>` (e.g. `Results<Created<CreateEventResponse>, ProblemHttpResult>`).
8. **Route Constraints**: Use constraints such as `{id:guid}` on route parameters to prevent invalid routing.
9. **Parameter-Injected Dependencies**: Inject handlers and services directly into Minimal API route delegate parameters, keeping closures clean.
10. **Propagate `CancellationToken`**: Accept `CancellationToken cancellationToken` in endpoints and forward it down through every async call.
11. **Contract Separation**: Response DTOs are distinct immutable records tailored to the use case.
12. **Never Return Domain or EF Core Entities**: Never expose Domain aggregates or EF Core entities directly to clients.

> [!NOTE]
> **Adaptability Rule**: While the structure below is standard, never rigidly impose a foreign naming format if the starter project already establishes a coherent vertical slice style (e.g. single-file slices vs. multi-file slices). Adapt to existing project conventions.

---

## 2. Standard Directory Layout

```text
Features/
└── Events/
    ├── CreateEvent/
    │   ├── CreateEventRequest.cs       # Input contract from HTTP client
    │   ├── CreateEventResponse.cs      # Output contract returned to client
    │   ├── CreateEventCommand.cs       # Internal CQRS command record
    │   ├── CreateEventHandler.cs       # Orchestrates domain, repository & UoW
    │   └── CreateEventEndpoint.cs      # Route definition & TypedResults mapping
    ├── GetEventById/
    │   ├── GetEventByIdResponse.cs     # Read-only response contract
    │   ├── GetEventByIdQuery.cs        # Internal CQRS query record
    │   ├── GetEventByIdHandler.cs      # Executes read query via repository
    │   └── GetEventByIdEndpoint.cs     # Route with {id:guid} constraint
    ├── PublishEvent/
    │   ├── PublishEventResponse.cs     # State transition output contract
    │   ├── PublishEventCommand.cs      # Transition command record
    │   ├── PublishEventHandler.cs      # Loads aggregate, calls event.Publish(), saves UoW
    │   └── PublishEventEndpoint.cs     # POST /{id:guid}/publish endpoint
    ├── Common/
    │   └── ResultToHttpExtensions.cs   # Maps Result/Error to ProblemHttpResult
    └── EventsEndpoints.cs              # Aggregates slices into MapGroup("/api/events")
```

---

## 3. Implementation Blueprints

### A. POST Create Slice (`CreateEvent`)

#### 1. Contracts & Command
```csharp
namespace MyApp.Features.Events.CreateEvent;

public sealed record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity);

public sealed record CreateEventResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateEventCommand(
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity);
```

#### 2. Custom Command Handler
```csharp
namespace MyApp.Features.Events.CreateEvent;

public sealed class CreateEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<CreateEventResponse>> HandleAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var titleResult = EventTitle.Create(command.Title);
        if (titleResult.IsFailure) return Result<CreateEventResponse>.Failure(titleResult.Error);

        var priceResult = Money.Create(command.TicketPrice, command.Currency);
        if (priceResult.IsFailure) return Result<CreateEventResponse>.Failure(priceResult.Error);

        var capacityResult = Capacity.Create(command.MaxCapacity);
        if (capacityResult.IsFailure) return Result<CreateEventResponse>.Failure(capacityResult.Error);

        var eventResult = Event.Create(
            titleResult.Value,
            command.Description,
            command.ScheduledAtUtc,
            priceResult.Value,
            capacityResult.Value,
            timeProvider.GetUtcNow());

        if (eventResult.IsFailure) return Result<CreateEventResponse>.Failure(eventResult.Error);

        var ev = eventResult.Value;
        await eventRepository.AddAsync(ev, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new CreateEventResponse(
            ev.Id,
            ev.Title.Value,
            ev.Description,
            ev.ScheduledAtUtc,
            ev.Price.Amount,
            ev.Price.Currency,
            ev.Capacity.Value,
            ev.Status.ToString(),
            ev.CreatedAtUtc);

        return Result<CreateEventResponse>.Success(response);
    }
}
```

#### 3. Endpoint Definition
```csharp
namespace MyApp.Features.Events.CreateEvent;

public static class CreateEventEndpoint
{
    public static RouteGroupBuilder MapCreateEventEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync)
            .WithName("CreateEvent")
            .WithSummary("Creates a new event")
            .Produces<CreateEventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    public static async Task<Results<Created<CreateEventResponse>, ProblemHttpResult>> HandleAsync(
        CreateEventRequest request,
        CreateEventHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateEventCommand(
            request.Title,
            request.Description,
            request.ScheduledAtUtc,
            request.TicketPrice,
            request.Currency,
            request.MaxCapacity);

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Created($"/api/events/{result.Value.Id}", result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
```

---

### B. GET By ID Slice with Route Constraint (`GetEventById`)

#### 1. Contracts & Query
```csharp
namespace MyApp.Features.Events.GetEventById;

public sealed record GetEventByIdResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset ScheduledAtUtc,
    decimal TicketPrice,
    string Currency,
    int MaxCapacity,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc);

public sealed record GetEventByIdQuery(Guid Id);
```

#### 2. Query Handler
```csharp
namespace MyApp.Features.Events.GetEventById;

public sealed class GetEventByIdHandler(IEventRepository eventRepository)
{
    public async Task<Result<GetEventByIdResponse>> HandleAsync(
        GetEventByIdQuery query,
        CancellationToken cancellationToken)
    {
        var ev = await eventRepository.GetByIdAsNoTrackingAsync(query.Id, cancellationToken);
        if (ev is null)
        {
            return Result<GetEventByIdResponse>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{query.Id}' was not found."));
        }

        var response = new GetEventByIdResponse(
            ev.Id,
            ev.Title.Value,
            ev.Description,
            ev.ScheduledAtUtc,
            ev.Price.Amount,
            ev.Price.Currency,
            ev.Capacity.Value,
            ev.Status.ToString(),
            ev.CreatedAtUtc,
            ev.PublishedAtUtc);

        return Result<GetEventByIdResponse>.Success(response);
    }
}
```

#### 3. Endpoint Definition with `{id:guid}`
```csharp
namespace MyApp.Features.Events.GetEventById;

public static class GetEventByIdEndpoint
{
    public static RouteGroupBuilder MapGetEventByIdEndpoint(this RouteGroupBuilder group)
    {
        // Enforce GUID route constraint {id:guid}
        group.MapGet("/{id:guid}", HandleAsync)
            .WithName("GetEventById")
            .WithSummary("Retrieves event details by ID")
            .Produces<GetEventByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<Results<Ok<GetEventByIdResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        GetEventByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetEventByIdQuery(id);
        var result = await handler.HandleAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
```

---

### C. POST State Transition Slice (`PublishEvent`)

#### 1. Contracts & Command
```csharp
namespace MyApp.Features.Events.PublishEvent;

public sealed record PublishEventResponse(
    Guid Id,
    string Status,
    DateTimeOffset PublishedAtUtc);

public sealed record PublishEventCommand(Guid Id);
```

#### 2. Transition Handler
```csharp
namespace MyApp.Features.Events.PublishEvent;

public sealed class PublishEventHandler(
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result<PublishEventResponse>> HandleAsync(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        var ev = await eventRepository.GetByIdAsync(command.Id, cancellationToken);
        if (ev is null)
        {
            return Result<PublishEventResponse>.Failure(
                Error.NotFound("Event.NotFound", $"Event '{command.Id}' was not found."));
        }

        // Domain aggregate method encapsulates state transition and invariant check
        var publishResult = ev.Publish(timeProvider);
        if (publishResult.IsFailure)
        {
            return Result<PublishEventResponse>.Failure(publishResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new PublishEventResponse(
            ev.Id,
            ev.Status.ToString(),
            ev.PublishedAtUtc!.Value);

        return Result<PublishEventResponse>.Success(response);
    }
}
```

#### 3. Endpoint Definition with `/{id:guid}/publish`
```csharp
namespace MyApp.Features.Events.PublishEvent;

public static class PublishEventEndpoint
{
    public static RouteGroupBuilder MapPublishEventEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/publish", HandleAsync)
            .WithName("PublishEvent")
            .WithSummary("Publishes an event")
            .Produces<PublishEventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    public static async Task<Results<Ok<PublishEventResponse>, ProblemHttpResult>> HandleAsync(
        Guid id,
        PublishEventHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new PublishEventCommand(id);
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.ToProblemHttpResult();
    }
}
```

---

### D. Result to HTTP Mapping Helper

Map domain failures into RFC 7807 `ProblemDetails` via `TypedResults.Problem(...)`:

```csharp
namespace MyApp.Features.Common;

public static class ResultToHttpExtensions
{
    public static ProblemHttpResult ToProblemHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem HTTP result.");
        }

        return result.Error.Type switch
        {
            ErrorType.Validation => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            ErrorType.Conflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code }),

            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: result.Error.Message,
                extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code })
        };
    }
}
```

---

### E. Route Grouping and Registration in `Program.cs`

#### Feature Aggregator (`EventsEndpoints.cs`)
```csharp
namespace MyApp.Features.Events;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events")
            .WithTags("Events");

        group.MapCreateEventEndpoint();
        group.MapGetEventByIdEndpoint();
        group.MapPublishEventEndpoint();

        return endpoints;
    }
}
```

#### Application Startup (`Program.cs`)
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Core & Infrastructure
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence(builder.Configuration);

// Register Feature Handlers (Scoped)
builder.Services.AddScoped<CreateEventHandler>();
builder.Services.AddScoped<GetEventByIdHandler>();
builder.Services.AddScoped<PublishEventHandler>();

var app = builder.Build();

// Map Vertical Slices
app.MapEventsEndpoints();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
```

---

## 4. Anti-Patterns to Reject

| Anti-Pattern | Why It Is Prohibited | Proper Vertical Slice Alternative |
| :--- | :--- | :--- |
| **Fat Endpoints** | Putting business logic, DB queries, or validation algorithms inside `group.MapPost(...)`. | Keep endpoint to 5-10 lines: create Command, call Handler, map `Result` to `TypedResults`. |
| **God Service (`EventsService`)** | A single service class containing 15 methods (`Create`, `Publish`, `Cancel`, `Search`). | Create a dedicated `Handler` per vertical slice (`CreateEventHandler`, `PublishEventHandler`). |
| **Shared Generic DTOs** | Using a single `EventDto` for Create, Update, Get, and Publish operations. | Dedicated `CreateEventRequest`, `CreateEventResponse`, `GetEventByIdResponse`. |
| **Direct DbContext in Endpoints** | Injecting `ApplicationDbContext` directly into Minimal API route delegate. | Route delegates inject the slice Handler, which coordinates with `IEventRepository` and `IUnitOfWork`. |
| **Leaking Domain Entities** | Returning `Event` entity directly to the client: `TypedResults.Ok(ev)`. | Always map to a dedicated response record (`OrderResponse`, `GetEventByIdResponse`). |
| **Raw `IResult` Return Types** | Returning `IResult` from endpoints instead of `Results<Ok<T>, ProblemHttpResult>`. | Use `Results<T1, T2>` with `TypedResults` to ensure compile-time OpenAPI type safety. |

---

## 5. Verification Checklist

Before finishing any vertical slice implementation:
- [ ] Is the feature placed in its own dedicated folder under `Features/<FeatureGroup>/<FeatureName>/`?
- [ ] Are Request, Response, Command, Handler, and Endpoint colocated?
- [ ] Does the endpoint delegate contain zero business logic or EF Core queries?
- [ ] Are routes grouped using `MapGroup(...)`?
- [ ] Does the endpoint return strongly typed `Results<TSuccess, ProblemHttpResult>` using `TypedResults`?
- [ ] Are route constraints (e.g. `{id:guid}`) applied where appropriate?
- [ ] Are dependencies injected via Minimal API method parameters?
- [ ] Is `CancellationToken` accepted and forwarded through all async calls?
- [ ] Are Response DTOs strictly separated from Domain entities?
- [ ] Does the project compile cleanly with zero warnings (`dotnet build --no-incremental`)?

---

## 6. Reference Code
Inspect the complete working reference files:
- [CreateEvent Endpoint & Handler](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/CreateEvent/CreateEventEndpoint.cs)
- [GetEventById Endpoint & Handler](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/GetEventById/GetEventByIdEndpoint.cs)
- [PublishEvent Endpoint & Handler](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/PublishEvent/PublishEventEndpoint.cs)
- [Events Route Group Aggregator](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/EventsEndpoints.cs)
- [Result to HTTP Mapping Helper](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/ResultToHttpExtensions.cs)
- [Program.cs Registration](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-vertical-slice/examples/events-feature/ProgramExtensions.cs)
