---
name: dotnet-testing-strategy
description: >-
  Guides comprehensive testing for ASP.NET Core .NET 10 Minimal APIs.
  Covers unit testing of Rich Domain Models and CQRS Handlers (with FakeTimeProvider),
  and integration testing of Minimal API endpoints using WebApplicationFactory and isolated SQLite instances.
---

# Testing Strategy for .NET 10 Minimal APIs

This skill provides an authoritative, practical framework for designing, implementing, and running automated tests for ASP.NET Core .NET 10 Minimal API applications incorporating Vertical Slice Architecture, Rich Domain Model, the `Result<T>` pattern, custom CQRS, and EF Core with SQLite.

---

## 1. Pre-Testing Inspection Protocol

Before writing a single test file:
1. **Inspect the Solution**: Search existing test projects (`*.Tests`, `tests/`).
2. **Identify Existing Framework**: Check if the solution uses `xUnit`, `NUnit`, or `MSTest`.
3. **Verify Existing Assertion & Mocking Libraries**: Check `.csproj` for `FluentAssertions`, `AwesomeAssertions`, `NSubstitute`, `Moq`, or standard assertion APIs.
4. **Reuse Conventions**: Adopt existing naming styles, base test fixtures, and directory structures.
5. **Zero Superfluous Packages**: Never introduce an alternative test runner or mock library if one is already present.

---

## 2. The 4-Tier Testing Architecture

```text
       /\
      /  \      Tier D: API Integration Tests (WebApplicationFactory + SQLite :memory:)
     /────\     • Validates Endpoints, TypedResults, Routing, 400/404/409 Statuses, JSON
    /      \
   /────────\   Tier C: Persistence Integration Tests (SQLite in-memory)
  /          \  • Validates EF Core Configurations, Unique Indexes, Constraints, UTC
 /────────────\
/  Tier B      \ Tier B: Handler Unit Tests (Mocks for Repositories & UnitOfWork)
──────────────── • Validates Orchestration, Invariant-failure early exit, SaveChanges called/not called
/  Tier A       \ Tier A: Domain Unit Tests (Pure C#, Zero Mocks, FakeTimeProvider)
───────────────── • Validates Invariants, Factory Methods, State Transitions, Value Objects
```

---

### Tier A: Domain Unit Tests (Zero Mocks, Pure C#)
Domain unit tests verify core business logic without touching databases, HTTP contexts, or mock frameworks:
- **Factory Methods**: Test `Event.Create(...)` returns `Result.Success` for valid inputs and `Result.Failure(Error.Validation)` for invalid data.
- **Invariants & Boundary Limits**: Test negative numbers, zero capacity, empty strings, past dates.
- **State Transitions**: Test `Publish`, `Cancel`, `Register`, asserting both valid transitions and invalid transitions (e.g. publishing an already cancelled event).
- **Value Objects**: Test `DateRange` boundaries (`StartUtc < EndUtc`), overlap detection (`Overlaps`), and immutability.
- **Error Assertions**: Assert specific `result.Error.Code` and `result.Error.Type` values (`Validation`, `NotFound`, `Conflict`).
- **Deterministic Time**: Use `Microsoft.Extensions.Time.Testing.FakeTimeProvider`. Never use `DateTime.UtcNow` or `Thread.Sleep()`.

---

### Tier B: Handler Unit Tests (Orchestration & Transaction Boundary)
Handler tests verify application orchestration using mocks/fakes for `IRepository` and `IUnitOfWork`:
- **Verify Repository Queries**: Verify aggregate lookup with proper ID.
- **Entity Missing**: Verify that when repository returns `null`, handler returns `Error.NotFound` and **does NOT call `unitOfWork.SaveChangesAsync`**.
- **Domain Failure Early Exit**: When domain method returns `Result.Failure`, verify handler returns that exact failure and **never calls `unitOfWork.SaveChangesAsync`**.
- **Success Flow**: Verify that when domain succeeds, `unitOfWork.SaveChangesAsync` is called exactly once and the mapped response DTO is returned.

---

### Tier C: Persistence Integration Tests (SQLite in-memory)
Verify Entity Framework Core configurations against real relational SQLite mechanics:
- **SQLite In-Memory**: Always use `SqliteConnection("DataSource=:memory:")` kept open for the test duration.
- **Reject `EF Core InMemory` Provider**: Never use `Microsoft.EntityFrameworkCore.InMemory` for relational tests. It does not enforce unique indexes, foreign keys, or relational data types.
- **Unique Indexes**: Verify that attempting to insert duplicate records (e.g. duplicate email or composite `(EventId, ParticipantId)`) throws `DbUpdateException`.
- **UTC Converters**: Verify timestamps read back from SQLite maintain `DateTimeOffset` UTC offset (+00:00).
- **Cascade Deletes**: Verify deleting a parent removes children according to `DeleteBehavior`.

---

### Tier D: API Integration Tests (Minimal API Endpoints)
Verify the complete HTTP lifecycle using `WebApplicationFactory<Program>`:
- **HTTP Status Codes**: Assert `201 Created`, `200 OK`, `400 BadRequest`, `404 NotFound`, `409 Conflict`.
- **Response Payloads**: Deserialize JSON and assert that fields match expected values.
- **ProblemDetails**: Ensure error responses conform to RFC 7807 (`title`, `detail`, `status`, `extensions["errorCode"]`).
- **End-to-End Pipeline**: Test that `POST /api/events` persists data accessible by subsequent requests.

---

## 3. Test Quality Standards (Clean Test Rules)

1. **Single Primary Reason to Fail**: Each test must test a single logical concept or assertion.
2. **Arrange, Act, Assert (AAA)**: Clearly separate setup, execution, and verification phases.
3. **Descriptive Naming Convention**:
   ```csharp
   [MethodUnderTest]_[ScenarioCondition]_[ExpectedOutcome]
   // Example: Cancel_WhenAfterCancellationWindow_ReturnsConflictError
   ```
4. **No Implementation Trivia**: Test external observable behavior and state, not private fields or compiler implementation details.
5. **No Flaky Tests**: Zero tolerance for intermittent failures caused by real system clocks or race conditions.
6. **No Overly General Assertions**: Never stop at `Assert.True(result.IsFailure)`. Always verify `Assert.Equal("Event.SoldOut", result.Error.Code)`.

---

## 4. Feature Testing Lifecycle (5-Step Loop)

Whenever a new requirement or feature is implemented:

```text
[1. Identify Failure Points] 
       ↓
[2. Build Positive & Negative Matrix] 
       ↓
[3. Implement Relevant Tests] 
       ↓
[4. Execute: dotnet test] 
       ↓
[5. Report Exact Pass/Fail Results]
```

> [!WARNING]
> **Strict Verification Rule:**
> **Never declare or claim that tests have passed without executing them via the CLI!**
> Always run:
> ```powershell
> dotnet test --logger "console;verbosity=normal"
> ```
> and inspect the terminal output.

---

## 5. Examples & References
- Complete Domain Unit Tests: [DomainUnitTests.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-testing-strategy/examples/DomainUnitTests.cs)
- Complete Handler Unit Tests (7 scenarios): [RegisterForEventHandlerTests.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-cqrs-handlers/examples/RegisterForEventHandlerTests.cs)
- SQLite In-Memory Persistence Tests: [PersistenceIntegrationTests.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-testing-strategy/examples/PersistenceIntegrationTests.cs)
- Minimal API Integration Tests: [ApiIntegrationTests.cs](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-testing-strategy/examples/ApiIntegrationTests.cs)
