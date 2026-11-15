using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Interfaces;
using AppTemplate.UseCases.Users.GetCurrent;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetCurrentUserHandlerHandle
{
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();

    private Task<Result<CurrentUserDto>> Get() =>
        new GetCurrentUserHandler(_users, _identity)
            .Handle(new GetCurrentUserQuery(UserId.From(1)), TestContext.Current.CancellationToken)
            .AsTask();

    private void ProfileIs(User? profile) =>
        _users
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(profile);

    [Fact]
    public async Task CombinesTheProfileWithTheAccountsEmail()
    {
        ProfileIs(TestUsers.Saved());
        _identity
            .GetUserAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Account());

        var result = await Get();

        result.Value.ShouldBe(new CurrentUserDto(UserId.From(1), TestUsers.Name, TestUsers.Email));
    }

    [Fact]
    public async Task ReturnsNotFoundForADeletedUser()
    {
        ProfileIs(null);

        (await Get()).Status.ShouldBe(ResultStatus.NotFound);
    }
}
