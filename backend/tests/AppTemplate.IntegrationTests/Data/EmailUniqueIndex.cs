using Npgsql;

namespace AppTemplate.IntegrationTests.Data;

public class EmailUniqueIndex(PostgresDatabaseFixture database) : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task RejectsASecondUserWithTheSameEmailInAnyCase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await GetRepository<User>().AddAsync(NewUser(email), cancellationToken);

        using var scope = NewScope();
        var exception = await Should.ThrowAsync<UniqueConstraintViolationException>(() =>
            scope
                .ServiceProvider.GetRequiredService<IRepository<User>>()
                .AddAsync(NewUser(email.ToUpperInvariant()), cancellationToken)
        );

        exception.ConstraintName.ShouldBe("ix_users_email");
        exception.InnerException?.InnerException.ShouldBeOfType<PostgresException>();
    }
}
