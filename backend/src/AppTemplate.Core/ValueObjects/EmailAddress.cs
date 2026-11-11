using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace AppTemplate.Core.ValueObjects;

// https://www.w3.org/Protocols/rfc822/#z8
public class EmailAddress : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled
    );
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
}
