using System.Net;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.Infrastructure.Data;
using AppTemplate.SharedKernel;
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
/// the transaction commits. Registration dispatches its event itself, after saving the user.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class DomainEventDispatchTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public async Task Registering_SendsTheWelcomeEmail()
    {
        var email = $"welcome-{Guid.NewGuid():N}@example.com";

        var response = await new BrowserSession(factory).RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var welcome = Assert.Single(factory.EmailSender.Sent, sent => sent.To == email);
        Assert.Equal("Welcome!", welcome.Subject);
    }

    [Fact]
    public async Task FailingHandlerAfterCommit_KeepsTheWriteAndStillSucceeds()
    {
        var emailSender = new FakeEmailSender
        {
            FailWith = new InvalidOperationException("SMTP is down"),
        };
        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
            })
        );
        var email = $"failing-{Guid.NewGuid():N}@example.com";

        var response = await new BrowserSession(app).RegisterAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = app.Services.CreateScope();
        Assert.True(
            await scope
                .ServiceProvider.GetRequiredService<ApplicationDatabaseContext>()
                .Users.AnyAsync(user => user.Email == new EmailAddress(email))
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
        var email = new EmailAddress($"rolled-back-{Guid.NewGuid():N}@example.com");
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDatabaseContext>();

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var user = User.Create(UserName.From("Ada"), email, "hashed-password");
            context.Users.Add(user); // assigns the id
            user.MarkRegistered();
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        // Dispatched, although the change it reports was rolled back: anything that must not
        // happen on rollback doesn't belong in a domain event handler.
        Assert.Equal(1, dispatcher.Calls);
        Assert.False(await context.Users.AnyAsync(user => user.Email == email));
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
