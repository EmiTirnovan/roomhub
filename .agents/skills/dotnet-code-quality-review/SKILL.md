---
name: dotnet-code-quality-review
description: >-
  Conducts production-grade code reviews for ASP.NET Core .NET 10 Minimal API projects.
  Enforces SOLID principles (specifically Open/Closed), rich domain encapsulation, TypedResults correctness,
  zero-warning compilation, absence of TODOs or dead code, and exam documentation boundaries.
---

# Production-Grade Technical Code Quality Review

This skill governs the final technical audit of any new feature or modification made to an ASP.NET Core .NET 10 Minimal API solution. In an examination setting, functional code that merely compiles is insufficient; the solution must satisfy rigorous production-grade architecture, maintainability, and clean code standards.

> [!IMPORTANT]
> **Core Review Rule:**
> **Do NOT automatically modify code during the review without identifying and documenting the issue first.**
> Analyze, run verification gates, report defects, and present the final verdict transparently.

---

## 1. The 20-Point Technical Checklist

Every modification must be rigorously evaluated against these 20 inspection gates:

```text
 1. [ ] Cerința este implementată complet? (All functional & edge requirements covered)
 2. [ ] Există Vertical Slice coerent? (Features/<Group>/<UseCase>/ grouping)
 3. [ ] Endpoint-urile sunt subțiri? (Route delegates strictly 5-10 lines, no business logic)
 4. [ ] Regulile de business sunt în Domain? (Aggregates & Value Objects own logic)
 5. [ ] Entitățile își protejează invariants? (Private setters, factory methods, valid state only)
 6. [ ] Sunt folosite corect Result și Error? (Result<T>, ErrorType, safe Value access)
 7. [ ] Sunt evitate excepțiile pentru business errors? (No throw new Exception for logic rejections)
 8. [ ] Handler-ele doar orchestrează? (Load -> Check -> Invoke Domain -> UoW Save -> Return DTO)
 9. [ ] Repository și UnitOfWork respectă separarea Domain/Infrastructure? (Domain contracts clean)
10. [ ] Repository și UnitOfWork împart aceeași instanță Scoped de DbContext? (Forwarded in DI)
11. [ ] Configurațiile EF Core sunt separate? (IEntityTypeConfiguration per entity, clean OnModelCreating)
12. [ ] Sunt corecte relațiile și indexurile? (Unique indexes, composite indexes, DeleteBehavior)
13. [ ] Există CancellationToken? (Forwarded across all async method calls)
14. [ ] Sunt folosite corect AsNoTracking și async? (AsNoTracking on reads, async on all I/O)
15. [ ] Există race conditions evidente? (Guarded by DB constraints or transaction boundaries)
16. [ ] Sunt tratate cazurile negative? (400 BadRequest, 404 NotFound, 409 Conflict ProblemDetails)
17. [ ] Există cod duplicat? (Zero duplicated domain validation across layers)
18. [ ] Există abstracții inutile? (No MediatR, no generic repository bloat)
19. [ ] Sunt respectate convențiile proiectului? (Consistent naming, clean nullable types)
20. [ ] Testele acoperă comportamentul important? (Domain unit, handler, persistence, API)
```

---

## 2. Mandatory Verification Gates (Must Execute in Terminal)

Before compiling the audit report, run the following verification commands:

### Gate 1: Clean Build (Zero Warnings)
```powershell
dotnet build --no-incremental
```
- Verify: **0 Errors**, **0 Warnings**. Any compiler warning (`CS8618`, `CS8602`, `CS1998`) must be treated as a blocker.

### Gate 2: Test Suite Execution
```powershell
dotnet test --logger "console;verbosity=normal"
```
- Verify: All tests pass (100% success rate).
- **Rule**: Never declare tests passed without executing this command and reading the real output.

### Gate 3: DI Registration Audit
Verify `Program.cs` or service extensions:
- Handlers registered as `Scoped`.
- `IUnitOfWork` forwarded: `services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CampusHubDbContext>());`.
- Repositories registered as `Scoped`.

### Gate 4: Route Constraints & OpenAPI
Verify endpoint definitions:
- Route constraints applied (e.g. `/{id:guid}`).
- Typed results declared: `Results<Ok<T>, ProblemHttpResult>` or `Results<Created<T>, ProblemHttpResult>`.

### Gate 5: EF Core Migrations
If the database schema changed:
- Confirm migration created: `dotnet ef migrations list`.
- Confirm database is up to date: `dotnet ef database update`.

### Gate 6: Final Git Diff Inspection
```powershell
git diff --stat
```
- Inspect changed files to ensure no stray `TODO`, temporary files, or accidental edits occurred.

---

## 3. Standardized Final Audit Report Template

Compile the final review strictly using this markdown template:

```markdown
# 📋 Raport Final de Audit Tehnic

## 1. Cerințe îndeplinite
- [x] [Cerința 1 - Descriere scurtă]
- [x] [Cerința 2 - Descriere scurtă]

## 2. Fișiere create / modificate
- `src/.../File1.cs` (Creat / Modificat)
- `tests/.../File1Tests.cs` (Creat)

## 3. Teste executate și rezultate
- **Comandă executată:** `dotnet test --logger "console;verbosity=normal"`
- **Total teste:** [X]
- **Trecute:** [X]
- **Eșuate:** [0]
- **Detalii teste cheie:**
  - `TestName1`: PASSED
  - `TestName2`: PASSED

## 4. Probleme identificate și rezolvate
- [Descrie problemele identificate pe baza celor 20 de puncte și cum au fost remediate]

## 5. Probleme nerezolvate
- [None sau lista problemelor deschise care necesită atenție]

## 6. Riscuri tehnice
- [Identifică riscuri: ex. concurență SQLite, locking, performanță query-uri]

## 7. Verdict Final
**[READY / NOT READY]**
*(NOTĂ: Nu declara NICIODATĂ "READY" dacă build-ul are avertismente sau vreun test eșuează!)*
```

---

## 4. Student Documentation Briefing (Exam Boundaries)

> [!CAUTION]
> **Reminder on Candidate Files (`DECIZII.md`, `BUGURI.md`, `AI-LOG.md`):**
> Do NOT create or edit these files!
> Conclude the audit with verbal bullet points so the candidate can manually log them:

```markdown
### 📝 Note pregătite pentru documentația personală (Examen):

**Pentru DECIZII.md:**
- [Decizia arhitecturală luată, de ex: utilizarea Value Object DateRange pentru protejarea intervalelor].
- [Decizia DI: rezolvarea IUnitOfWork din aceeași instanță Scoped de DbContext].

**Pentru BUGURI.md:**
- [Capcana / bug-ul evitat: prevenirea apelării SaveChangesAsync atunci când Domain returnează Failure].
- [Indexul compus creat pentru a preveni înregistrările duplicate la nivel de bază de date].

**Pentru AI-LOG.md:**
- [Prompt-ul și activitatea de audit tehnic realizată].
```

---

## 5. References & Examples
- Complete 20-Point Checklist Rubric: [production-code-review-rubric.md](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-code-quality-review/references/production-code-review-rubric.md)
- Reference Audit Report: [sample-quality-audit-report.md](file:///d:/Facultate/dotNet/Partial/.agents/skills/dotnet-code-quality-review/examples/sample-quality-audit-report.md)
