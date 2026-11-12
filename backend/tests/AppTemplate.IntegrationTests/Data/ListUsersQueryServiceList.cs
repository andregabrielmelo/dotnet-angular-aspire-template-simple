using AppTemplate.UseCases.Users.Queries.List;

namespace AppTemplate.IntegrationTests.Data;

public class ListUsersQueryServiceList(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    [Fact]
    public async Task ReadsSavedUsersThroughItsRawSql()
    {
        // FromSqlRaw must list every mapped column by its snake_case name: a renamed or added
        // column only breaks it at runtime, so run it for real.
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = NewUser();
        user.UpdatePhoneNumber(new PhoneNumber("+1", "555 0100", null));
        await GetRepository<User>().AddAsync(user, cancellationToken);

        var page = await Services
            .GetRequiredService<IListUsersQueryService>()
            .ListAsync(1, 1000, cancellationToken);

        var listed = page.Items.Single(item => item.Id == user.Id);
        listed.PhoneNumber.ShouldBe(user.PhoneNumber);
    }
}
