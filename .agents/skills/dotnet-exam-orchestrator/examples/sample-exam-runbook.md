# Sample Exam Orchestration Runbook

This document demonstrates the exact execution of the 6-phase protocol for a typical exam requirement.

---

## Example Requirement
*"Implement the ability to cancel an event: `POST /api/events/{id:guid}/cancel`. An event can only be cancelled if it is in Draft or Published status and has not already started. A cancellation reason is mandatory. When cancelled, all registrations remain preserved for audit, but the status becomes Cancelled and CancelledAtUtc is recorded. Return 200 OK with the cancellation response or 409 Conflict if invariants fail."*

---

## Faza 1 — Reconnaissance
1. Run discovery commands:
   ```powershell
   Get-ChildItem -Recurse *.csproj
   Get-Content src/CampusHub.Web/Program.cs
   Get-Content src/CampusHub.Web/appsettings.json
   ```
2. Findings:
   - Solution targets `net10.0` with C# 13/14.
   - Persistence: `CampusHubDbContext` uses EF Core 10 SQLite.
   - Result pattern: `Result` and `Result<T>` found in `CampusHub.Domain.Common`.
   - Existing aggregate: `Event` found in `CampusHub.Domain.Events`.
   - Repositories: `IEventRepository` and `CampusHubDbContext : IUnitOfWork`.
   - Existing tests: xUnit + NSubstitute in `tests/CampusHub.Tests.Unit`.

## Faza 2 — Requirement Analysis
- **Domain**: Add `Event.Cancel(string reason, TimeProvider timeProvider)` method. Validate status != Cancelled, status != Completed, reason not empty, and `now < Schedule.StartUtc`.
- **Vertical Slice**: Create `Features/Events/CancelEvent/` containing `CancelEventRequest`, `CancelEventResponse`, `CancelEventCommand`, `CancelEventHandler`, and `CancelEventEndpoint`.
- **Persistence**: No new tables needed; reuse `IEventRepository.GetByIdAsync` and `IUnitOfWork.SaveChangesAsync`.
- **HTTP**: `POST /api/events/{id:guid}/cancel` returning `Results<Ok<CancelEventResponse>, ProblemHttpResult>`.
- **Edge cases**: Missing reason -> 400 Bad Request; nonexistent ID -> 404 Not Found; event in the past or already cancelled -> 409 Conflict.

## Faza 3 — Implementation Plan
Present short plan to student:
1. Domain: Implement `Event.Cancel` and unit tests.
2. Application/Feature: Implement `CancelEventCommand` & `CancelEventHandler` and handler tests.
3. Minimal API: Implement `CancelEventEndpoint` and register in `EventsEndpoints.cs`.
4. Verification: Run `dotnet test` and execute final review.

## Faza 4 — Incremental Implementation
- **Step 1**: Add `Cancel` method to `Event` aggregate. Run `dotnet test tests/CampusHub.Tests.Unit --filter FullyQualifiedName~EventTests`. (Passed).
- **Step 2**: Add `CancelEventCommand` & `CancelEventHandler`. Run `dotnet test`. (Passed).
- **Step 3**: Add `CancelEventEndpoint` with `{id:guid}` constraint. Wire in `EventsEndpoints.cs`. Run `dotnet build`. (Passed with 0 warnings).

## Faza 5 — Verification
- Run `dotnet build --no-incremental`: 0 warnings, 0 errors.
- Run `dotnet test`: 18 tests passed, 0 failed.
- Confirm DI: Handler registered as `Scoped`.
- Check initial requirements: Invariants enforced, status updated, timestamp recorded.

## Faza 6 — Final Report & Briefing
- Present the final report with verdict `READY`.
- Deliver verbal talking points for the candidate's personal files (`DECIZII.md`, `BUGURI.md`, `AI-LOG.md`).
