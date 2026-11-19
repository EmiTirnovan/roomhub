using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Orders;

namespace MyApp.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        // 1. Value Object mapped via Value Converter
        builder.Property(o => o.CustomerEmail)
            .HasConversion(
                email => email.Value,
                value => CustomerEmail.Create(value).Value)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(o => o.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.Quantity)
            .IsRequired();

        // 2. Complex Property for Value Object (EF Core 8/9/10)
        // Stores Amount as decimal and Currency as text in the same table
        builder.ComplexProperty(o => o.UnitPrice, moneyBuilder =>
        {
            moneyBuilder.Property(m => m.Amount)
                .HasColumnName("UnitPriceAmount")
                .HasPrecision(18, 2)
                .IsRequired();

            moneyBuilder.Property(m => m.Currency)
                .HasColumnName("UnitPriceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.ComplexProperty(o => o.TotalAmount, moneyBuilder =>
        {
            moneyBuilder.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();

            moneyBuilder.Property(m => m.Currency)
                .HasColumnName("TotalCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // 3. Enum mapped as string for readable SQLite storage
        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // 4. Timestamps
        builder.Property(o => o.CreatedAtUtc)
            .IsRequired();

        builder.Property(o => o.CancelledAtUtc)
            .IsRequired(false);

        // Indexes for performance
        builder.HasIndex(o => o.CustomerEmail);
        builder.HasIndex(o => o.Status);
    }
}
