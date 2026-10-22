using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Errors;

/// <summary>
/// Every failure, whatever produces it, is an RFC 9457 problem details response with a
/// <c>traceId</c> (ADR 014): FastEndpoints validation, use case results, authentication and
/// authorization, unmatched routes and unhandled exceptions.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class ProblemDetailsContractTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static string NewSubject() => $"sub-{Guid.NewGuid():N}";

    /// <summary>Asserts the response is a problem details document and returns its body.</summary>
    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        return body;
    }

    [Fact]
    public async Task ValidatorFailure_IsAValidationProblemKeyedByJsonFieldName()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync("/users/0");

        var body = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(
            "One or more validation errors occurred.",
            body.GetProperty("title").GetString()
        );
        var errors = body.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.GetProperty("id").ValueKind);
        Assert.Contains(
            "Id must be greater than zero",
            errors.GetProperty("id").EnumerateArray().Select(e => e.GetString())
        );
    }

    [Fact]
    public async Task Unauthenticated_IsA401Problem()
    {
        var response = await factory.CreateClient().GetAsync("/users/me");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingPermission_IsA403Problem()
    {
        var response = await factory.CreateAuthenticatedClient(NewSubject()).GetAsync("/users");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ForbiddenUseCaseResult_IsA403Problem()
    {
        var target = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetFromJsonAsync<CurrentUserResponse>("/users/me");

        // Another user's profile without users:write is rejected by the use case itself.
        var response = await factory
            .CreateAuthenticatedClient(NewSubject())
            .PutAsJsonAsync($"/users/{target!.Id}", new { id = target.Id, name = "Hijacked" });

        var body = await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(
            "You don't have permission to perform this operation.",
            body.GetProperty("detail").GetString()
        );
    }

    [Fact]
    public async Task NotFoundUseCaseResult_IsA404Problem()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject(), Permission.UsersRead)
            .GetAsync($"/users/{int.MaxValue}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConflictUseCaseResult_IsA409ProblemWithTheUseCaseMessage()
    {
        // Another identity already owns the email this caller's token carries.
        var subject = NewSubject();
        using (var scope = factory.Services.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<IRepository<User>>()
                .AddAsync(
                    User.Create(
                        NewSubject(),
                        UserName.From("Earlier Account"),
                        new EmailAddress($"{subject}@example.com")
                    )
                );
        }

        var response = await factory.CreateAuthenticatedClient(subject).GetAsync("/users/me");

        var body = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(
            "Email is already linked to another user",
            body.GetProperty("detail").GetString()
        );
    }

    [Fact]
    public async Task UnmatchedRoute_IsA404Problem()
    {
        var response = await factory
            .CreateAuthenticatedClient(NewSubject())
            .GetAsync("/no-such-route");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnhandledException_IsA500ProblemWithoutExceptionDetails()
    {
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListUsersQueryService>();
                services.AddSingleton<IListUsersQueryService, ThrowingListUsersQueryService>();
            })
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, NewSubject());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, Permission.UsersRead);

        var response = await client.GetAsync("/users");

        var body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        var raw = body.GetRawText();
        Assert.DoesNotContain(ThrowingListUsersQueryService.Secret, raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("   at ", raw); // no stack trace
    }

    [Fact]
    public async Task ValidationAndExceptionResponses_FromOneHost_KeepTheirOwnBodies()
    {
        // The exception handler, status code pages and FastEndpoints' validation response all
        // run in one pipeline; none may overwrite, empty or double-write another's body.
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListUsersQueryService>();
                services.AddSingleton<IListUsersQueryService, ThrowingListUsersQueryService>();
            })
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, NewSubject());
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, Permission.UsersRead);

        var invalid = await AssertProblemAsync(
            await client.GetAsync("/users/0"),
            HttpStatusCode.BadRequest
        );
        var failed = await AssertProblemAsync(
            await client.GetAsync("/users"),
            HttpStatusCode.InternalServerError
        );

        Assert.Equal(
            ["errors", "status", "title", "traceId", "type"],
            invalid.EnumerateObject().Select(p => p.Name).Order()
        );
        Assert.False(failed.TryGetProperty("errors", out _));
        Assert.Equal(
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            failed.GetProperty("type").GetString()
        );
    }

    private sealed class ThrowingListUsersQueryService : IListUsersQueryService
    {
        public const string Secret = "connection-string-password=hunter2";

        public Task<PagedResult<UserDto>> ListAsync(
            int page,
            int perPage,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException(Secret);
    }
}
