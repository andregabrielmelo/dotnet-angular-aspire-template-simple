using AppTemplate.Web.Endpoints.Users;

namespace AppTemplate.FunctionalTests.User;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserUpdate(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private static JsonContent Body(object body) => JsonContent.Create(body);

    [Fact]
    public async Task UpdatesTheCallersOwnProfileWithoutPermissions()
    {
        var (me, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.PutAndDeserializeAsync<UpdateUserResponse>(
            $"/users/{me.Id.Value}",
            Body(new { name = "Renamed Myself" })
        );

        response.User.Name.ShouldBe("Renamed Myself");
    }

    [Fact]
    public async Task EchoesAPhoneNumberBack()
    {
        var (me, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.PutAndDeserializeAsync<UpdateUserResponse>(
            $"/users/{me.Id.Value}",
            Body(
                new
                {
                    name = "Ada Lovelace",
                    phoneNumber = "11 98765 4321",
                    phoneCountryCode = "+55",
                }
            )
        );

        response.User.PhoneNumber.ShouldBe("+55 11 98765 4321");
    }

    [Theory]
    [InlineData("11 98765 4321", null, null)] // a phone number needs a country code
    [InlineData("11 98765 4321", "55", null)] // country code without '+'
    [InlineData("call me", "+55", null)] // not a phone number
    [InlineData("11 98765 4321", "+55", "ext12")] // extension isn't digits
    public async Task ReturnsBadRequestGivenInvalidPhoneParts(
        string phoneNumber,
        string? phoneCountryCode,
        string? phoneExtension
    )
    {
        var (me, session) = await factory.CreateSignedInSessionAsync();

        await session.Client.PutAndEnsureBadRequestAsync(
            $"/users/{me.Id.Value}",
            Body(
                new
                {
                    name = "Ada Lovelace",
                    phoneNumber,
                    phoneCountryCode,
                    phoneExtension,
                }
            )
        );
    }

    [Fact]
    public async Task UsesTheRouteIdEvenIfTheBodyNamesAnotherUser()
    {
        var other = await factory.CreateUserAsync();
        var (me, session) = await factory.CreateSignedInSessionAsync();

        var response = await session.Client.PutAndDeserializeAsync<UpdateUserResponse>(
            $"/users/{me.Id.Value}",
            Body(new { id = other.Id.Value, name = "Renamed Myself" })
        );

        response.User.Id.ShouldBe(me.Id.Value);
    }

    [Fact]
    public async Task ReturnsForbiddenForSomeoneElseWithoutUsersWrite()
    {
        var target = await factory.CreateUserAsync();
        var (_, member) = await factory.CreateSignedInSessionAsync();

        await member.Client.PutAndEnsureForbiddenAsync(
            $"/users/{target.Id.Value}",
            Body(new { name = "Hijacked" })
        );
    }

    [Fact]
    public async Task UpdatesSomeoneElseGivenUsersWrite()
    {
        var target = await factory.CreateUserAsync();
        var (_, admin) = await factory.CreateSignedInSessionAsync(Role.Admin);

        var response = await admin.Client.PutAndDeserializeAsync<UpdateUserResponse>(
            $"/users/{target.Id.Value}",
            Body(new { name = "Renamed By Admin" })
        );

        response.User.Name.ShouldBe("Renamed By Admin");
    }
}
