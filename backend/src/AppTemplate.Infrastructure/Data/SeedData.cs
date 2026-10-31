using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Authentication;

namespace AppTemplate.Infrastructure.Data;

/// <summary>Development-only demo data (see DatabaseConfigurations.StartDatabase).</summary>
public class SeedData
{
    public const int NUMBER_OF_SEED_USERS = 25;

    /// <summary>An account with the Admin role, for trying the users pages locally.</summary>
    public const string AdminEmail = "admin@example.com";
    public const string AdminPassword = "Admin123!";

    /// <summary>The shared password of the fixture users (user1@example.com, ...).</summary>
    public const string FixturePassword = "Passw0rd!";

    public static async Task InitializeAsync(
        ApplicationDatabaseContext dbContext,
        IPasswordHasher passwordHasher
    )
    {
        if (await dbContext.Users.AnyAsync())
            return; // DB has been seeded

        await PopulateTestDataAsync(dbContext, passwordHasher);
    }

    public static async Task PopulateTestDataAsync(
        ApplicationDatabaseContext dbContext,
        IPasswordHasher passwordHasher
    )
    {
        dbContext.Users.Add(
            User.Create(
                UserName.From("Admin"),
                new EmailAddress(AdminEmail),
                passwordHasher.Hash(AdminPassword),
                UserRole.Admin
            )
        );

        // Hashing is deliberately slow, so the fixtures share one hash.
        var fixtureHash = passwordHasher.Hash(FixturePassword);
        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            dbContext.Users.Add(
                User.Create(
                    UserName.From($"User {i}"),
                    new EmailAddress($"user{i}@example.com"),
                    fixtureHash
                )
            );
        }

        await dbContext.SaveChangesAsync();
    }
}
