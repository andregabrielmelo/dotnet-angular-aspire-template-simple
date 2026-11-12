namespace AppTemplate.UseCases.Constants;

// TOD: Revise to see if there is not a better place for this, e.g., in Infrastructure.Identity. The rules are used by both the Identity service and the request validators, so they need to be accessible to both.
/// <summary>
/// Length only, no composition rules, as NIST SP 800-63B advises. Identity enforces the minimum
/// (configured in Infrastructure); request validators reject anything longer than the maximum.
/// </summary>
public static class PasswordConstants
{
    public const int MIN_LENGTH = 8;

    // Hashing cost grows with length; nobody needs more.
    public const int MAX_LENGTH = 128;
}
