namespace AppTemplate.Core.Aggregates.UserAggregate;

// The database generates ids. EF Core hands a new row a negative temporary id and converts it
// (and every id it reads) through Vogen's deserialization, so that path skips Validate;
// UserId.From(...) in code still enforces it.
[ValueObject<int>(deserializationStrictness: DeserializationStrictness.AllowAnything)]
public readonly partial struct UserId
{
    private static Validation Validate(int value) =>
        value > 0 ? Validation.Ok : Validation.Invalid("UserId must be positive.");
}
