using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Taxi.Domain.Cars;

namespace Taxi.Infrastructure.Data.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Make)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(c => c.Model)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(c => c.Year)
               .IsRequired();

        builder.OwnsOne(c => c.Description, description =>
        {
            description.ToJson();
            description.Property(d => d.En).IsRequired();
            description.Property(d => d.Ar).IsRequired();
        });

        builder.HasIndex(c => new { c.Make, c.Model });
    }
}
