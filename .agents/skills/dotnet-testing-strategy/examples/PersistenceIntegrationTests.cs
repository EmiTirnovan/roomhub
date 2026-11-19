using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Domain.Participants;
using MyApp.Infrastructure.Persistence;
using Xunit;

namespace MyApp.Tests.Integration.Persistence;

public sealed class CampusHubPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CampusHubDbContext _dbContext;

    public CampusHubPersistenceTests()
    {
        // SQLite in-memory database requires an open connection for the lifetime of the test
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CampusHubDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new CampusHubDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SaveChanges_WithValidEventAndValueObject_PersistsAndReadsBackCorrectly()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var startUtc = DateTimeOffset.UtcNow.AddDays(3);
        var endUtc = startUtc.AddHours(2);
        var schedule = DateRange.Create(startUtc, endUtc).Value;

        var ev = Event.Create("Tech Meetup", "Great discussions.", schedule, 50, timeProvider).Value;

        // Act
        await _dbContext.Events.AddAsync(ev);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Assert
        var loaded = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == ev.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Tech Meetup", loaded.Title);
        Assert.Equal(50, loaded.MaxCapacity);
        Assert.Equal(EventStatus.Draft, loaded.Status);
        Assert.Equal(schedule.StartUtc, loaded.Schedule.StartUtc);
        Assert.Equal(schedule.EndUtc, loaded.Schedule.EndUtc);
    }

    [Fact]
    public async Task SaveChanges_WhenDuplicateParticipantEmailInserted_ThrowsDbUpdateExceptionDueToUniqueIndex()
    {
        // Arrange
        var p1 = Participant.Create("Alice", "duplicate@test.com", DateTimeOffset.UtcNow);
        var p2 = Participant.Create("Bob", "duplicate@test.com", DateTimeOffset.UtcNow);

        await _dbContext.Participants.AddAsync(p1);
        await _dbContext.SaveChangesAsync();

        // Act & Assert: SQLite unique constraint violation
        await _dbContext.Participants.AddAsync(p2);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_WhenDuplicateEventRegistrationInserted_ThrowsDbUpdateExceptionDueToCompositeUniqueIndex()
    {
        // Arrange
        var timeProvider = TimeProvider.System;
        var schedule = DateRange.Create(DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(5).AddHours(2)).Value;
        var ev = Event.Create("Conference", "Desc", schedule, 100, timeProvider).Value;
        ev.Publish(timeProvider);

        var participant = Participant.Create("Charlie", "charlie@test.com", DateTimeOffset.UtcNow);

        await _dbContext.Events.AddAsync(ev);
        await _dbContext.Participants.AddAsync(participant);
        await _dbContext.SaveChangesAsync();

        // Register participant
        ev.Register(participant.Id, participant.Email, timeProvider);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Act & Assert: Attempting to insert a duplicate registration directly into DB
        var duplicateReg = EventRegistration.Create(ev.Id, participant.Id, participant.Email, DateTimeOffset.UtcNow).Value;
        await _dbContext.EventRegistrations.AddAsync(duplicateReg);

        await Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UtcDateTimeConverter_PreservesUtcKindAndOffset()
    {
        // Arrange
        var utcTimestamp = new DateTimeOffset(2026, 8, 15, 14, 30, 0, TimeSpan.Zero);
        var participant = Participant.Create("Dana", "dana@test.com", utcTimestamp);

        // Act
        await _dbContext.Participants.AddAsync(participant);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        // Assert
        var loaded = await _dbContext.Participants.FirstAsync(p => p.Id == participant.Id);
        Assert.Equal(TimeSpan.Zero, loaded.CreatedAtUtc.Offset);
        Assert.Equal(utcTimestamp, loaded.CreatedAtUtc);
    }
}
