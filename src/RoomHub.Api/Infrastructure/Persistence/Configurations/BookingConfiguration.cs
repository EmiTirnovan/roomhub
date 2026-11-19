using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomHub.Api.Models;

namespace RoomHub.Api.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.RoomId)
            .IsRequired();

        builder.Property(b => b.BookedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.StartUtc)
            .IsRequired();

        builder.Property(b => b.EndUtc)
            .IsRequired();

        builder.Property(b => b.Attendees)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
    }
}
