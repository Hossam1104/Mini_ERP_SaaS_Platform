#pragma warning disable CS1591

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.App.Modules.Identity;

public static class IdentityModuleRegistration
{
    public static IServiceCollection AddIdentityAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPasswordHasher<GlobalUser>, PasswordHasher<GlobalUser>>();
        services.AddSingleton<IdentityStore>();
        services.AddSingleton<IDurableWorkOperationCatalogue>(DurableWorkOperationCatalogue.Empty);
        services.AddSingleton<IdentityAuthorizationService>(serviceProvider =>
            new IdentityAuthorizationService(
                serviceProvider.GetRequiredService<IdentityStore>(),
                timeProvider: serviceProvider.GetRequiredService<TimeProvider>(),
                passwordHasher: serviceProvider.GetRequiredService<IPasswordHasher<GlobalUser>>(),
                assuranceEvidenceSource: serviceProvider.GetRequiredService<IAuthenticationAssuranceEvidenceSource>(),
                operationCatalogue: serviceProvider.GetRequiredService<IDurableWorkOperationCatalogue>()));
        services.AddSingleton<IOrganizationScopeOwnershipResolver>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityAuthorizationService>());
        services.AddSingleton<ICurrentOrganizationScopeResolver>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityAuthorizationService>());
        services.AddSingleton<IDurableWorkAuthorityRevalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityAuthorizationService>());
        services.AddSingleton<INotificationRecipientAuthorizer>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityAuthorizationService>());
        services.AddSingleton<ISupportAccessContextValidator>(serviceProvider =>
            serviceProvider.GetRequiredService<IdentityAuthorizationService>());
        services.AddSingleton<SupportAccessSessionStore>();
        services.AddSingleton<IAuthenticationAssuranceEvidenceSource, UnavailableAuthenticationAssuranceEvidenceSource>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITenantDisplayNameProvider, ConfiguredTenantDisplayNameProvider>();
        services.TryAddSingleton<IFoundationOperationalContextProvider, NoFoundationOperationalContextProvider>();
        services.AddSingleton<IFoundationTenantBrandingProvider, ConfiguredFoundationTenantBrandingProvider>();
        services.AddSingleton<IFoundationIdentityHost>(serviceProvider =>
            new FoundationIdentityHost(
                serviceProvider.GetRequiredService<IdentityAuthorizationService>(),
                serviceProvider.GetRequiredService<ITenantDisplayNameProvider>(),
                serviceProvider.GetRequiredService<IFoundationOperationalContextProvider>()));
        services.AddSingleton<TenantHostRegistry>();
        services.AddSingleton<ITenantEntryAuthority, TenantEntryAuthority>();
        return services;
    }
}

#pragma warning restore CS1591
