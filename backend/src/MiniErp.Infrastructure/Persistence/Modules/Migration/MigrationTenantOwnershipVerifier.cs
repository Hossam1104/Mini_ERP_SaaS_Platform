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
            MigrationIntakeEntity intake => migrationContext.Intakes
                .Where(item => item.RunId == intake.RunId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationStagedRecordEntity staged => migrationContext.StagedRecords
                .Where(item => item.StagedRecordId == staged.StagedRecordId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationValidationResultEntity result => migrationContext.ValidationResults
                .Where(item => item.ValidationResultId == result.ValidationResultId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationValidationRecordEntity record => migrationContext.ValidationRecords
                .Where(item => item.TenantId == record.TenantId && item.RunId == record.RunId && item.AttemptId == record.AttemptId && item.StagedRecordId == record.StagedRecordId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationValidationFindingEntity finding => migrationContext.ValidationFindings
                .Where(item => item.FindingId == finding.FindingId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationDryRunPreviewEntity preview => migrationContext.DryRunPreviews
                .Where(item => item.PreviewId == preview.PreviewId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationDryRunPreviewRowEntity previewRow => migrationContext.DryRunPreviewRows
                .Where(item => item.TenantId == previewRow.TenantId && item.PreviewId == previewRow.PreviewId && item.StagedRecordId == previewRow.StagedRecordId)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationExecutionBatchEntity batch => migrationContext.ExecutionBatches
                .Where(item => item.Id == batch.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationExecutionEffectEntity effect => migrationContext.ExecutionEffects
                .Where(item => item.Id == effect.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationEconomicRepresentationEntity representation => migrationContext.EconomicRepresentations
                .Where(item => item.Id == representation.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationReconciliationEntity reconciliation => migrationContext.Reconciliations
                .Where(item => item.Id == reconciliation.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationReconciliationDetailEntity detail => migrationContext.ReconciliationDetails
                .Where(item => item.Id == detail.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationReconciliationRequirementEntity requirement => migrationContext.ReconciliationRequirements
                .Where(item => item.Id == requirement.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationReconciliationApprovalEntity approval => migrationContext.ReconciliationApprovals
                .Where(item => item.Id == approval.Id)
                .Select(item => item.TenantId)
                .SingleOrDefault(),
            MigrationHandoverReadinessEntity readiness => migrationContext.HandoverReadiness
                .Where(item => item.Id == readiness.Id)
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
            MigrationIntakeEntity intake => await migrationContext.Intakes
                .Where(item => item.RunId == intake.RunId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationStagedRecordEntity staged => await migrationContext.StagedRecords
                .Where(item => item.StagedRecordId == staged.StagedRecordId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationValidationResultEntity result => await migrationContext.ValidationResults
                .Where(item => item.ValidationResultId == result.ValidationResultId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationValidationRecordEntity record => await migrationContext.ValidationRecords
                .Where(item => item.TenantId == record.TenantId && item.RunId == record.RunId && item.AttemptId == record.AttemptId && item.StagedRecordId == record.StagedRecordId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationValidationFindingEntity finding => await migrationContext.ValidationFindings
                .Where(item => item.FindingId == finding.FindingId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationDryRunPreviewEntity preview => await migrationContext.DryRunPreviews
                .Where(item => item.PreviewId == preview.PreviewId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationDryRunPreviewRowEntity previewRow => await migrationContext.DryRunPreviewRows
                .Where(item => item.TenantId == previewRow.TenantId && item.PreviewId == previewRow.PreviewId && item.StagedRecordId == previewRow.StagedRecordId)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationExecutionBatchEntity batch => await migrationContext.ExecutionBatches
                .Where(item => item.Id == batch.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationExecutionEffectEntity effect => await migrationContext.ExecutionEffects
                .Where(item => item.Id == effect.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationEconomicRepresentationEntity representation => await migrationContext.EconomicRepresentations
                .Where(item => item.Id == representation.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationReconciliationEntity reconciliation => await migrationContext.Reconciliations
                .Where(item => item.Id == reconciliation.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationReconciliationDetailEntity detail => await migrationContext.ReconciliationDetails
                .Where(item => item.Id == detail.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationReconciliationRequirementEntity requirement => await migrationContext.ReconciliationRequirements
                .Where(item => item.Id == requirement.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationReconciliationApprovalEntity approval => await migrationContext.ReconciliationApprovals
                .Where(item => item.Id == approval.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            MigrationHandoverReadinessEntity readiness => await migrationContext.HandoverReadiness
                .Where(item => item.Id == readiness.Id)
                .Select(item => (TenantId?)item.TenantId)
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }
}

#pragma warning restore CS1591
