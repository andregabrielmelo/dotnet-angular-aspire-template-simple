namespace AppTemplate.IntegrationTests.Data;

public class EfRepositoryAdd(PostgresDatabaseFixture database) : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task AddsUserAndSetsId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = GetRepository<User>();
        var user = NewUser();

        await repository.AddAsync(user, cancellationToken);

        var saved = await repository.GetByIdAsync(user.Id, cancellationToken);
        saved.ShouldNotBeNull();
        saved.Name.ShouldBe(user.Name);
        saved.Id.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AddsTwoUsersWithDistinctDbGeneratedIds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = GetRepository<User>();
        var first = NewUser();
        var second = NewUser();

        await repository.AddAsync(first, cancellationToken);
        await repository.AddAsync(second, cancellationToken);

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task SetsCreatedAtUtcFromTheDatabaseClock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var user = NewUser();

        await GetRepository<User>().AddAsync(user, cancellationToken);

        using var scope = NewScope();
        var stored = await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .UserProfiles.AsNoTracking()
            .SingleAsync(saved => saved.Id == user.Id, cancellationToken);
        stored.CreatedAtUtc.ShouldBeInRange(before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task RoundTripsThePhoneNumberAsAnOwnedType()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        user.UpdatePhoneNumber(new PhoneNumber("+55", "11 98765 4321", "12"));

        await GetRepository<User>().AddAsync(user, cancellationToken);

        using var scope = NewScope();
        var stored = await scope
            .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
            .UserProfiles.AsNoTracking()
            .SingleAsync(saved => saved.Id == user.Id, cancellationToken);
        stored.PhoneNumber.ShouldBe(new PhoneNumber("+55", "11 98765 4321", "12"));
    }

    [Fact]
    public async Task AddsARefreshTokenForAUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        await GetRepository<User>().AddAsync(user, cancellationToken);
        var token = NewRefreshToken(user.Id);

        await GetRepository<RefreshToken>().AddAsync(token, cancellationToken);

        token.Id.Value.ShouldBeGreaterThan(0);
    }
}
