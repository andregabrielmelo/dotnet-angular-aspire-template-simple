namespace AppTemplate.UseCases.Authentication;

/// <summary>
/// Length only, no composition rules, as NIST SP 800-63B advises. Identity enforces the minimum
/// (configured in Infrastructure); request validators reject anything longer than the maximum.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    // Hashing cost grows with length; nobody needs more.
    public const int MaxLength = 128;
}
