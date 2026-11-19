# The 20-Point Technical Audit Rubric (.NET 10 Minimal APIs)

Every pull request, vertical slice, or exam submission must be evaluated against these 20 inspection gates before receiving approval.

---

## The 20 Inspection Gates

### 1. Cerința este implementată complet?
- [ ] Toate criteriile de acceptanță specificate în cerință sunt acoperite.
- [ ] Nu lipsesc câmpuri din request sau response.
- [ ] Stările și fluxurile secundare sunt funcționale.

### 2. Există Vertical Slice coerent?
- [ ] Fișierele sunt grupate per use case în `Features/<FeatureGroup>/<UseCase>/`.
- [ ] Request, Response, Command, Handler și Endpoint sunt colocalizate.
- [ ] Nu există scurgeri sau apeluri directe între felii paralele.

### 3. Endpoint-urile sunt subțiri?
- [ ] Delegatul de rută are maxim 5-10 linii.
- [ ] Endpoint-ul doar mapează HTTP -> Command, apelează Handler și mapează `Result` -> `TypedResults`.
- [ ] Fără algoritmi, fără validări de business și fără apeluri EF Core în endpoint.

### 4. Regulile de business sunt în Domain?
- [ ] Calculele, validările de stare și regulile de eligibilitate sunt metode pe Aggregate Root sau Value Objects.
- [ ] Handler-ul nu conține logică decizională de business.

### 5. Entitățile își protejează invariants?
- [ ] Toate proprietățile au `private set;` sau `init;`.
- [ ] Colecțiile sunt expuse ca `IReadOnlyCollection<T>` cu backing field privat.
- [ ] Nu există constructori publici care să permită instanțierea într-o stare invalidă.
- [ ] Instanțierea se face exclusiv prin fabrici statice (`Create`) care returnează `Result<T>`.

### 6. Sunt folosite corect Result și Error?
- [ ] Operațiile returnează `Result<T>` sau `Result`.
- [ ] Proprietatea `.Value` este accesată DOAR după verificarea `IsSuccess`.
- [ ] Erorile au `Code` stabil, `Message` clar și `ErrorType` corect.

### 7. Sunt evitate excepțiile pentru business errors?
- [ ] Nicio excepție aruncată (`throw new ...`) pentru validări normale sau reguli de business respinse.
- [ ] Excepțiile sunt rezervate strict pentru erori de sistem neașteptate (crash bază de date, memorie epuizată).

### 8. Handler-ele doar orchestrează?
- [ ] Handler-ul încarcă agregatul din repository.
- [ ] Verifică existența resurselor (returnează `NotFound` dacă lipsesc).
- [ ] Apelează metoda din Domain.
- [ ] Apelează `unitOfWork.SaveChangesAsync(cancellationToken)`.
- [ ] Mapează și returnează DTO-ul de răspuns.

### 9. Repository și UnitOfWork respectă separarea Domain/Infrastructure?
- [ ] Interfețele `IRepository` și `IUnitOfWork` trăiesc în Domain/Application.
- [ ] Interfețele din Domain NU conțin `using Microsoft.EntityFrameworkCore;` și NU expun `IQueryable` sau `DbSet`.
- [ ] Implementările trăiesc exclusiv în Infrastructure.

### 10. Repository și UnitOfWork împart aceeași instanță Scoped de DbContext?
- [ ] În DI, `IUnitOfWork` este înregistrat prin forwardare:
  `services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CampusHubDbContext>());`
- [ ] Nu există instanțe multiple de DbContext create în cadrul aceluiași request.

### 11. Configurațiile EF Core sunt separate?
- [ ] Fiecare entitate are propria clasă `IEntityTypeConfiguration<T>`.
- [ ] `OnModelCreating` este curat și apelează doar `ApplyConfigurationsFromAssembly`.

### 12. Sunt corecte relațiile și indexurile?
- [ ] Indexuri unice configurate pentru câmpuri unice (ex: `Email`).
- [ ] Indexuri compuse configurate pentru relații de asociere (ex: `(EventId, AttendeeId)`).
- [ ] Relații foreign key configurate cu `DeleteBehavior` explicit (`Cascade` sau `Restrict`).
- [ ] Lungimi maxime (`HasMaxLength`) specificate pe toate coloanele text.

### 13. Există CancellationToken?
- [ ] Endpoint-ul primește `CancellationToken cancellationToken`.
- [ ] Tokenul este transmis către Handler, Repository și metodele EF Core (`SaveChangesAsync`, `FirstOrDefaultAsync`, etc.).

### 14. Sunt folosite corect AsNoTracking și async?
- [ ] Toate operațiile I/O sunt asincrone (`async`/`await`).
- [ ] Query-urile de citire folosesc `.AsNoTracking()`.
- [ ] Verificările de existență folosesc `.AnyAsync(...)`.

### 15. Există race conditions evidente?
- [ ] Verificările de capacitate sau duplicare sunt protejate fie la nivel de tranzacție, fie prin indexuri unice în baza de date.

### 16. Sunt tratate cazurile negative?
- [ ] Resursă inexistentă -> `404 Not Found` (ProblemDetails).
- [ ] Date invalide -> `400 Bad Request` (ProblemDetails).
- [ ] Conflict de stare / capacitate epuizată -> `409 Conflict` (ProblemDetails).

### 17. Există cod duplicat?
- [ ] Validările nu sunt duplicate între Endpoint, Handler și Domain.
- [ ] Nu există DTO-uri clonă copiate inutil.

### 18. Există abstracții inutile?
- [ ] Fără `GenericRepository<T>` supra-complicat.
- [ ] Fără biblioteci externe nejustificate (ex: MediatR, AutoMapper).
- [ ] Fără interfețe cu o singură metodă create fără a fi necesare pentru testare sau decuplare.

### 19. Sunt respectate convențiile proiectului?
- [ ] Nomenclatura claselor, metodelor și fișierelor respectă starter project-ul.
- [ ] Nullable reference types sunt respectate fără avertismente `CS8618` sau `CS8602`.

### 20. Testele acoperă comportamentul important?
- [ ] Există teste unitare pentru Domain (invariante, fabrici statice, tranziții de stare, `FakeTimeProvider`).
- [ ] Există teste pentru Handler (verifică că `SaveChanges` nu se apelează pe eroare).
- [ ] Există teste de integrare (persistență SQLite și endpoint Minimal API).
