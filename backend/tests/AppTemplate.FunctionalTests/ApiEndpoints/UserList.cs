using AppTemplate.Web.Users;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserList(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    public sealed record UserPage(List<UserRecord> Items, int Page, int PerPage, int TotalCount);

    [Fact]
    public async Task ReturnsAPageOfUsersGivenUsersRead()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Roles.Admin);

        var page = await admin.Client.GetAndDeserializeAsync<UserPage>(
            "/users?page=1&per_page=100"
        );

        page.Items.ShouldContain(item => item.Id == target.Id.Value);
        page.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ReturnsForbiddenWithoutUsersRead()
    {
        var (_, member) = await factory.CreateSignedInSessionAsync();

        await member.Client.GetAndEnsureForbiddenAsync("/users");
    }

    [Fact]
    public async Task ReturnsUnauthorizedWithoutAnAccessToken()
    {
        await new TestSession(factory).Client.GetAndEnsureUnauthorizedAsync("/users");
    }
}
