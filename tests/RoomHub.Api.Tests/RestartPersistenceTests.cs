using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Infrastructure.Persistence;
using RoomHub.Api.Infrastructure.Repositories;
using RoomHub.Api.Models;

namespace RoomHub.Api.Tests;

public class RestartPersistenceTests
{
    [Fact]
    public async Task Data_PersistsAndSurvivesAcrossApplicationRestart()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "test_restart.db");
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        var connectionString = $"Data Source={dbPath}";
        var roomId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        // Phase 1: First "Run" of the application
        var options1 = new DbContextOptionsBuilder<RoomHubDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using (var db1 = new RoomHubDbContext(options1))
        {
            await db1.Database.EnsureCreatedAsync();

            var roomRepo = new SqliteRoomRepository(db1);
            var bookingRepo = new SqliteBookingRepository(db1);

            var room = Room.Create("Persistent Room", 15).Value;
            // Overwrite Id to known GUID for retrieval
            typeof(Room).GetProperty("Id")!.SetValue(room, roomId);
            await roomRepo.AddAsync(room);

            var booking = Booking.Create(
                roomId,
                "Emi",
                new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
                4,
                15,
                TimeProvider.System).Value;

            typeof(Booking).GetProperty("Id")!.SetValue(booking, bookingId);
            await bookingRepo.AddAsync(booking);

            await db1.SaveChangesAsync();
        } // Application 1 completely stopped and disposed!

        // Phase 2: Application "Restart" with fresh DbContext and fresh Repositories
        var options2 = new DbContextOptionsBuilder<RoomHubDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using (var db2 = new RoomHubDbContext(options2))
        {
            var roomRepo = new SqliteRoomRepository(db2);
            var bookingRepo = new SqliteBookingRepository(db2);

            var retrievedRoom = await roomRepo.GetByIdAsync(roomId);
            var retrievedBooking = await bookingRepo.GetByIdAsync(bookingId);

            Assert.NotNull(retrievedRoom);
            Assert.Equal("Persistent Room", retrievedRoom.Name);
            Assert.Equal(15, retrievedRoom.Capacity);

            Assert.NotNull(retrievedBooking);
            Assert.Equal("Emi", retrievedBooking.BookedBy);
            Assert.Equal(BookingStatus.Active, retrievedBooking.Status);
            Assert.Equal(4, retrievedBooking.Attendees);
        }

        // Cleanup
        SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath))
        {
            try { File.Delete(dbPath); } catch { }
        }
    }
}
