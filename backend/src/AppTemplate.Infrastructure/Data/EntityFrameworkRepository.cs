using Ardalis.Specification.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data;

/// <summary>The Ardalis.Specification repository over <see cref="ApplicationDatabaseContext"/>.</summary>
public class EntityFrameworkRepository<T>(ApplicationDatabaseContext dbContext)
    : RepositoryBase<T>(dbContext),
        IRepository<T>
    where T : class, IAggregateRoot;
