using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AppTemplate.FunctionalTests.Data;

/// <summary>
/// Behavior that only a real Postgres has - the reason these tests don't use an in-memory
/// database: unique indexes, database-generated values, owned types and raw SQL.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class PostgresPersistenceTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private static User NewUser(string externalId, string? email = null) =>
        User.Create(
            externalId,
            UserName.From("Ada Lovelace"),
            new EmailAddress(email ?? $"{externalId}@example.com")
        );

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<User> AddAsync(User user) =>
        InScopeAsync(services => services.GetRequiredService<IRepository<User>>().AddAsync(user));

    private Task<User?> LoadAsync(UserId id) =>
        InScopeAsync(services =>
            services
                .GetRequiredService<ApplicationDatabaseContext>()
                .Users.AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == id)
        );

    [Fact]
    public async Task Insert_WithDuplicateExternalId_ThrowsUniqueConstraintViolation()
    {
        var externalId = NewSubject();
        await AddAsync(NewUser(externalId));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddAsync(NewUser(externalId, email: $"other-{externalId}@example.com"))
        );
        AssertUniqueViolation(ex, "external_id");
    }

    [Fact]
    public async Task Insert_WithDuplicateEmail_ThrowsUniqueConstraintViolation()
    {
        var email = $"{NewSubject()}@example.com";
        await AddAsync(NewUser(NewSubject(), email));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddAsync(NewUser(NewSubject(), email))
        );
        AssertUniqueViolation(ex, "email");
    }

    private static void AssertUniqueViolation(DbUpdateException ex, string column)
    {
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains(column, postgres.ConstraintName);
    }

    [Fact]
    public async Task Insert_SetsCreatedAtUtcFromTheDatabaseClock()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var user = await AddAsync(NewUser(NewSubject()));
        var stored = await LoadAsync(user.Id);

        Assert.InRange(stored!.CreatedAtUtc, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task PhoneNumber_RoundTripsAsAnOwnedType()
    {
        var user = NewUser(NewSubject());
        user.UpdatePhoneNumber(new PhoneNumber("+55", "11 98765 4321", "12"));

        var saved = await AddAsync(user);
        var stored = await LoadAsync(saved.Id);

        Assert.Equal(new PhoneNumber("+55", "11 98765 4321", "12"), stored!.PhoneNumber);
    }

    [Fact]
    public async Task ListUsersQueryService_RawSqlMatchesTheSchema()
    {
        // FromSqlRaw must list every mapped column by its snake_case name - a renamed or added
        // column breaks it at runtime only, so run it for real.
        var user = NewUser(NewSubject());
        user.UpdatePhoneNumber(new PhoneNumber("+1", "555 0100", null));
        var saved = await AddAsync(user);

        var page = await InScopeAsync(services =>
            services.GetRequiredService<IListUsersQueryService>().ListAsync(1, 1000, default)
        );

        var listed = Assert.Single(page.Items, item => item.Id == saved.Id);
        Assert.Equal(user.PhoneNumber, listed.PhoneNumber);
    }

    [Fact]
    public async Task ListEndpoint_ReturnsProvisionedUsers()
    {
        var me = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetFromJsonAsync<CurrentUserResponse>("/users/me"); // provisions on first call

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetFromJsonAsync<UserPage>($"/users?page=1&per_page={Constants.MAX_PAGE_SIZE}");

        Assert.Contains(response!.Items, item => item.Id == me!.Id);
    }

    private sealed record UserPage(List<UserRecord> Items);
}
