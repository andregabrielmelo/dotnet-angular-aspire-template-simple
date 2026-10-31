using System.Net;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.Web.Features.UserFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static UserId UnknownUser() => UserId.From(Random.Shared.Next(1_000_000, int.MaxValue));

    private Task<User> ProvisionAsync() => factory.CreateUserAsync();

    // --- Authentication -------------------------------------------------------------------

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- /users/me (any authenticated user) -------------------------------------------------

    [Fact]
    public async Task Me_ReturnsTheUserTheTokenNames()
    {
        var user = await factory.CreateUserAsync(email: $"me-{Guid.NewGuid():N}@example.com");

        var me = await factory
            .CreateAuthenticatedClient(user.Id)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");

        Assert.Equal(user.Id.Value, me!.Id);
        Assert.Equal(user.Email.Value, me.Email);
        Assert.Empty(me.Permissions);
    }

    [Fact]
    public async Task Me_ReturnsTheTokensPermissions()
    {
        var user = await ProvisionAsync();

        var me = await factory
            .CreateAuthenticatedClient(user.Id, Permission.UsersRead)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");

        Assert.Equal([Permission.UsersRead], me!.Permissions);
    }

    [Fact]
    public async Task Me_ForADeletedUser_ReturnsNotFound()
    {
        var response = await factory.CreateAuthenticatedClient(UnknownUser()).GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Permission policies ----------------------------------------------------------------

    [Fact]
    public async Task List_WithoutUsersRead_IsForbidden()
    {
        var response = await factory.CreateAuthenticatedClient(UnknownUser()).GetAsync("/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithUsersRead_ReturnsUsers()
    {
        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead)
            .GetAsync("/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutUsersRead_IsForbidden()
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser())
            .GetAsync($"/users/{me.Id.Value}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUsersRead_ReturnsTheUser()
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead)
            .GetAsync($"/users/{me.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead)
            .GetAsync("/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutUsersDelete_IsForbiddenAndKeepsTheUser()
    {
        var target = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead, Permission.UsersWrite)
            .DeleteAsync($"/users/{target.Id.Value}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stillThere = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead)
            .GetAsync($"/users/{target.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUsersDelete_RemovesTheUser()
    {
        var target = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersDelete)
            .DeleteAsync($"/users/{target.Id.Value}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Resource-based: update own profile vs. anyone's -------------------------------------

    [Fact]
    public async Task Update_OwnProfile_IsAllowedWithoutPermissions()
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(me.Id)
            .PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new { id = me.Id.Value, name = "Renamed Myself" }
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithPhoneNumber_EchoesItBack()
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(me.Id)
            .PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new
                {
                    name = "Ada Lovelace",
                    phoneNumber = "11 98765 4321",
                    phoneCountryCode = "+55",
                }
            );
        var body = await response.Content.ReadFromJsonAsync<UpdateUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("+55 11 98765 4321", body!.User.PhoneNumber);
    }

    [Fact]
    public async Task Update_WithPhoneNumberButNoCountryCode_IsRejected()
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(me.Id)
            .PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new { name = "Ada Lovelace", phoneNumber = "11 98765 4321" }
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("11 98765 4321", "55", null)] // country code without '+'
    [InlineData("11 98765 4321", "+5555", null)] // country code too long
    [InlineData("call me", "+55", null)] // not a phone number
    [InlineData("11 98765 4321", "+55", "ext12")] // extension isn't digits
    public async Task Update_WithInvalidPhoneParts_IsRejected(
        string phoneNumber,
        string phoneCountryCode,
        string? phoneExtension
    )
    {
        var me = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(me.Id)
            .PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new
                {
                    name = "Ada Lovelace",
                    phoneNumber,
                    phoneCountryCode,
                    phoneExtension,
                }
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UsesTheRouteId_EvenIfTheBodyNamesAnotherUser()
    {
        var me = await ProvisionAsync();
        var other = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(me.Id)
            .PutAsJsonAsync(
                $"/users/{me.Id.Value}",
                new { id = other.Id.Value, name = "Renamed Myself" }
            );
        var body = await response.Content.ReadFromJsonAsync<UpdateUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(me.Id.Value, body!.User.Id);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithoutUsersWrite_IsForbidden()
    {
        var target = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersRead)
            .PutAsJsonAsync(
                $"/users/{target.Id.Value}",
                new { id = target.Id.Value, name = "Hijacked" }
            );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithUsersWrite_IsAllowed()
    {
        var target = await ProvisionAsync();

        var response = await factory
            .CreateAuthenticatedClient(UnknownUser(), Permission.UsersWrite)
            .PutAsJsonAsync(
                $"/users/{target.Id.Value}",
                new { id = target.Id.Value, name = "Renamed By Admin" }
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
