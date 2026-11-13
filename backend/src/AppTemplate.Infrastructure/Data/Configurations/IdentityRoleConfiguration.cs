using AppTemplate.UseCases.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppTemplate.Infrastructure.Data.Configurations;

/// <summary>
/// The Identity roles, seeded by the migrations from <c>Role.List</c> so they exist in every
/// environment: the role's <c>Value</c> is the row id. Postgres doesn't advance an identity column
/// for seeded ids, so roles are only ever added in <see cref="Role"/>, never at runtime. A new role
/// also needs a fixed concurrency stamp here, so the seeded row never changes between migrations.
/// </summary>
public class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<int>>
{
    private static readonly Dictionary<Role, string> ConcurrencyStamps = new()
    {
        [Role.Admin] = "8d9a5c5e-3f71-4b0e-9c5a-0a6e2f0c1a01",
        [Role.Member] = "2b7f9e41-6d3c-4a8e-8f12-5c4d3b2a1e02",
    };

    public void Configure(EntityTypeBuilder<IdentityRole<int>> builder) =>
        builder.HasData(
            Role.List.Select(role => new IdentityRole<int>
            {
                Id = role.Value,
                Name = role.Name,
                NormalizedName = role.Name.ToUpperInvariant(),
                ConcurrencyStamp = ConcurrencyStamps[role],
            })
        );
}
