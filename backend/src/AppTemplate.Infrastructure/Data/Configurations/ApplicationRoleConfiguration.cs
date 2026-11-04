using AppTemplate.Infrastructure.Identity;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// The roles in <see cref="Roles"/>, seeded by the migrations so they exist in every environment.
/// Postgres doesn't advance an identity column for seeded ids, so add new roles here too, with
/// the next id, rather than at runtime.
/// </summary>
public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder) =>
        builder.HasData(
            new ApplicationRole
            {
                Id = 1,
                Name = Roles.Admin,
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "8d9a5c5e-3f71-4b0e-9c5a-0a6e2f0c1a01",
            },
            new ApplicationRole
            {
                Id = 2,
                Name = Roles.Member,
                NormalizedName = "MEMBER",
                ConcurrencyStamp = "2b7f9e41-6d3c-4a8e-8f12-5c4d3b2a1e02",
            }
        );
}
