#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

internal static class MigrationTenantOwnershipVerifier
{
    internal static TenantOwnershipVerifierRegistration For<TEntity>()
        where TEntity : class, ITenantOwned => new(
            typeof(TEntity),
            static (context, entry) => Read<TEntity>(context, entry),
            static (context, entry, cancellationToken) => ReadAsync<TEntity>(context, entry, cancellationToken));

    private static TenantId? Read<TEntity>(TenantPersistenceDbContext context, EntityEntry entry)
        where TEntity : class, ITenantOwned
    {
        if (context is not MigrationDbContext migrationContext)
        {
            return null;
        }

        return entry.Entity switch
        {
            MigrationRunEntity run => migrationContext.Runs
                .Where(item => item.RunId == run.RunId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationAttemptEntity attempt => migrationContext.Attempts
                .Where(item => item.AttemptId == attempt.AttemptId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationIdempotencyEntity idempotency => migrationContext.Idempotency
                .Where(item => item.TenantId == idempotency.TenantId
                    && item.Operation == idempotency.Operation
                    && item.IdempotencyKey == idempotency.IdempotencyKey)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            _ => null
        };
    }

    private static async Task<TenantId?> ReadAsync<TEntity>(
        TenantPersistenceDbContext context,
        EntityEntry entry,
        CancellationToken cancellationToken)
        where TEntity : class, ITenantOwned
    {
        if (context is not MigrationDbContext migrationContext)
        {
            return null;
        }

        return entry.Entity switch
        {
            MigrationRunEntity run => await migrationContext.Runs
                .Where(item => item.RunId == run.RunId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationAttemptEntity attempt => await migrationContext.Attempts
                .Where(item => item.AttemptId == attempt.AttemptId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationIdempotencyEntity idempotency => await migrationContext.Idempotency
                .Where(item => item.TenantId == idempotency.TenantId
                    && item.Operation == idempotency.Operation
                    && item.IdempotencyKey == idempotency.IdempotencyKey)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }
}

#pragma warning restore CS1591
