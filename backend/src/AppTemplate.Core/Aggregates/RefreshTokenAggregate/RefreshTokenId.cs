namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

/// <summary>
/// Created in the domain (a version 7 GUID) rather than by the database, so concurrent sign-ins
/// can never be handed the same id.
/// </summary>
[ValueObject<Guid>]
public readonly partial struct RefreshTokenId
{
    public static RefreshTokenId New() => From(Guid.CreateVersion7());

    private static Validation Validate(Guid value) =>
        value != Guid.Empty ? Validation.Ok : Validation.Invalid("RefreshTokenId cannot be empty.");
}
