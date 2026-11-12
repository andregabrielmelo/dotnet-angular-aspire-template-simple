namespace AppTemplate.SharedKernel;

/// <summary>
/// A save broke a unique constraint, such as a second user with the same email. Thrown by the
/// repository instead of the database provider's exception, so use cases can turn the
/// constraints they expect into a <c>Conflict</c> result without depending on EF Core.
/// </summary>
public sealed class UniqueConstraintViolationException(string constraintName, Exception inner)
    : Exception($"The unique constraint '{constraintName}' was violated.", inner)
{
    /// <summary>The database constraint or index name, such as <c>ix_users_email</c>.</summary>
    public string ConstraintName { get; } = constraintName;
}

/// <summary>
/// A save changed a row that another request changed (or deleted) after it was read, so
/// nothing was saved. Thrown by the repository for EF Core's optimistic concurrency check.
/// </summary>
public sealed class ConcurrencyConflictException(Exception inner)
    : Exception("The row was changed by another request since it was read.", inner);
