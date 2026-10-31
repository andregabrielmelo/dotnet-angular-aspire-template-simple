using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;

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

        // Emails are stored lower-cased (EmailAddress), so this is unique regardless of case.
        builder
            .HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName(UserConstraints.EmailUniqueIndex);

        builder
            .Property(entity => entity.PasswordHash)
            .HasMaxLength(User.PasswordHashMaxLength)
            .IsRequired();

        builder.Property(entity => entity.Role).HasConversion<string>().HasMaxLength(32);

        // Set by Postgres on insert (EF leaves the column out while the value is unset).
        builder.Property(entity => entity.CreatedAtUtc).HasDefaultValueSql("now()");

        builder.OwnsOne(builder => builder.PhoneNumber);
    }
}
