#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.BuildingBlocks.Owners;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.App.Modules.Migration;

/// <summary>Registers the bounded Migration application seams.</summary>
public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddMigrationFoundation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<MigrationFoundationService>();
        services.AddSingleton<MigrationIntakeService>();
        services.AddSingleton<MigrationValidationService>();
        services.AddSingleton<MigrationInventoryOpeningExecutionCoordinator>();
        services.AddSingleton<MigrationArOpeningExecutionCoordinator>();
        services.AddSingleton<MigrationApOpeningExecutionCoordinator>();
        services.AddSingleton<MigrationCashBankOpeningExecutionCoordinator>();
        services.AddSingleton<MigrationExecutionService>(provider => new MigrationExecutionService(
            provider.GetRequiredService<MigrationFoundationService>(),
            provider.GetRequiredService<IMigrationFoundationPersistence>(),
            provider.GetRequiredService<IMigrationValidationPersistence>(),
            provider.GetRequiredService<IMigrationExecutionPersistence>(),
            provider.GetRequiredService<ICurrentOrganizationScopeResolver>(),
            provider.GetRequiredService<IOrganizationScopeOwnershipResolver>(),
            provider.GetRequiredService<IOwnerExecutionGateway>(),
            provider.GetRequiredService<IMigrationReferenceAuthority>(),
            provider.GetRequiredService<MigrationInventoryOpeningExecutionCoordinator>(),
            provider.GetRequiredService<MigrationArOpeningExecutionCoordinator>(),
            provider.GetRequiredService<MigrationApOpeningExecutionCoordinator>(),
            provider.GetService<TimeProvider>(),
            provider.GetRequiredService<MigrationCashBankOpeningExecutionCoordinator>()));
        services.AddSingleton<IMigrationReferenceAuthority, UnavailableMigrationReferenceAuthority>();
        return services;
    }
}

#pragma warning restore CS1591
