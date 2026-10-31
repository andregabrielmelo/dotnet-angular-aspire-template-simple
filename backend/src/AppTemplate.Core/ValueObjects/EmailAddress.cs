using System.Text.RegularExpressions;

namespace AppTemplate.Core.ValueObjects;

// https://www.w3.org/Protocols/rfc822/#z8
[PersonalData]
public class EmailAddress : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled
    );
    public string Value { get; private set; }

    /// <summary>Whether <paramref name="value"/> makes a valid address; for request validators.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailRegex.IsMatch(value.Trim());

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");

        // Stored lower-cased, so the unique index treats Ada@Example.com and ada@example.com
        // as the same address and sign-in doesn't depend on how the user typed it.
        var normalized = value.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(normalized))
            throw new ArgumentException("Invalid email format");

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
}
