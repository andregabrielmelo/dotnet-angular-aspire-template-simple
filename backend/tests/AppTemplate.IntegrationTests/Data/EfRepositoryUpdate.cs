namespace AppTemplate.IntegrationTests.Data;

public class EfRepositoryUpdate(PostgresDatabaseFixture database) : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task UpdatesTheUsersName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        await GetRepository<User>().AddAsync(user, cancellationToken);

        using (var scope = NewScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            var loaded = await repository.GetByIdAsync(user.Id, cancellationToken);
            loaded!.UpdateName(UserName.From("Grace Hopper"));
            await repository.UpdateAsync(loaded, cancellationToken);
        }

        using var check = NewScope();
        var updated = await check
            .ServiceProvider.GetRequiredService<IRepository<User>>()
            .GetByIdAsync(user.Id, cancellationToken);
        updated!.Name.ShouldBe(UserName.From("Grace Hopper"));
    }
}
