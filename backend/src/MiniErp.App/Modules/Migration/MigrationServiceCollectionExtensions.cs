#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Migration;

/// <summary>Registers the bounded Migration application seams.</summary>
public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationFoundation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<MigrationFoundationService>();
        services.AddSingleton<MigrationIntakeService>();
        return services;
    }
}

#pragma warning restore CS1591
