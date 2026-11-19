using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Common;
using MyApp.Domain.Events;
using MyApp.Infrastructure.Persistence.Converters;

namespace MyApp.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");

        // Primary Key generated in Domain
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.MaxCapacity)
            .IsRequired();

        // Enum stored as string for forward compatibility and SQLite readability
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Value Object: DateRange mapped via ComplexProperty (.NET 8/9/10)
        builder.ComplexProperty(e => e.Schedule, scheduleBuilder =>
        {
            scheduleBuilder.Property(s => s.StartUtc)
                .HasColumnName("ScheduleStartUtc")
                .HasConversion(new UtcDateTimeOffsetConverter())
                .IsRequired();

            scheduleBuilder.Property(s => s.EndUtc)
                .HasColumnName("ScheduleEndUtc")
                .HasConversion(new UtcDateTimeOffsetConverter())
                .IsRequired();
        });

        // Audit timestamps with explicit UTC conversion
        builder.Property(e => e.CreatedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired();

        builder.Property(e => e.PublishedAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired(false);

        builder.Property(e => e.CancelledAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired(false);

        builder.Property(e => e.CancellationReason)
            .HasMaxLength(500)
            .IsRequired(false);

        // Encapsulation: Map navigation property to backing field _registrations
        builder.HasMany(e => e.Registrations)
            .WithOne()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Registrations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Query Indexes
        builder.HasIndex(e => e.Title);
        builder.HasIndex(e => e.Status);
    }
}

public sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> builder)
    {
        builder.ToTable("EventRegistrations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.AttendeeEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.RegisteredAtUtc)
            .HasConversion(new UtcDateTimeOffsetConverter())
            .IsRequired();

        // Foreign keys
        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Unique Index: An attendee cannot register twice for the same event
        builder.HasIndex(r => new { r.EventId, r.AttendeeId })
            .IsUnique();
    }
}
