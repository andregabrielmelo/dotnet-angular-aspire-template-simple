namespace AppTemplate.UseCases.Authentication;

/// <summary>Hashes and verifies passwords. Implemented in Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
