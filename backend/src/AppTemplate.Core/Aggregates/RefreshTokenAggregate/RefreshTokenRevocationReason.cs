namespace AppTemplate.Core.Aggregates.RefreshTokenAggregate;

public enum RefreshTokenRevocationReason
{
    /// <summary>Exchanged for the next token in its family; presenting it again is reuse.</summary>
    Rotated,

    /// <summary>Its family was revoked because a rotated token of the family was presented again.</summary>
    Reused,

    /// <summary>Its family was revoked when the user signed out.</summary>
    LoggedOut,
}
