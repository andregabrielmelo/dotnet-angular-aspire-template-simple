using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Infrastructure.Data;

public static class ApplicationDatabaseContextExtensions
{
    public static void AddApplicationDbContext(
        this IServiceCollection services,
        string connectionString
    ) =>
        services.AddDbContext<ApplicationDatabaseContext>(options =>
            options.UseNpgsql(connectionString)
        );
}
