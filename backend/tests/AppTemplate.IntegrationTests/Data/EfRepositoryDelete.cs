namespace AppTemplate.IntegrationTests.Data;

public class EfRepositoryDelete(PostgresDatabaseFixture database) : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task DeletesAUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = GetRepository<User>();
        var user = NewUser();
        await repository.AddAsync(user, cancellationToken);

        await repository.DeleteAsync(user, cancellationToken);

        (await repository.GetByIdAsync(user.Id, cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingARowAnotherRequestDeletedFirstIsAConcurrencyConflict()
    {
        // Of two requests deleting the same user, only one succeeds.
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        await GetRepository<User>().AddAsync(user, cancellationToken);

        using var first = NewScope();
        using var second = NewScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IRepository<User>>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IRepository<User>>();
        var firstCopy = await firstRepository.GetByIdAsync(user.Id, cancellationToken);
        var secondCopy = await secondRepository.GetByIdAsync(user.Id, cancellationToken);

        await firstRepository.DeleteAsync(firstCopy!, cancellationToken);

        await Should.ThrowAsync<ConcurrencyConflictException>(() =>
            secondRepository.DeleteAsync(secondCopy!, cancellationToken)
        );
    }
}
