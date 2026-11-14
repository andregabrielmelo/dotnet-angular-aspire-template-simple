namespace AppTemplate.IntegrationTests.Data;

/// <summary>
/// Domain events are dispatched after a save, asynchronous or not. The fixture's dispatcher is a
/// substitute, one per scope, so each test checks what its own scope's save handed it.
/// </summary>
public class EventDispatchInterceptorSavedChanges(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private IDomainEventDispatcher Dispatcher =>
        Services.GetRequiredService<IDomainEventDispatcher>();

    [Fact]
    public async Task DispatchesAfterAnAsynchronousSave()
    {
        var user = await SavedUserWithANameChangeAsync();

        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Dispatcher
            .Received(1)
            .DispatchAndClearEvents(
                Arg.Is<IEnumerable<IHasDomainEvents>>(entities => entities.Single() == user)
            );
    }

    [Fact]
    public async Task DispatchesAfterASynchronousSave()
    {
        var user = await SavedUserWithANameChangeAsync();

        DbContext.SaveChanges();

        await Dispatcher
            .Received(1)
            .DispatchAndClearEvents(
                Arg.Is<IEnumerable<IHasDomainEvents>>(entities => entities.Single() == user)
            );
    }

    private async Task<User> SavedUserWithANameChangeAsync()
    {
        var user = NewUser();
        DbContext.Add(user);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Dispatcher.ClearReceivedCalls();

        user.UpdateName(UserName.From("Grace Hopper"));
        return user;
    }
}
