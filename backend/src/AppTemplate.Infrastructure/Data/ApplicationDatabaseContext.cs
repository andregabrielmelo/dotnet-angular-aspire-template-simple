using System.Reflection;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data;

/// <summary>
/// The application's data and ASP.NET Core Identity's (accounts, roles, their claims, logins and
/// tokens), in one database and one transaction. It also keeps the Data Protection key ring, which
/// protects the confirmation and reset codes, so links survive restarts and work on every instance. <c>Users</c> is Identity's accounts
/// (<see cref="ApplicationUser"/>); the domain's profiles are <see cref="UserProfiles"/>.
/// </summary>
public class ApplicationDatabaseContext(DbContextOptions<ApplicationDatabaseContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options),
        IDataProtectionKeyContext
{
    public DbSet<User> UserProfiles => Set<User>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Override OnModelCreating to apply class configurations from the assembly
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity names its tables AspNet*; the snake_case convention only renames what it named
        // itself, so name them the same way as every other table.
        modelBuilder.Entity<ApplicationUser>().ToTable("asp_net_users");
        modelBuilder.Entity<IdentityRole<int>>().ToTable("asp_net_roles");
        modelBuilder.Entity<IdentityUserRole<int>>().ToTable("asp_net_user_roles");
        modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<int>>().ToTable("asp_net_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("asp_net_role_claims");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    // Override SaveChanges to ensure that all changes are saved asynchronously
    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();
}
