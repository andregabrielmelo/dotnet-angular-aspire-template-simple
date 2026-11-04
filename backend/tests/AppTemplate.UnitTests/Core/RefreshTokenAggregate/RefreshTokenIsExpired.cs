using AppTemplate.Core.Aggregates.RefreshTokenAggregate;

namespace AppTemplate.UnitTests.Core.RefreshTokenAggregate;

public class RefreshTokenIsExpired
{
    private static readonly DateTimeOffset ExpiresAt = new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

    private static RefreshToken CreateToken() =>
        RefreshToken.Create(
            UserId.From(1),
            new string('a', RefreshToken.TokenHashLength),
            ExpiresAt
        );

    [Fact]
    public void ReturnsFalseBeforeTheExpiry() =>
        CreateToken().IsExpired(ExpiresAt.AddSeconds(-1)).ShouldBeFalse();

    [Fact]
    public void ReturnsTrueAtTheExpiry() => CreateToken().IsExpired(ExpiresAt).ShouldBeTrue();
}
