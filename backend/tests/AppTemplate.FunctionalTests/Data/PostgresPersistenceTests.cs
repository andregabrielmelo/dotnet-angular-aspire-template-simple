using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
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
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static User NewUser(string? email = null) =>
        User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress(email ?? NewEmail()),
            "hashed-password"
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
    public async Task Insert_WithDuplicateEmail_IsAUniqueViolationOnTheEmailIndex()
    {
        var email = NewEmail();
        await AddAsync(NewUser(email));

        // The repository translates the Postgres error, keeping the constraint name.
        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            AddAsync(NewUser(email.ToUpperInvariant()))
        );
        Assert.Equal(UserConstraints.EmailUniqueIndex, ex.ConstraintName);
        var postgres = Assert.IsType<PostgresException>(ex.InnerException?.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [Fact]
    public async Task RefreshToken_UpdatedConcurrently_IsAConcurrencyConflict()
    {
        // Two requests read the same token; the second to save loses (xmin row version).
        var user = await AddAsync(NewUser());
        var token = RefreshToken.StartFamily(
            user.Id,
            new string('a', RefreshToken.TokenHashLength),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30)
        );
        await InScopeAsync(services =>
            services.GetRequiredService<IRepository<RefreshToken>>().AddAsync(token)
        );

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IRepository<RefreshToken>>();
        var secondRepository = second.ServiceProvider.GetRequiredService<
            IRepository<RefreshToken>
        >();
        var firstCopy = await firstRepository.GetByIdAsync(token.Id);
        var secondCopy = await secondRepository.GetByIdAsync(token.Id);

        firstCopy!.Revoke(DateTimeOffset.UtcNow, RefreshTokenRevocationReason.Rotated);
        await firstRepository.UpdateAsync(firstCopy);
        secondCopy!.Revoke(DateTimeOffset.UtcNow, RefreshTokenRevocationReason.LoggedOut);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            secondRepository.UpdateAsync(secondCopy)
        );
    }

    [Fact]
    public async Task DeletingAUser_DeletesTheirRefreshTokens()
    {
        var user = await AddAsync(NewUser());
        var token = RefreshToken.StartFamily(
            user.Id,
            new string('b', RefreshToken.TokenHashLength),
            DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30)
        );
        await InScopeAsync(services =>
            services.GetRequiredService<IRepository<RefreshToken>>().AddAsync(token)
        );

        await InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<IRepository<User>>();
            await users.DeleteAsync((await users.GetByIdAsync(user.Id))!);
            return 0;
        });

        Assert.False(
            await InScopeAsync(services =>
                services
                    .GetRequiredService<ApplicationDatabaseContext>()
                    .RefreshTokens.AnyAsync(stored => stored.Id == token.Id)
            )
        );
    }

    [Fact]
    public async Task Insert_SetsCreatedAtUtcFromTheDatabaseClock()
    {
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var user = await AddAsync(NewUser());
        var stored = await LoadAsync(user.Id);

        Assert.InRange(stored!.CreatedAtUtc, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task PhoneNumber_RoundTripsAsAnOwnedType()
    {
        var user = NewUser();
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
        var user = NewUser();
        user.UpdatePhoneNumber(new PhoneNumber("+1", "555 0100", null));
        var saved = await AddAsync(user);

        var page = await InScopeAsync(services =>
            services.GetRequiredService<IListUsersQueryService>().ListAsync(1, 1000, default)
        );

        var listed = Assert.Single(page.Items, item => item.Id == saved.Id);
        Assert.Equal(user.PhoneNumber, listed.PhoneNumber);
    }

    [Fact]
    public async Task ListEndpoint_ReturnsSavedUsers()
    {
        var saved = await AddAsync(NewUser());

        var response = await factory
            .CreateAuthenticatedClient(UserId.From(int.MaxValue), Permission.UsersRead)
            .GetFromJsonAsync<UserPage>($"/users?page=1&per_page={Constants.MAX_PAGE_SIZE}");

        Assert.Contains(response!.Items, item => item.Id == saved.Id.Value);
    }

    private sealed record UserPage(List<UserRecord> Items);
}
