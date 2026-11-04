using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.ConsumeRefreshToken;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class ConsumeRefreshTokenHandlerHandle
{
    private const string Token = "plain-refresh-token";

    private readonly IRepository<RefreshToken> _refreshTokens = Substitute.For<
        IRepository<RefreshToken>
    >();
    private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );

    private RefreshToken StoredToken(TimeSpan expiresIn)
    {
        var token = RefreshToken.Create(
            UserId.From(1),
            RefreshTokenHash.Of(Token),
            _time.GetUtcNow() + expiresIn
        );
        _refreshTokens
            .FirstOrDefaultAsync(
                Arg.Any<RefreshTokenByHashSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(token);
        return token;
    }

    private Task<Result<AuthenticatedUserDto>> Consume(string token = Token) =>
        new ConsumeRefreshTokenHandler(_refreshTokens, _users, _time)
            .Handle(new ConsumeRefreshTokenCommand(token), TestContext.Current.CancellationToken)
            .AsTask();

    [Fact]
    public async Task DeletesTheTokenAndReturnsItsUserWithTheirCurrentRole()
    {
        var token = StoredToken(TimeSpan.FromDays(1));
        _users
            .GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>())
            .Returns(TestUsers.Saved(role: UserRole.Admin));

        var result = await Consume();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Role.ShouldBe(UserRole.Admin);
        await _refreshTokens.Received(1).DeleteAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAnUnknownToken()
    {
        _refreshTokens
            .FirstOrDefaultAsync(
                Arg.Any<RefreshTokenByHashSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((RefreshToken?)null);

        (await Consume()).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenNoToken()
    {
        (await Consume(string.Empty)).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ReturnsUnauthorizedGivenAnExpiredTokenAndStillDeletesIt()
    {
        var token = StoredToken(TimeSpan.FromDays(-1));

        (await Consume()).Status.ShouldBe(ResultStatus.Unauthorized);
        await _refreshTokens.Received(1).DeleteAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenAConcurrentRequestUsedTheTokenFirst()
    {
        StoredToken(TimeSpan.FromDays(1));
        _refreshTokens
            .DeleteAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException(new Exception()));

        (await Consume()).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task ReturnsUnauthorizedWhenTheUserWasDeleted()
    {
        StoredToken(TimeSpan.FromDays(1));
        _users.GetByIdAsync(UserId.From(1), Arg.Any<CancellationToken>()).Returns((User?)null);

        (await Consume()).Status.ShouldBe(ResultStatus.Unauthorized);
    }
}
