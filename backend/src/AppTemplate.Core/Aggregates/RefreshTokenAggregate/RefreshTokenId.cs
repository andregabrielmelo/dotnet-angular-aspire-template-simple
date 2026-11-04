namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

// The database generates ids. EF Core hands a new row a negative temporary id and converts it
// (and every id it reads) through Vogen's deserialization, so that path skips Validate;
// RefreshTokenId.From(...) in code still enforces it.
[ValueObject<int>(deserializationStrictness: DeserializationStrictness.AllowAnything)]
public readonly partial struct RefreshTokenId
{
    private static Validation Validate(int value) =>
        value > 0 ? Validation.Ok : Validation.Invalid("RefreshTokenId must be positive.");
}
