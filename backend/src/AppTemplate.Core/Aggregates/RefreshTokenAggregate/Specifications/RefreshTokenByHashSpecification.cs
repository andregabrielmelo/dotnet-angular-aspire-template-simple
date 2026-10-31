namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

/// <summary>Tracked, because a refresh revokes the token it finds.</summary>
public class RefreshTokenByHashSpecification : SingleResultSpecification<RefreshToken>
{
    public RefreshTokenByHashSpecification(string tokenHash) =>
        Query.Where(token => token.TokenHash == tokenHash);
}
