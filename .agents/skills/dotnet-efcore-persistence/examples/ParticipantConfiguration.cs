using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyApp.Infrastructure.Persistence.Configurations;

public sealed class Participant
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Participant() { }

    public static Participant Create(string fullName, string email, DateTimeOffset nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            CreatedAtUtc = nowUtc
        };
}

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");

        // Explicit Primary Key with Domain-generated Guid
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        // Unique index on Email for data integrity
        builder.HasIndex(p => p.Email)
            .IsUnique();
    }
}
