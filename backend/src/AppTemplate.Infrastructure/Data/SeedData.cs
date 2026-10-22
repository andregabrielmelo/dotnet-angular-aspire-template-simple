using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Infrastructure.Data;

public class SeedData
{
    public const int NUMBER_OF_SEED_USERS = 25;

    public static async Task InitializeAsync(ApplicationDatabaseContext dbContext)
    {
        if (await dbContext.Users.AnyAsync())
            return; // DB has been seeded

        await PopulateTestDataAsync(dbContext);
    }

    public static async Task PopulateTestDataAsync(ApplicationDatabaseContext dbContext)
    {
        var names = new List<string>();
        for (int i = 1; i <= NUMBER_OF_SEED_USERS; i++)
        {
            names.Add($"User {i}");
        }

        // Seed users are fixtures for pagination demos, not accounts anyone can log into - their
        // ExternalIds don't match any identity in Keycloak.
        var users = names.Select(
            (name, index) =>
                User.Create(
                    $"seed-{index + 1}",
                    UserName.From(name),
                    new EmailAddress($"{name.ToLowerInvariant().Replace(" ", "")}@example.com")
                )
        );
        dbContext.Users.AddRange(users);
        await dbContext.SaveChangesAsync();
    }
}
