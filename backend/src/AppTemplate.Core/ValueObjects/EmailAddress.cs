using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace AppTemplate.Core.ValueObjects;

/// <summary>
/// A value object representing an email address.
/// </summary>
/// <remarks>
/// See more details about <see href="https://www.w3.org/Protocols/rfc822/#z8">email address specification</see>
/// </remarks>
public partial class EmailAddress : ValueObject
{
    public string Value { get; private set; }

    public EmailAddress(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, message: "Email cannot be empty");

        // Stored lower-cased, so the unique index treats Ada@Example.com and ada@example.com
        // as the same address and sign-in doesn't depend on how the user typed it.
        var normalized = value.Trim().ToLowerInvariant();
        Guard.Against.InvalidInput(
            normalized,
            nameof(value),
            EmailRegex.IsMatch,
            "Invalid email format"
        );

        Value = normalized;
    }

    public override string ToString()
    {
        return Value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailValidationRegex();

    private static readonly Regex EmailRegex = EmailValidationRegex();
}
