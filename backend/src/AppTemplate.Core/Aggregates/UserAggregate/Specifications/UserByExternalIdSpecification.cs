namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>Looks a user up by the OpenID Connect provider's <c>sub</c> claim.</summary>
public class UserByExternalIdSpecification : SingleResultSpecification<User>
{
    public UserByExternalIdSpecification(string externalId) =>
        Query.Where(user => user.ExternalId == externalId);
}
