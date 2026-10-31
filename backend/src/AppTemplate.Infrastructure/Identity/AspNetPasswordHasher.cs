using AppTemplate.UseCases.Authentication;
using Microsoft.AspNetCore.Identity;

namespace AppTemplate.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity's password hasher (PBKDF2 with a per-password salt) on its own, without
/// the rest of Identity. It needs no user to hash with, so none is passed.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly object NoUser = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(NoUser, passwordHash, password)
            is not PasswordVerificationResult.Failed;
}
