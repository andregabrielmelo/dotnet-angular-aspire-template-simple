using AppTemplate.Core.Aggregates.RefreshTokenAggregate;

namespace AppTemplate.UnitTests.Core.RefreshTokenAggregate;

public class RefreshTokenCreate
{
    private static readonly string Hash = new('a', RefreshToken.TokenHashLength);
    private static readonly DateTimeOffset ExpiresAt = new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SetsTheUserHashAndExpiry()
    {
        var token = RefreshToken.Create(UserId.From(1), Hash, ExpiresAt);

        token.UserId.ShouldBe(UserId.From(1));
        token.TokenHash.ShouldBe(Hash);
        token.ExpiresAtUtc.ShouldBe(ExpiresAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha256-hash")]
    public void ThrowsGivenAHashOfTheWrongLength(string hash)
    {
        Should.Throw<ArgumentException>(() => RefreshToken.Create(UserId.From(1), hash, ExpiresAt));
    }
}
