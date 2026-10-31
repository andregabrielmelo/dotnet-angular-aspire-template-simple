using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.SharedKernel;
using AppTemplate.UseCases.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace AppTemplate.UnitTests.UseCases.Authentication;

/// <summary>
/// The substitutes the authentication handlers share, and a real <see cref="SessionFactory"/>
/// over them, so tests see exactly which tokens were stored.
/// </summary>
public sealed class AuthenticationFixture
{
    public static readonly RefreshTokenOptions Options = new(
        SlidingLifetime: TimeSpan.FromDays(7),
        AbsoluteLifetime: TimeSpan.FromDays(30),
        ReuseGracePeriod: TimeSpan.FromSeconds(10)
    );

    private int _refreshTokenCount;

    public AuthenticationFixture()
    {
        TokenIssuer
            .IssueAccessToken(Arg.Any<User>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(_ => new AccessToken("access-token", Time.GetUtcNow().AddMinutes(15)));
        TokenIssuer
            .CreateRefreshTokenValue()
            .Returns(_ => $"refresh-token-{Interlocked.Increment(ref _refreshTokenCount)}");
        RefreshTokens
            .AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<RefreshToken>());
        PasswordHasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");
        PasswordHasher
            .Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(0) == $"hashed:{call.ArgAt<string>(1)}");
    }

    public FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    public IRepository<User> Users { get; } = Substitute.For<IRepository<User>>();
    public IRepository<RefreshToken> RefreshTokens { get; } =
        Substitute.For<IRepository<RefreshToken>>();
    public ITokenIssuer TokenIssuer { get; } = Substitute.For<ITokenIssuer>();
    public IPasswordHasher PasswordHasher { get; } = Substitute.For<IPasswordHasher>();

    public SessionFactory Sessions => new(RefreshTokens, TokenIssuer, Options, Time);

    /// <summary>A saved user with the given password.</summary>
    public static User SavedUser(
        string password = "secret-password",
        UserRole role = UserRole.Member
    )
    {
        var user = User.Create(
            UserName.From("Ada Lovelace"),
            new EmailAddress("ada@example.com"),
            $"hashed:{password}",
            role
        );
        user.Id = UserId.From(1);
        return user;
    }

    /// <summary>The active first token of a new family, stored for <paramref name="value"/>.</summary>
    public RefreshToken StoredToken(string value = "stored-refresh-token") =>
        RefreshToken.StartFamily(
            UserId.From(1),
            RefreshTokenHash.Of(value),
            Time.GetUtcNow(),
            Options.SlidingLifetime,
            Options.AbsoluteLifetime
        );
}
