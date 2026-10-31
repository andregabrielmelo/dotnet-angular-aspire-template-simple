using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Created in the domain (RefreshTokenId.New), never by the database.
        builder.Property(entity => entity.Id).HasVogenConversion().ValueGeneratedNever();

        builder.Property(entity => entity.UserId).HasVogenConversion().IsRequired();

        builder
            .Property(entity => entity.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashLength)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(entity => entity.TokenHash).IsUnique();

        builder.HasIndex(entity => entity.FamilyId);

        builder
            .Property(entity => entity.RevocationReason)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Postgres' xmin system column as the row version: an UPDATE only applies when nobody
        // changed the row since it was read. This is what makes refresh-token rotation atomic.
        builder.Property<uint>("xmin").IsRowVersion();

        // Deleting a user ends all of their sessions.
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
