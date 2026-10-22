using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Users.Get;
using Ardalis.Result;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Users;

public class GetUserHandlerTests
{
    private readonly IRepository<User> _repository = Substitute.For<IRepository<User>>();

    private static readonly GetUserQuery Query = new(UserId.From(1));

    private static User CreateUser()
    {
        var user = User.Create(
            "sub-1",
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com")
        );
        user.UpdatePhoneNumber(new PhoneNumber("+44", "5551234", "7"));
        user.Id = UserId.From(1); // as if loaded from the database
        return user;
    }

    private void RepositoryReturns(User? user) =>
        _repository
            .FirstOrDefaultAsync(Arg.Any<UserByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(user);

    [Fact]
    public async Task Handle_ExistingUser_ReturnsIt()
    {
        RepositoryReturns(CreateUser());

        var result = await new GetUserHandler(_repository).Handle(Query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserName.From("Ada Lovelace"), result.Value.Name);
        Assert.Equal("+44 5551234 x7", result.Value.PhoneNumber!.ToString());
    }

    [Fact]
    public async Task Handle_MissingUser_IsNotFound()
    {
        RepositoryReturns(null);

        var result = await new GetUserHandler(_repository).Handle(Query, CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
