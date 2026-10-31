namespace AppTemplate.UseCases.Users;

/// <summary>
/// Database constraint names a use case reacts to. Infrastructure names its indexes with these,
/// so the two can't drift apart.
/// </summary>
public static class UserConstraints
{
    /// <summary>The unique index on the user's email.</summary>
    public const string EmailUniqueIndex = "ix_users_email";
}
