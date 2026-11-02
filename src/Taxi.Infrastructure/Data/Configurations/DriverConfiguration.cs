using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Taxi.Domain.Cars;
using Taxi.Domain.Drivers;

namespace Taxi.Infrastructure.Data.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FullName)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(d => d.LicenseNumber)
               .IsRequired()
               .HasMaxLength(50);

        builder.HasIndex(d => d.LicenseNumber)
               .IsUnique();

        // A relationship to another aggregate, by id only: no navigation on either side. Restrict,
        // because RemoveCarCommandHandler refuses to delete an assigned car (and cars are soft-deleted).
        builder.HasOne<Car>()
               .WithMany()
               .HasForeignKey(d => d.AssignedCarId)
               .OnDelete(DeleteBehavior.Restrict);

        // One driver per car; NULLs (unassigned drivers) do not collide.
        builder.HasIndex(d => d.AssignedCarId)
               .IsUnique()
               .HasFilter("\"AssignedCarId\" IS NOT NULL");

        // A child collection owned by the aggregate, in its own table. Its LocalizedText cannot be a
        // JSON column here — EF Core does not support OwnsOne(...).ToJson() inside OwnsMany(...).ToTable(...)
        // — so the two languages are flat columns. The domain still sees one LocalizedText.
        builder.OwnsMany(d => d.Certifications, certification =>
        {
            certification.ToTable("DriverCertifications");
            certification.WithOwner().HasForeignKey("DriverId");
            certification.HasKey(c => c.Id);
            certification.Property(c => c.Id).ValueGeneratedNever();

            certification.OwnsOne(c => c.Name, name =>
            {
                name.Property(n => n.En).HasColumnName("NameEn").HasMaxLength(100).IsRequired();
                name.Property(n => n.Ar).HasColumnName("NameAr").HasMaxLength(100).IsRequired();
            });

            certification.Property(c => c.ExpiresOn).IsRequired();
        });

        builder.Navigation(d => d.Certifications).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.UseXminConcurrencyToken();
    }
}
