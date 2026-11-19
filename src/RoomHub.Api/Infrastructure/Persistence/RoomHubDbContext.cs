using Microsoft.EntityFrameworkCore;
using RoomHub.Api.Domain.Repositories;
using RoomHub.Api.Models;

namespace RoomHub.Api.Infrastructure.Persistence;

public class RoomHubDbContext(DbContextOptions<RoomHubDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomHubDbContext).Assembly);
    }
}
