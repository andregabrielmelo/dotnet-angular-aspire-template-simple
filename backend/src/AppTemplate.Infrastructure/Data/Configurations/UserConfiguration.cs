using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder
            .Property(entity => entity.Id)
            .HasValueGenerator<VogenIdValueGenerator<ApplicationDatabaseContext, User, UserId>>()
            .HasVogenConversion()
            .IsRequired();

        builder
            .Property(entity => entity.Name)
            .HasVogenConversion()
            .HasMaxLength(UserName.MaxLength)
            .IsRequired();

        builder
            .Property(entity => entity.Email)
            .HasConversion(email => email.Value, value => new EmailAddress(value))
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(entity => entity.Email).IsUnique();

        builder
            .Property(entity => entity.ExternalId)
            .HasMaxLength(User.ExternalIdMaxLength)
            .IsRequired();

        builder.HasIndex(entity => entity.ExternalId).IsUnique();

        // Set by Postgres on insert (EF leaves the column out while the value is unset); the
        // migration that added it backfilled existing rows with the migration time.
        builder.Property(entity => entity.CreatedAtUtc).HasDefaultValueSql("now()");

        builder.OwnsOne(builder => builder.PhoneNumber);
    }
}
