namespace AppTemplate.UnitTests;

/// <summary>Users as they'd come back from the database.</summary>
public static class TestUsers
{
    public static readonly UserName Name = UserName.From("Ada Lovelace");
    public static readonly EmailAddress Email = new("ada@example.com");

    public static User Saved(
        int id = 1,
        UserRole role = UserRole.Member,
        string passwordHash = "hash"
    )
    {
        var user = User.Create(Name, Email, passwordHash, role);
        user.Id = UserId.From(id);
        return user;
    }
}
