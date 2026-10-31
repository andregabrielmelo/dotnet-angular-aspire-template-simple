using AppTemplate.Core.Aggregates.RefreshTokenAggregate;

namespace AppTemplate.UnitTests.Core.RefreshTokenAggregate;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Sliding = TimeSpan.FromDays(7);
    private static readonly TimeSpan Absolute = TimeSpan.FromDays(30);

    private static string Hash(char c) => new(c, RefreshToken.TokenHashLength);

    private static RefreshToken Start() =>
        RefreshToken.StartFamily(UserId.From(1), Hash('a'), Now, Sliding, Absolute);

    [Fact]
    public void StartFamily_IsActiveWithSlidingExpiryAndAbsoluteFamilyExpiry()
    {
        var token = Start();

        Assert.True(token.IsActive(Now));
        Assert.Equal(Now + Sliding, token.ExpiresAtUtc);
        Assert.Equal(Now + Absolute, token.FamilyExpiresAtUtc);
        Assert.NotEqual(Guid.Empty, token.FamilyId);
    }

    [Fact]
    public void StartFamily_WithAHashOfTheWrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.StartFamily(UserId.From(1), "short", Now, Sliding, Absolute)
        );
    }

    [Fact]
    public void Separate_SignIns_StartSeparateFamilies()
    {
        Assert.NotEqual(Start().FamilyId, Start().FamilyId);
    }

    [Fact]
    public void IsActive_IsFalseOnceExpired()
    {
        Assert.False(Start().IsActive(Now + Sliding));
    }

    [Fact]
    public void Rotate_RevokesTheTokenAndReturnsTheNextInTheSameFamily()
    {
        var token = Start();
        var later = Now.AddDays(1);

        var next = token.Rotate(Hash('b'), later, Sliding);

        Assert.False(token.IsActive(later));
        Assert.Equal(RefreshTokenRevocationReason.Rotated, token.RevocationReason);
        Assert.Equal(token.FamilyId, next.FamilyId);
        Assert.Equal(token.UserId, next.UserId);
        Assert.NotEqual(token.Id, next.Id);
        Assert.True(next.IsActive(later));
        Assert.Equal(later + Sliding, next.ExpiresAtUtc);
    }

    [Fact]
    public void Rotate_NeverExtendsPastTheFamilysAbsoluteExpiry()
    {
        var token = Start();
        var nearTheEnd = Now + Absolute - TimeSpan.FromDays(1);

        // Refreshed every few days, the session still ends at the absolute expiry.
        var current = token;
        for (var at = Now.AddDays(5); at < nearTheEnd; at = at.AddDays(5))
        {
            current = current.Rotate(Hash('b'), at, Sliding);
        }
        var last = current.Rotate(Hash('c'), nearTheEnd, Sliding);

        Assert.Equal(Now + Absolute, last.ExpiresAtUtc);
        Assert.False(last.IsActive(Now + Absolute));
    }

    [Fact]
    public void Rotate_ARevokedToken_Throws()
    {
        var token = Start();
        token.Revoke(Now, RefreshTokenRevocationReason.LoggedOut);

        Assert.Throws<InvalidOperationException>(() => token.Rotate(Hash('b'), Now, Sliding));
    }

    [Fact]
    public void Revoke_KeepsTheFirstReason()
    {
        var token = Start();

        token.Revoke(Now, RefreshTokenRevocationReason.Rotated);
        token.Revoke(Now.AddMinutes(1), RefreshTokenRevocationReason.Reused);

        Assert.Equal(RefreshTokenRevocationReason.Rotated, token.RevocationReason);
        Assert.Equal(Now, token.RevokedAtUtc);
    }

    [Fact]
    public void WasRotatedWithin_OnlyForARecentRotation()
    {
        var grace = TimeSpan.FromSeconds(10);
        var rotated = Start();
        rotated.Rotate(Hash('b'), Now, Sliding);
        var loggedOut = Start();
        loggedOut.Revoke(Now, RefreshTokenRevocationReason.LoggedOut);

        Assert.True(rotated.WasRotatedWithin(grace, Now.AddSeconds(10)));
        Assert.False(rotated.WasRotatedWithin(grace, Now.AddSeconds(11)));
        Assert.False(loggedOut.WasRotatedWithin(grace, Now));
        Assert.False(Start().WasRotatedWithin(grace, Now));
    }
}
