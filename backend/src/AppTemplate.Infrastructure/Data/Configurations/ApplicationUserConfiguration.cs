using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTemplate.Infrastructure.Data.Configurations;

/// <summary>Identity configures the rest of the account (IdentityDbContext); this links it to its profile.</summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(account => account.ProfileId).HasVogenConversion().IsRequired();
        builder.HasIndex(account => account.ProfileId).IsUnique();

        // Deleting a user (their profile) deletes their account, and Identity's own foreign keys
        // take its roles, claims and tokens with it.
        builder
            .HasOne(account => account.Profile)
            .WithOne()
            .HasForeignKey<ApplicationUser>(account => account.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
