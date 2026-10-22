using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.GetOrCreateCurrent;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetOrCreateCurrentUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly TestMetrics _metrics = new();

    private static readonly GetOrCreateCurrentUserCommand Command = new(
        "keycloak-sub-1",
        UserName.From("Ada Lovelace"),
        new EmailAddress("ada@example.com")
    );

    private GetOrCreateCurrentUserHandler CreateHandler() => new(_repository, _metrics.Application);

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsItWithoutCreating()
    {
        var existingUser = User.Create(Command.ExternalId, Command.Name, Command.Email);
        existingUser.Id = UserId.From(1); // as if loaded from the database
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(existingUser);

        using var provisioned = _metrics.Collect<long>("users.provisioned");

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Command.Name, result.Value.Name);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        Assert.Empty(provisioned.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task Handle_WithNewIdentity_CreatesUser()
    {
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((User?)null);
        _repository
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());
        using var provisioned = _metrics.Collect<long>("users.provisioned");

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Assert.Single(provisioned.GetMeasurementSnapshot()).Value);
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => u.ExternalId == Command.ExternalId && u.Email == Command.Email),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_WithEmailLinkedToAnotherIdentity_ReturnsConflict()
    {
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((User?)null);
        _repository
            .AnyAsync(Arg.Any<UserByEmailSpecification>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(Command, CancellationToken.None);

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await _repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewUser_RaisesUserProvisioned()
    {
        _repository
            .FirstOrDefaultAsync(
                Arg.Any<UserByExternalIdSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((User?)null);
        _repository
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        await CreateHandler().Handle(Command, CancellationToken.None);

        // Raised for whatever should happen to new users, dispatched once the user is saved.
        await _repository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.DomainEvents.OfType<UserProvisioned>().Single().ExternalId
                    == Command.ExternalId
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
