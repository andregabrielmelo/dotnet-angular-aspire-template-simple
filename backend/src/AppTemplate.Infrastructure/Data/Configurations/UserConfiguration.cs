using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // The DbSet is UserProfiles (Identity's DbSet is Users); the table keeps the domain's name.
        builder.ToTable("users");

        builder
            .Property(entity => entity.Id)
            .ValueGeneratedOnAdd()
            .HasVogenConversion()
            .IsRequired();

        builder
            .Property(entity => entity.Name)
            .HasVogenConversion()
            .HasMaxLength(UserName.MaxLength)
            .IsRequired();

        // Set by Postgres on insert (EF leaves the column out while the value is unset).
        builder.Property(entity => entity.CreatedAtUtc).HasDefaultValueSql("now()");

        builder.OwnsOne(builder => builder.PhoneNumber);
    }
}
