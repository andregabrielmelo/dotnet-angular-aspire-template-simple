namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

/// <summary>Tracked, because consuming a refresh token deletes it.</summary>
public class RefreshTokenByHashSpecification : SingleResultSpecification<RefreshToken>
{
    public RefreshTokenByHashSpecification(string tokenHash) =>
        Query.Where(token => token.TokenHash == tokenHash);
}
