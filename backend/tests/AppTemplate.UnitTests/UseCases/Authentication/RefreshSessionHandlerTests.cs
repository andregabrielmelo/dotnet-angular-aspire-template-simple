using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authentication.Refresh;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppTemplate.UnitTests.UseCases.Authentication;

public class RefreshSessionHandlerTests
{
    private readonly AuthenticationFixture _auth = new();
    private readonly User _user = AuthenticationFixture.SavedUser();

    public RefreshSessionHandlerTests()
    {
        _auth.Users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    private Task<Result<AuthSession>> Refresh(string value = "stored-refresh-token") =>
        new RefreshSessionHandler(
            _auth.RefreshTokens,
            _auth.Users,
            _auth.Sessions,
            AuthenticationFixture.Options,
            _auth.Time
        )
            .Handle(new RefreshSessionCommand(value), default)
            .AsTask();

    private void Stored(RefreshToken? token) =>
        _auth
            .RefreshTokens.FirstOrDefaultAsync(
                Arg.Any<RefreshTokenByHashSpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(token);

    [Fact]
    public async Task UnknownToken_IsUnauthorized()
    {
        Stored(null);

        Assert.Equal(ResultStatus.Unauthorized, (await Refresh()).Status);
    }

    [Fact]
    public async Task ActiveToken_RotatesWithinItsFamily_AndUsesTheCurrentRole()
    {
        var token = _auth.StoredToken();
        Stored(token);
        _user.ChangeRole(UserRole.Admin);

        var result = await Refresh();

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-token-1", result.Value.RefreshToken);
        Assert.Equal(RefreshTokenRevocationReason.Rotated, token.RevocationReason);
        await _auth
            .RefreshTokens.Received(1)
            .AddAsync(
                Arg.Is<RefreshToken>(next =>
                    next.FamilyId == token.FamilyId
                    && next.TokenHash == RefreshTokenHash.Of("refresh-token-1")
                ),
                Arg.Any<CancellationToken>()
            );
        _auth.TokenIssuer.Received(1).IssueAccessToken(_user, Permission.All);
    }

    [Fact]
    public async Task LosingAConcurrentRotation_IsAConflict()
    {
        Stored(_auth.StoredToken());
        _auth
            .RefreshTokens.AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyConflictException(new Exception()));

        var result = await Refresh();

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task TokenRotatedWithinTheGracePeriod_IsAConflict_AndRevokesNothing()
    {
        var token = _auth.StoredToken();
        token.Rotate(
            new string('b', RefreshToken.TokenHashLength),
            _auth.Time.GetUtcNow(),
            TimeSpan.FromDays(7)
        );
        Stored(token);
        _auth.Time.Advance(TimeSpan.FromSeconds(5));

        var result = await Refresh();

        Assert.Equal(ResultStatus.Conflict, result.Status);
        await _auth
            .RefreshTokens.DidNotReceive()
            .UpdateRangeAsync(Arg.Any<IEnumerable<RefreshToken>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenRotatedEarlier_IsReuse_AndRevokesItsFamily()
    {
        var token = _auth.StoredToken();
        var successor = token.Rotate(
            new string('b', RefreshToken.TokenHashLength),
            _auth.Time.GetUtcNow(),
            TimeSpan.FromDays(7)
        );
        Stored(token);
        _auth
            .RefreshTokens.ListAsync(
                Arg.Any<ActiveRefreshTokensByFamilySpecification>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([successor]);
        _auth.Time.Advance(TimeSpan.FromMinutes(1));

        var result = await Refresh();

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
        Assert.Equal(RefreshTokenRevocationReason.Reused, successor.RevocationReason);
        await _auth
            .RefreshTokens.Received(1)
            .UpdateRangeAsync(
                Arg.Is<IEnumerable<RefreshToken>>(family => family.Single() == successor),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task LoggedOutToken_IsUnauthorized()
    {
        var token = _auth.StoredToken();
        token.Revoke(_auth.Time.GetUtcNow(), RefreshTokenRevocationReason.LoggedOut);
        Stored(token);

        Assert.Equal(ResultStatus.Unauthorized, (await Refresh()).Status);
    }

    [Fact]
    public async Task ExpiredToken_IsUnauthorized()
    {
        Stored(_auth.StoredToken());
        _auth.Time.Advance(TimeSpan.FromDays(7));

        Assert.Equal(ResultStatus.Unauthorized, (await Refresh()).Status);
    }

    [Fact]
    public async Task DeletedUser_IsUnauthorized()
    {
        Stored(_auth.StoredToken());
        _auth.Users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        Assert.Equal(ResultStatus.Unauthorized, (await Refresh()).Status);
    }
}
