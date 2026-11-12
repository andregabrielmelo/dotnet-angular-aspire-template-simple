using AppTemplate.UseCases.Users.Commands.Delete;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UnitTests.UseCases.Users;

public class DeleteUserHandlerHandle
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();

    private Task<Result> Delete() =>
        new DeleteUserHandler(_repository, _cache)
            .Handle(new DeleteUserCommand(UserId.From(1)), TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task DeletesAnExistingUser()
    {
        var user = TestUsers.Saved();
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns(user);

        (await Delete()).IsSuccess.ShouldBeTrue();
        Received.InOrder(() =>
        {
            _ = _repository.DeleteAsync(user, Arg.Any<CancellationToken>());
            _ = _cache.RemoveByTagAsync(
                Arg.Is<IEnumerable<string>>(tags =>
                    tags.SequenceEqual(new[] { "users:1", "users:list" })
                ),
                Arg.Any<CancellationToken>()
            );
        });
    }

    [Fact]
    public async Task ReturnsNotFoundGivenAnUnknownId()
    {
        _repository.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        (await Delete()).Status.ShouldBe(ResultStatus.NotFound);
        await _repository
            .DidNotReceive()
            .DeleteAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        _cache.ReceivedCalls().ShouldBeEmpty();
    }
}
