using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate.Specifications;

/// <summary>Read-only existence check (used by GetOrCreateCurrentUserHandler) - AsNoTracking.</summary>
public class UserByEmailSpecification : Specification<User>
{
    public UserByEmailSpecification(EmailAddress email) =>
        Query.Where(user => user.Email == email).AsNoTracking();
}
