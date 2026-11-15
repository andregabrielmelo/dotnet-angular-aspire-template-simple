using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace AppTemplate.AspireTests;

/// <summary>
/// The AppHost keeps Postgres and Redis in persistent containers with data volumes, for local
/// development. A test run must neither reuse nor change a developer's data, so here every
/// container lives only as long as the test run and keeps no volume.
/// </summary>
public static class ThrowawayContainers
{
    public static void UseThrowawayContainers(this IDistributedApplicationTestingBuilder builder)
    {
        foreach (var container in builder.Resources.OfType<ContainerResource>().ToList())
        {
            builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session);

            foreach (
                var volume in container
                    .Annotations.OfType<ContainerMountAnnotation>()
                    .Where(mount => mount.Type == ContainerMountType.Volume)
                    .ToList()
            )
            {
                container.Annotations.Remove(volume);
            }
        }
    }
}
