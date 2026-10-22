using System.Net;
using System.Net.Http.Json;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.UserFeatures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppTemplate.FunctionalTests.Events;

/// <summary>
/// In-process domain events are dispatched by <c>EventDispatchInterceptor</c> right after
/// <c>SaveChanges</c> succeeds. These tests pin down what that means (see
/// docs/content/design-decisions.md): a failing handler can't undo the write, so it must
/// not fail the request either; and inside an explicit transaction, dispatch happens before
/// the transaction commits.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class DomainEventDispatchTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public async Task FailingDispatchAfterCommit_KeepsTheWriteAndStillSucceeds()
    {
        var dispatcher = new RecordingDispatcher
        {
            FailWith = new InvalidOperationException("handler failed"),
        };
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDomainEventDispatcher>();
                services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            })
        );
        var subject = $"sub-{Guid.NewGuid():N}";
        var meClient = app.CreateClient();
        meClient.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, subject);

        var response = await meClient.GetAsync("/users/me"); // provisions: an INSERT

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(dispatcher.Calls > 0);
        using var scope = app.Services.CreateScope();
        Assert.True(
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
                .Users.AnyAsync(user => user.ExternalId == subject)
        );
    }

    [Fact]
    public async Task InsideAnExplicitTransaction_DispatchRunsBeforeCommit()
    {
        var dispatcher = new RecordingDispatcher();
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDomainEventDispatcher>();
                services.AddSingleton<IDomainEventDispatcher>(dispatcher);
            })
        );
        var externalId = $"sub-{Guid.NewGuid():N}";
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Users.Add(
                User.Create(
                    externalId,
                    UserName.From("Ada"),
                    new EmailAddress($"{externalId}@example.com")
                )
            );
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // Dispatched, although the change it reports was rolled back: anything that must not
        // happen on rollback doesn't belong in a domain event handler.
        Assert.Equal(1, dispatcher.Calls);
        Assert.False(await context.Users.AnyAsync(user => user.ExternalId == externalId));
    }

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        private int _calls;

        public int Calls => _calls;

        public Exception? FailWith { get; init; }

        public Task DispatchAndClearEvents(IEnumerable<IHasDomainEvents> entitiesWithEvents)
        {
            Interlocked.Increment(ref _calls);
            return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
        }
    }
}
