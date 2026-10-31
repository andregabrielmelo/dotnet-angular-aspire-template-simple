using System.Reflection;
using AppTemplate.Core.Aggregates.RefreshTokenAggregate;
using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.Infrastructure.Data;

public class ApplicationDatabaseContext(DbContextOptions<ApplicationDatabaseContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Override OnModelCreating to apply class configurations from the assembly
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    // Override SaveChanges to ensure that all changes are saved asynchronously
    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();
}
