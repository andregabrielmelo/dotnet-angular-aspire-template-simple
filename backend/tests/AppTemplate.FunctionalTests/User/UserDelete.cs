namespace AppTemplate.FunctionalTests.User;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserDelete(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task DeletesTheUserGivenUsersDelete()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);

        await admin.Client.DeleteAndEnsureNoContentAsync($"/users/{target.Id.Value}");

        await admin.Client.GetAndEnsureNotFoundAsync($"/users/{target.Id.Value}");
    }

    [Fact]
    public async Task ReturnsForbiddenWithoutUsersDelete()
    {
        var target = await factory.CreateUserAsync();
        var (_, member) = await factory.CreateSignedInSessionAsync();

        await member.Client.DeleteAndEnsureForbiddenAsync($"/users/{target.Id.Value}");
    }
}
