using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.Web.Features.UserFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.UserFeatures;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class UserEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    private async Task<CurrentUserResponse> ProvisionAsync(
        string subject,
        params string[] permissions
    )
    {
        var me = await factory
            .CreateAuthenticatedClient(subject, permissions)
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");
        return me!;
    }

    // --- Authentication -------------------------------------------------------------------

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- /users/me (any authenticated user) -------------------------------------------------

    [Fact]
    public async Task Me_OnFirstCall_ProvisionsTheUserFromTokenClaims()
    {
        var subject = NewSubject();

        var me = await ProvisionAsync(subject);

        Assert.Equal($"Test {subject}", me.Name);
        Assert.Equal($"{subject}@example.com", me.Email);
        Assert.Empty(me.Permissions);
    }

    [Fact]
    public async Task Me_CalledTwice_ReturnsTheSameUser()
    {
        var client = factory.CreateAuthenticatedClient(NewSubject());

        var first = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");
        var second = await client.GetFromJsonAsync<CurrentUserResponse>("/users/me");

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Me_ReturnsOnlyKnownPermissions()
    {
        var me = await ProvisionAsync(NewSubject(), Permission.UsersRead, "not-a-permission");

        Assert.Equal([Permission.UsersRead], me.Permissions);
    }

    // --- Permission policies ----------------------------------------------------------------

    [Fact]
    public async Task List_WithoutUsersRead_IsForbidden()
    {
        var response = await factory.CreateAuthenticatedClient(NewSubject()).GetAsync("/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithUsersRead_ReturnsUsers()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithoutUsersRead_IsForbidden()
    {
        var me = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetAsync($"/users/{me.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUsersRead_ReturnsTheUser()
    {
        var me = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync($"/users/{me.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutUsersDelete_IsForbiddenAndKeepsTheUser()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead, Permission.UsersWrite)
            .DeleteAsync($"/users/{target.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stillThere = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync($"/users/{target.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUsersDelete_RemovesTheUser()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersDelete)
            .DeleteAsync($"/users/{target.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // --- Resource-based: update own profile vs. anyone's -------------------------------------

    [Fact]
    public async Task Update_OwnProfile_IsAllowedWithoutPermissions()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync($"/users/{me.Id}", new { id = me.Id, name = "Renamed Myself" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithPhoneNumber_EchoesItBack()
    {
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync(
                $"/users/{me.Id}",
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
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync(
                $"/users/{me.Id}",
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
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync(
                $"/users/{me.Id}",
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
        var subject = NewSubject();
        var me = await ProvisionAsync(subject);
        var other = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(subject)
            .PutAsJsonAsync($"/users/{me.Id}", new { id = other.Id, name = "Renamed Myself" });
        var body = await response.Content.ReadFromJsonAsync<UpdateUserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(me.Id, body!.User.Id);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithoutUsersWrite_IsForbidden()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .PutAsJsonAsync($"/users/{target.Id}", new { id = target.Id, name = "Hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_SomeoneElse_WithUsersWrite_IsAllowed()
    {
        var target = await ProvisionAsync(NewSubject());

        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersWrite)
            .PutAsJsonAsync(
                $"/users/{target.Id}",
                new { id = target.Id, name = "Renamed By Admin" }
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
