namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate.Specifications;

/// <summary>The family's tokens that are not revoked yet (tracked, to revoke them).</summary>
public class ActiveRefreshTokensByFamilySpecification : Specification<RefreshToken>
{
    public ActiveRefreshTokensByFamilySpecification(Guid familyId) =>
        Query.Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null);
}
