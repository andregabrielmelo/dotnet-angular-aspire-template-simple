using AppTemplate.Core.Aggregates.RefreshTokenAggregate;

namespace AppTemplate.UnitTests.Core.RefreshTokenAggregate;

public class RefreshTokenIsStale
{
    private static readonly RefreshToken Token = RefreshToken.Create(
        UserId.From(1),
        new string('a', RefreshToken.TokenHashLength),
        "STAMP-1",
        new DateTimeOffset(2026, 1, 8, 0, 0, 0, TimeSpan.Zero)
    );

    [Fact]
    public void IsFreshWhileTheAccountKeepsItsStamp() => Token.IsStale("STAMP-1").ShouldBeFalse();

    [Fact]
    public void IsStaleOnceTheAccountsStampChanged() => Token.IsStale("STAMP-2").ShouldBeTrue();
}
