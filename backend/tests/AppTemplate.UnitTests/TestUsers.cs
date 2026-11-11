using AppTemplate.UseCases.Authentication;
using AppTemplate.UseCases.Authorization;

namespace AppTemplate.UnitTests;

/// <summary>Profiles and accounts as they'd come back from the database.</summary>
public static class TestUsers
{
    public static readonly UserName Name = UserName.From("Ada Lovelace");
    public static readonly EmailAddress Email = new("ada@example.com");

    public static User Saved(int id = 1)
    {
        var user = User.Create(Name);
        user.Id = UserId.From(id);
        return user;
    }

    public static AccountDto Account(int id = 1, Role? role = null) =>
        new(UserId.From(id), Email, EmailConfirmed: true, [(role ?? Role.Member).Name]);
}
