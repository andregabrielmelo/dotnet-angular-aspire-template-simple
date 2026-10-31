using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authentication.Logout;
using Ardalis.Result;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class LogoutHandlerTests
{
    private readonly AuthenticationFixture _auth = new();

    private Task<Result> Logout() =>
        new LogoutHandler(_auth.RefreshTokens, _auth.Time)
            .Handle(new LogoutCommand("stored-refresh-token"), default)
            .AsTask();

    private RefreshToken StoreFamily()
    {
        var token = _auth.StoredToken();
        _auth
            .RefreshTokens.FirstOrDefaultAsync(
                Arg.Any<RefreshTokenByHashSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(token);
        _auth
            .RefreshTokens.ListAsync(
                Arg.Any<ActiveRefreshTokensByFamilySpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([token]);
        return token;
    }

    [Fact]
    public async Task UnknownToken_Succeeds_WithNothingToRevoke()
    {
        var result = await Logout();

        Assert.True(result.IsSuccess);
        await _auth
            .RefreshTokens.DidNotReceive()
            .UpdateRangeAsync(Arg.Any<IEnumerable<RefreshToken>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KnownToken_RevokesItsFamily()
    {
        var token = StoreFamily();

        var result = await Logout();

        Assert.True(result.IsSuccess);
        Assert.Equal(RefreshTokenRevocationReason.LoggedOut, token.RevocationReason);
        await _auth
            .RefreshTokens.Received(1)
            .UpdateRangeAsync(Arg.Any<IEnumerable<RefreshToken>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentRefresh_IsAConflict()
    {
        StoreFamily();
        _auth
            .RefreshTokens.UpdateRangeAsync(
                Arg.Any<IEnumerable<RefreshToken>>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ConcurrencyConflictException(new Exception()));

        Assert.Equal(ResultStatus.Conflict, (await Logout()).Status);
    }
}
