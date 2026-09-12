#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Migration;

/// <summary>Registers the Slice 1 Migration application seam.</summary>
public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationFoundation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<MigrationFoundationService>();
        return services;
    }
}

#pragma warning restore CS1591
