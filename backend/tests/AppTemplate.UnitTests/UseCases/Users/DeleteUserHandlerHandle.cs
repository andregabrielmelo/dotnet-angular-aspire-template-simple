using AppTemplate.UseCases.Users.Commands.Delete;

namespace AppTemplate.UnitTests.UseCases.Users;

public class DeleteUserHandlerHandle
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private Task<Result> Delete() =>
        new DeleteUserHandler(_repository)
            .Handle(new DeleteUserCommand(UserId.From(1)), TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task DeletesAnExistingUser()
    {
        var user = TestUsers.Saved();
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns(user);

        (await Delete()).IsSuccess.ShouldBeTrue();
        await _repository.Received(1).DeleteAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNotFoundGivenAnUnknownId()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        (await Delete()).Status.ShouldBe(ResultStatus.NotFound);
        await _repository
            .DidNotReceive()
            .DeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
