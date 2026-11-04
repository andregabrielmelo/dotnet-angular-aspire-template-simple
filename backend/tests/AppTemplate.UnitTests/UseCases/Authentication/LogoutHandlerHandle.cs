using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Logout;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LogoutHandlerHandle
{
    private readonly IRepository<RefreshToken> _refreshTokens = Substitute.For<
        IRepository<RefreshToken>
    >();

    private Task<Result> Logout(string? token) =>
        new LogoutHandler(_refreshTokens)
            .Handle(new LogoutCommand(token), TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task DeletesTheSessionsRefreshToken()
    {
        var token = RefreshToken.Create(
            UserId.From(1),
            RefreshTokenHash.Of("plain-refresh-token"),
            DateTimeOffset.UtcNow.AddDays(1)
        );
        _refreshTokens
            .FirstOrDefaultAsync(
                Arg.Any<RefreshTokenByHashSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(token);

        (await Logout("plain-refresh-token")).IsSuccess.ShouldBeTrue();
        await _refreshTokens.Received(1).DeleteAsync(token, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-token")]
    public async Task SucceedsWithNothingToDelete(string? token)
    {
        (await Logout(token)).IsSuccess.ShouldBeTrue();
        await _refreshTokens
            .DidNotReceive()
            .DeleteAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }
}
