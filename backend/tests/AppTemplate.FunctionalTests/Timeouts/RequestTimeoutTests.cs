using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppTemplate.Infrastructure.Data;
using AppTemplate.UseCases;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.List;
using AppTemplate.Web.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Timeouts;

/// <summary>
/// A request past its timeout gets a 504 problem details response, and the cancellation
/// reaches the work it started: an EF Core query against Postgres and an outbound HttpClient
/// call both observe it and stop, instead of running on after the client got its answer.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RequestTimeoutTests(RequestTimeoutTests.ShortTimeoutFactory factory)
    : IClassFixture<RequestTimeoutTests.ShortTimeoutFactory>
{
    private HttpClient ClientWith(SlowQueryService service)
    {
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListUsersQueryService>();
                services.AddScoped<IListUsersQueryService>(provider =>
                {
                    service.Services = provider;
                    return service;
                });
            })
        );
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"sub-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, Permission.UsersRead);
        return client;
    }

    private static async Task AssertTimeoutProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(504, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task SlowDatabaseQuery_IsCancelledAndAnswered504()
    {
        var query = new SlowDatabaseQuery();
        var stopwatch = Stopwatch.StartNew();

        var response = await ClientWith(query).GetAsync("/users");

        await AssertTimeoutProblemAsync(response);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        var observed = await query.Observed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsAssignableFrom<OperationCanceledException>(observed);
    }

    [Fact]
    public async Task SlowOutboundHttpCall_IsCancelledAndAnswered504()
    {
        var call = new SlowHttpCall();

        var response = await ClientWith(call).GetAsync("/users");

        await AssertTimeoutProblemAsync(response);
        Assert.True(await call.Handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    public abstract class SlowQueryService : IListUsersQueryService
    {
        public IServiceProvider Services { get; set; } = default!;

        public abstract Task<PagedResult<UserDto>> ListAsync(
            int page,
            int perPage,
            CancellationToken cancellationToken
        );
    }

    /// <summary>Runs a 30-second query in Postgres and records how it ended.</summary>
    public sealed class SlowDatabaseQuery : SlowQueryService
    {
        public TaskCompletionSource<Exception> Observed { get; } = new();

        public override async Task<PagedResult<UserDto>> ListAsync(
            int page,
            int perPage,
            CancellationToken cancellationToken
        )
        {
            var context = Services.GetRequiredService<ApplicationDatabaseContext>();
            try
            {
                await context.Database.ExecuteSqlRawAsync("SELECT pg_sleep(30)", cancellationToken);
            }
            catch (Exception exception)
            {
                Observed.TrySetResult(exception);
                throw;
            }
            throw new InvalidOperationException("The query was expected to be cancelled.");
        }
    }

    /// <summary>Calls a stand-in service that never answers, through a real HttpClient.</summary>
    public sealed class SlowHttpCall : SlowQueryService
    {
        public NeverAnsweringHandler Handler { get; } = new();

        public override async Task<PagedResult<UserDto>> ListAsync(
            int page,
            int perPage,
            CancellationToken cancellationToken
        )
        {
            using var client = new HttpClient(Handler, disposeHandler: false);
            await client.GetAsync("https://slow.test/", cancellationToken);
            throw new InvalidOperationException("The call was expected to be cancelled.");
        }
    }

    public sealed class NeverAnsweringHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Cancelled { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult(true);
                throw;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    public sealed class ShortTimeoutFactory : AppTemplateWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RequestTimeouts:Default", "00:00:00.500");
        }
    }
}
