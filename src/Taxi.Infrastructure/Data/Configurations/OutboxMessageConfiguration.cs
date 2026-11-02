using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Taxi.Infrastructure.Outbox;

namespace Taxi.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(m => m.Content)
               .IsRequired()
               .HasColumnType("jsonb");

        // The processor's query: pending messages in arrival order. Partial, so it stays small.
        builder.HasIndex(m => m.OccurredOnUtc)
               .HasFilter("\"ProcessedOnUtc\" IS NULL");
    }
}
