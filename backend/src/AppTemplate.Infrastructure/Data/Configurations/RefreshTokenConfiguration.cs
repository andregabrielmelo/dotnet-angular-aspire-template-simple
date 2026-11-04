using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder
            .Property(entity => entity.Id)
            .ValueGeneratedOnAdd()
            .HasVogenConversion()
            .IsRequired();

        builder.Property(entity => entity.UserId).HasVogenConversion().IsRequired();

        builder
            .Property(entity => entity.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashLength)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(entity => entity.TokenHash).IsUnique();

        builder
            .Property(entity => entity.SecurityStamp)
            .HasMaxLength(RefreshToken.SecurityStampMaxLength)
            .IsRequired();

        // Deleting a user ends all of their sessions.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
