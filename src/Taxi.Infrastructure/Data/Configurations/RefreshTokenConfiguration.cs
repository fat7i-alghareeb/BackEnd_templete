using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Taxi.Domain.Identity;

namespace Taxi.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(r => r.UserId)
               .IsRequired();

        // Get-only properties are NOT mapped by convention — every one must be listed. ExpiresOnUtc
        // was once missing, so it was never stored and every refresh token read back as expired.
        builder.Property(r => r.ExpiresOnUtc)
               .IsRequired();

        // RefreshTokenCommandHandler looks a token up by value; TokenProvider deletes by user.
        builder.HasIndex(r => r.Token)
               .IsUnique();

        builder.HasIndex(r => r.UserId);
    }
}