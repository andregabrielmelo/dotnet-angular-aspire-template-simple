namespace AppTemplate.Core.Aggregates.UserAggregate;

public enum UserRole
{
    /// <summary>A registered user: may read and update their own profile.</summary>
    Member,

    /// <summary>May manage every user.</summary>
    Admin,
}
