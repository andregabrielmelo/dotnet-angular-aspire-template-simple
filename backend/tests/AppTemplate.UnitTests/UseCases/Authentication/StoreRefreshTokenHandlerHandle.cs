using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.StoreRefreshToken;
using Microsoft.Extensions.Time.Testing;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class StoreRefreshTokenHandlerHandle
{
    private readonly IRepository<RefreshToken> _refreshTokens = Substitute.For<
        IRepository<RefreshToken>
    >();
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );

    public StoreRefreshTokenHandlerHandle() =>
        _identity
            .GetAccountAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Account());

    private Task<Result> Store(string token) =>
        new StoreRefreshTokenHandler(_refreshTokens, _identity, _time)
            .Handle(
                new StoreRefreshTokenCommand(UserId.From(1), token, _time.GetUtcNow().AddDays(7)),
                TestContext.Current.CancellationToken
            )
            .AsTask();

    [Fact]
    public async Task StoresOnlyTheHashOfTheTokenWithTheAccountsSecurityStamp()
    {
        _refreshTokens
            .ListAsync(
                Arg.Any<ExpiredRefreshTokensByUserSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);

        var result = await Store("plain-refresh-token");

        result.IsSuccess.ShouldBeTrue();
        await _refreshTokens
            .Received(1)
            .AddAsync(
                Arg.Is<RefreshToken>(token =>
                    token.TokenHash == RefreshTokenHash.Of("plain-refresh-token")
                    && token.UserId == UserId.From(1)
                    && token.SecurityStamp == TestUsers.SecurityStamp
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task DeletesTheUsersExpiredTokens()
    {
        var expired = RefreshToken.Create(
            UserId.From(1),
            new string('e', RefreshToken.TokenHashLength),
            TestUsers.SecurityStamp,
            _time.GetUtcNow().AddDays(-1)
        );
        _refreshTokens
            .ListAsync(
                Arg.Any<ExpiredRefreshTokensByUserSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([expired]);

        await Store("plain-refresh-token");

        await _refreshTokens
            .Received(1)
            .DeleteRangeAsync(
                Arg.Is<IEnumerable<RefreshToken>>(tokens => tokens.Single() == expired),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ReturnsNotFoundWithoutAnAccount()
    {
        _identity
            .GetAccountAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns((AccountDto?)null);

        (await Store("plain-refresh-token")).Status.ShouldBe(ResultStatus.NotFound);
        await _refreshTokens
            .DidNotReceive()
            .AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }
}
