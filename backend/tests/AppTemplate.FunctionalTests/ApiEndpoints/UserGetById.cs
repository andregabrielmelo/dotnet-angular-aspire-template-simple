using AppTemplate.Web.Users;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserGetById(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task ReturnsTheUserGivenUsersRead()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Roles.Admin);

        var user = await admin.Client.GetAndDeserializeAsync<UserRecord>(
            $"/users/{target.Id.Value}"
        );

        user.Id.ShouldBe(target.Id.Value);
        user.Name.ShouldBe(target.Name.Value);
    }

    [Fact]
    public async Task ReturnsForbiddenWithoutUsersRead()
    {
        var target = await factory.CreateUserAsync();
        var (_, member) = await factory.CreateSignedInSessionAsync();

        await member.Client.GetAndEnsureForbiddenAsync($"/users/{target.Id.Value}");
    }

    [Fact]
    public async Task ReturnsNotFoundGivenAnUnknownId()
    {
        var (_, admin) = await factory.CreateSignedInSessionAsync(Roles.Admin);

        await admin.Client.GetAndEnsureNotFoundAsync("/users/999999");
    }

    [Fact]
    public async Task ReturnsBadRequestGivenIdZero()
    {
        var (_, admin) = await factory.CreateSignedInSessionAsync(Roles.Admin);

        await admin.Client.GetAndEnsureBadRequestAsync("/users/0");
    }
}
