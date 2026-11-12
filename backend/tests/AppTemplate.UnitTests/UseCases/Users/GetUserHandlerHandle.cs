using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Users.Queries.Get;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetUserHandlerHandle
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private Task<Result<AppTemplate.UseCases.Users.UserDto>> Get() =>
        new GetUserHandler(_repository)
            .Handle(new GetUserQuery(UserId.From(1)), TestContext.Current.CancellationToken)
            .AsTask();

    private void RepositoryReturns(User? user) =>
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(user);

    [Fact]
    public async Task ReturnsTheUserWithTheirPhoneNumber()
    {
        var user = TestUsers.Saved();
        user.UpdatePhoneNumber(new PhoneNumber("+44", "5551234", "7"));
        RepositoryReturns(user);

        var result = await Get();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(TestUsers.Name);
        result.Value.PhoneNumber!.ToString().ShouldBe("+44 5551234 x7");
    }

    [Fact]
    public async Task ReturnsNotFoundGivenAnUnknownId()
    {
        RepositoryReturns(null);

        (await Get()).Status.ShouldBe(ResultStatus.NotFound);
    }
}
