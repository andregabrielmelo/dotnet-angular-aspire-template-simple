using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>The user signing in with this email, if any. Emails are stored lower-cased.</summary>
public class UserByEmailSpecification : SingleResultSpecification<User>
{
    public UserByEmailSpecification(EmailAddress email) =>
        Query.Where(user => user.Email == email).AsNoTracking();
}
