using AppTemplate.Core.Aggregates.RefreshTokenAggregate;

namespace AppTemplate.UnitTests.Core.RefreshTokenAggregate;

public class RefreshTokenCreate
{
    private static readonly string Hash = new('a', RefreshToken.TokenHashLength);
    private static readonly DateTimeOffset ExpiresAt = new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetsTheUserHashStampAndExpiry()
    {
        var token = RefreshToken.Create(UserId.From(1), Hash, "STAMP", ExpiresAt);

        token.UserId.ShouldBe(UserId.From(1));
        token.TokenHash.ShouldBe(Hash);
        token.SecurityStamp.ShouldBe("STAMP");
        token.ExpiresAtUtc.ShouldBe(ExpiresAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha256-hash")]
    public void ThrowsGivenAHashOfTheWrongLength(string hash)
    {
        Should.Throw<ArgumentException>(() =>
            RefreshToken.Create(UserId.From(1), hash, "STAMP", ExpiresAt)
        );
    }

    [Fact]
    public void ThrowsGivenNoSecurityStamp()
    {
        Should.Throw<ArgumentException>(() =>
            RefreshToken.Create(UserId.From(1), Hash, string.Empty, ExpiresAt)
        );
    }
}
