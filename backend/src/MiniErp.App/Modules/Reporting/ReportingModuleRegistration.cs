#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.App.Modules.Reporting;

public static class ReportingModuleRegistration
{
    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        services.AddSingleton<ReportingAuthorizationService>();
        services.AddSingleton<ReportingRuntimeStore>();
        services.AddSingleton<IPrivateObjectStorage, InMemoryPrivateObjectStorage>();
        services.AddSingleton<IReportingService, ReportingService>();
        return services;
    }
}

#pragma warning restore CS1591
