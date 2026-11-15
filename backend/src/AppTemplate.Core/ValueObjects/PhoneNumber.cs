using Ardalis.GuardClauses;

namespace AppTemplate.Core.ValueObjects;

/// <summary>
/// A value object representing a phone number: a country code, the number, and an optional
/// extension. The constructor refuses a blank country code or number, so every instance is valid,
/// including those EF Core loads for the owned type and System.Text.Json reads back from the cache.
/// </summary>
public class PhoneNumber : ValueObject
{
    public string CountryCode { get; private set; }
    public string Number { get; private set; }
    public string? Extension { get; private set; }

    public PhoneNumber(string countryCode, string number, string? extension)
    {
        CountryCode = Guard.Against.NullOrWhiteSpace(countryCode, nameof(countryCode));
        Number = Guard.Against.NullOrWhiteSpace(number, nameof(number));
        Extension = extension;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return CountryCode;
        yield return Number;
        yield return Extension ?? String.Empty;
    }

    public override string ToString()
    {
        return $"{CountryCode} {Number}{(string.IsNullOrEmpty(Extension) ? String.Empty : $" x{Extension}")}";
    }
}
