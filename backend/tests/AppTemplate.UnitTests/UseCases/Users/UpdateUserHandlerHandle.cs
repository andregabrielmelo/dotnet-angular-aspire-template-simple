using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users;
using AppTemplate.UseCases.Users.Update;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UnitTests.UseCases.Users;

public class UpdateUserHandlerHandle
{
    private static readonly string[] UserAndListTags = ["users:1", "users:list"];

    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly User _target = TestUsers.Saved(id: 1);

    private static readonly UpdateUserCommand Command = new(
        UserId.From(1),
        UserName.From("Grace Hopper"),
        null,
        null,
        null
    );

    public UpdateUserHandlerHandle()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns(_target);
    }

    private Task<Result<UserDto>> Update(UpdateUserCommand? command = null) =>
        new UpdateUserHandler(_repository, _currentUser, _cache)
            .Handle(command ?? Command, TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task UpdatesTheCallersOwnProfileWithoutPermissions()
    {
        _currentUser.Id.Returns(UserId.From(1));

        (await Update()).IsSuccess.ShouldBeTrue();
        _target.Name.ShouldBe(Command.UserName);
        await _repository.Received(1).UpdateAsync(_target, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DropsTheUsersCachedReadsAndTheListAfterSaving()
    {
        _currentUser.Id.Returns(UserId.From(1));

        await Update();

        Received.InOrder(() =>
        {
            _ = _repository.UpdateAsync(_target, Arg.Any<CancellationToken>());
            _ = _cache
                .RemoveByTagAsync(
                    Arg.Is<IEnumerable<string>>(tags => tags.SequenceEqual(UserAndListTags)),
                    Arg.Any<CancellationToken>()
                )
                .AsTask();
        });
    }

    [Fact]
    public async Task ReturnsForbiddenForSomeoneElseWithoutUsersWrite()
    {
        _currentUser.Id.Returns(UserId.From(2));
        _currentUser.HasPermission(Permission.UsersWrite).Returns(false);

        (await Update()).Status.ShouldBe(ResultStatus.Forbidden);
        _target.Name.ShouldBe(TestUsers.Name);
        _cache.ReceivedCalls().ShouldBeEmpty();
        await _repository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatesSomeoneElseWithUsersWrite()
    {
        _currentUser.Id.Returns(UserId.From(3));
        _currentUser.HasPermission(Permission.UsersWrite).Returns(true);

        (await Update()).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StoresAndReturnsAPhoneNumber()
    {
        _currentUser.Id.Returns(UserId.From(1));

        var result = await Update(
            Command with
            {
                PhoneNumber = "11 98765 4321",
                PhoneCountryCode = "+55",
                PhoneExtension = "12",
            }
        );

        var expected = new PhoneNumber("+55", "11 98765 4321", "12");
        _target.PhoneNumber.ShouldBe(expected);
        result.Value.PhoneNumber.ShouldBe(expected);
    }

    [Fact]
    public async Task FailsWithoutChangesGivenAPhoneNumberWithoutCountryCode()
    {
        _currentUser.Id.Returns(UserId.From(1));

        var result = await Update(Command with { PhoneNumber = "11 98765 4321" });

        result.Status.ShouldBe(ResultStatus.Error);
        _target.Name.ShouldBe(TestUsers.Name);
        _target.PhoneNumber.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNotFoundGivenAnUnknownId()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        (await Update()).Status.ShouldBe(ResultStatus.NotFound);
    }
}
