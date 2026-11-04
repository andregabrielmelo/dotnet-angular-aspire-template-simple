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
    public async Task DeletingAUserDeletesTheirRefreshTokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        await GetRepository<User>().AddAsync(user, cancellationToken);
        var token = NewRefreshToken(user.Id);
        await GetRepository<RefreshToken>().AddAsync(token, cancellationToken);

        await GetRepository<User>().DeleteAsync(user, cancellationToken);

        (
            await DbContext.RefreshTokens.AnyAsync(
                stored => stored.Id == token.Id,
                cancellationToken
            )
        ).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletingARowAnotherRequestDeletedFirstIsAConcurrencyConflict()
    {
        // How a refresh token is used once: of two requests deleting it, only one succeeds.
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        await GetRepository<User>().AddAsync(user, cancellationToken);
        var token = NewRefreshToken(user.Id);
        await GetRepository<RefreshToken>().AddAsync(token, cancellationToken);

        using var first = NewScope();
        using var second = NewScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IRepository<RefreshToken>>();
        var secondRepository = second.ServiceProvider.GetRequiredService<
            IRepository<RefreshToken>
        >();
        var firstCopy = await firstRepository.GetByIdAsync(token.Id, cancellationToken);
        var secondCopy = await secondRepository.GetByIdAsync(token.Id, cancellationToken);

        await firstRepository.DeleteAsync(firstCopy!, cancellationToken);

        await Should.ThrowAsync<ConcurrencyConflictException>(() =>
            secondRepository.DeleteAsync(secondCopy!, cancellationToken)
        );
    }
}
