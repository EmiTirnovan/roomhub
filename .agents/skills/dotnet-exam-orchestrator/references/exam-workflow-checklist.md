# Exam Protocol & Discipline Checklist

## 1. Non-Negotiable Documentation Boundary
The following three files are exclusively owned by the student/candidate:
- `DECIZII.md`
- `BUGURI.md`
- `AI-LOG.md`

### Strict Agent Protocol:
- ❌ **NEVER** create these files.
- ❌ **NEVER** edit or append to these files.
- ❌ **NEVER** generate template or stub content inside these files.
- ✅ **DO** provide clear verbal briefings at the conclusion of an increment summarizing:
  - Architecture and design choices made (for `DECIZII.md`).
  - Bugs, edge cases, or traps identified and handled (for `BUGURI.md`).
  - Recommended prompts or workflow steps (for `AI-LOG.md`).

---

## 2. Micro-Iterations Protocol (Step-by-Step Delivery)

Never implement an entire feature across all layers in a single pass. Adhere strictly to the 6 micro-steps:

| Micro-Step | Scope of Change | Verification Gate | Stop Condition |
| :--- | :--- | :--- | :--- |
| **1. Domain** | Entities, Value Objects, Factory method, Invariants, Domain Errors | `dotnet build` / Domain Unit Tests | Do not proceed if compilation fails or invariants are exposed. |
| **2. Persistence** | `IEntityTypeConfiguration`, Repository contract & implementation | `dotnet build` | Do not proceed if mapping or foreign keys are unverified. |
| **3. Handler** | Command record, Custom Handler orchestrating Domain & UoW | `dotnet build` / Handler Unit Tests | Do not proceed if `SaveChangesAsync` is called on Domain failure. |
| **4. Endpoint** | Request, Response, Minimal API route delegate, `TypedResults` | `dotnet build` | Do not proceed if route constraints or return types are untyped. |
| **5. Tests** | Integration tests (SQLite in-memory, WebApplicationFactory) | `dotnet test` | Do not proceed until 100% of tests pass. |
| **6. Final Review** | 20-point quality audit, zero warnings, clean diff | `dotnet-code-quality-review` | Do not declare Ready if warnings or unverified code exist. |

> [!WARNING]
> **No Speculative File Creation:**
> Never create stub files, empty classes, or placeholders in advance without an immediate concrete need in the current micro-step.

---

## 3. Database Transaction & Persistence Discipline

### Transaction Rules:
- **`Repository.Add` never saves**: Only registers the entity in the `ChangeTracker`.
- **`UnitOfWork` controls `SaveChangesAsync`**: Handlers call `unitOfWork.SaveChangesAsync(cancellationToken)` once at the end of the operation.
- **Single Scoped DbContext**: All Repositories and `UnitOfWork` must resolve the exact same `Scoped` instance of `DbContext`.
- **Avoid Multiple `SaveChangesAsync`**: In 95% of use cases, a single `SaveChangesAsync` call guarantees complete atomicity via EF Core's built-in transaction. Do NOT call `SaveChanges` multiple times in the same handler.
- **Explicit Transactions are Rare Exceptions**:
  - Use `await dbContext.Database.BeginTransactionAsync()` ONLY when a logical operation requires atomicity across multiple distinct save operations or external boundaries.
  - Do NOT open transactions inside Repository methods.
  - Do NOT open explicit transactions for simple single-save operations.
- **SQLite Concurrency Awareness**:
  - SQLite uses database-level write locks.
  - Keep transactions as short as possible to prevent `busy` / `locked` SQLite errors.
  - Never execute long-running I/O (e.g. HTTP calls, file reading) while holding an open SQLite transaction.

---

## 4. Specialized Skills Dispatch Matrix

| Concern | Primary Specialized Skill | Key Invariant / Standard Enforced |
| :--- | :--- | :--- |
| **Slice Structuring** | `dotnet-vertical-slice` | Cohesive feature folder under `Features/`, thin endpoint, TypedResults, route constraints. |
| **Domain Logic** | `dotnet-rich-domain-result` | Private setters, factory methods (`Create`), invariants in Domain, Value Objects (`DateRange`), Result/Error. |
| **State Mutations** | `dotnet-cqrs-handlers` | Dedicated Command & Handler, orchestration only, zero MediatR, assembly scan registration. |
| **Data Persistence** | `dotnet-efcore-persistence` | `IEntityTypeConfiguration<T>`, Value Object mapping via `ComplexProperty`, SQLite UTC converter. |
| **Contracts Decoupling** | `dotnet-repository-unit-of-work` | Repository in Domain, implementation in Infrastructure, single Scoped DbContext instance for UoW. |
| **Test Engineering** | `dotnet-testing-strategy` | 4-tier testing (Domain unit, Handler unit, SQLite in-memory persistence, Minimal API integration). |
| **Quality Audit Gate** | `dotnet-code-quality-review` | 20-point checklist, zero warnings, clean diff, Ready / Not Ready verdict. |

---

## 5. Ambiguity Resolution Protocol

If an exam requirement is ambiguous:
1. Do NOT guess blindly or over-engineer.
2. Identify the **minimal viable assumption** that satisfies the core requirement without breaking existing invariants.
3. State the assumption clearly to the student before writing code.
4. Proceed once confirmed or as documented in the implementation plan.
