using Ardalis.Specification.EntityFrameworkCore;
using Npgsql;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// The Ardalis.Specification repository over <see cref="ApplicationDatabaseContext"/>. Save
/// failures that use cases react to are translated into SharedKernel exceptions, so UseCases
/// never depends on EF Core or Npgsql.
/// </summary>
public class EntityFrameworkRepository<T>(ApplicationDatabaseContext dbContext)
    : RepositoryBase<T>(dbContext),
        IRepository<T>
    where T : class, IAggregateRoot
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: { } constraintName,
                    }
            )
        {
            throw new UniqueConstraintViolationException(constraintName, exception);
        }
    }
}
