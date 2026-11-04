using AppTemplate.UseCases.Authorization;

namespace AppTemplate.FunctionalTests.ApiEndpoints;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserMe(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    public sealed record MeBody(int Id, string Name, string Email, List<string> Permissions);

    [Fact]
    public async Task ReturnsTheSignedInUserWithoutPermissionsForAMember()
    {
        var (user, session) = await factory.CreateSignedInSessionAsync();

        var me = await session.Client.GetAndDeserializeAsync<MeBody>("/users/me");

        me.Id.ShouldBe(user.Id.Value);
        me.Email.ShouldBe(user.Email.Value);
        me.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsEveryPermissionForAnAdmin()
    {
        var (_, session) = await factory.CreateSignedInSessionAsync(Roles.Admin);

        var me = await session.Client.GetAndDeserializeAsync<MeBody>("/users/me");

        me.Permissions.ShouldBe(Permission.All.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ReturnsUnauthorizedWithoutAnAccessToken()
    {
        await new TestSession(factory).Client.GetAndEnsureUnauthorizedAsync("/users/me");
    }
}
