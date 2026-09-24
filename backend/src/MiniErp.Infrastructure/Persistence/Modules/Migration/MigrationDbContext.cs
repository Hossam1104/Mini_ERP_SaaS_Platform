#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence.Modules.Migration;

/// <summary>
/// Migration-owned foundation context. It contains only run, attempt and
/// safe-idempotency metadata; source rows and domain opening data belong to
/// later bounded slices and other owning modules.
/// </summary>
internal sealed class MigrationDbContext : TenantPersistenceDbContext
{
    internal MigrationDbContext(DbContextOptions options, TenantContext tenantContext)
        : base(options, tenantContext, TenantOwnershipVerifierRegistry.CreateMigration())
    {
    }

    internal DbSet<MigrationRunEntity> Runs => Set<MigrationRunEntity>();

    internal DbSet<MigrationAttemptEntity> Attempts => Set<MigrationAttemptEntity>();

    internal DbSet<MigrationIdempotencyEntity> Idempotency => Set<MigrationIdempotencyEntity>();

    internal DbSet<MigrationIntakeEntity> Intakes => Set<MigrationIntakeEntity>();

    internal DbSet<MigrationStagedRecordEntity> StagedRecords => Set<MigrationStagedRecordEntity>();

    internal DbSet<MigrationValidationResultEntity> ValidationResults => Set<MigrationValidationResultEntity>();

    internal DbSet<MigrationValidationRecordEntity> ValidationRecords => Set<MigrationValidationRecordEntity>();

    internal DbSet<MigrationValidationFindingEntity> ValidationFindings => Set<MigrationValidationFindingEntity>();

    internal DbSet<MigrationDryRunPreviewEntity> DryRunPreviews => Set<MigrationDryRunPreviewEntity>();

    internal DbSet<MigrationDryRunPreviewRowEntity> DryRunPreviewRows => Set<MigrationDryRunPreviewRowEntity>();

    internal DbSet<MigrationExecutionBatchEntity> ExecutionBatches => Set<MigrationExecutionBatchEntity>();

    internal DbSet<MigrationExecutionEffectEntity> ExecutionEffects => Set<MigrationExecutionEffectEntity>();
    internal DbSet<MigrationEconomicRepresentationEntity> EconomicRepresentations => Set<MigrationEconomicRepresentationEntity>();
    internal DbSet<MigrationReconciliationEntity> Reconciliations => Set<MigrationReconciliationEntity>();
    internal DbSet<MigrationReconciliationDetailEntity> ReconciliationDetails => Set<MigrationReconciliationDetailEntity>();
    internal DbSet<MigrationReconciliationRequirementEntity> ReconciliationRequirements => Set<MigrationReconciliationRequirementEntity>();
    internal DbSet<MigrationReconciliationApprovalEntity> ReconciliationApprovals => Set<MigrationReconciliationApprovalEntity>();
    internal DbSet<MigrationHandoverReadinessEntity> HandoverReadiness => Set<MigrationHandoverReadinessEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TenantOwnedRecords is the shared Tenancy-owned table. It is part of
        // the base persistence model for runtime guards, but its schema is
        // created and upgraded only by the dedicated Tenancy migration.
        modelBuilder.Ignore<TenantOwnedRecord>();

        var run = modelBuilder.Entity<MigrationRunEntity>();
        run.ToTable("MigrationRuns", "migration");
        run.HasKey(item => item.RunId);
        run.Property(item => item.RunId).ValueGeneratedNever();
        ConfigureTenant(run.Property(item => item.TenantId));
        run.Property(item => item.ActorId).IsRequired();
        run.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        run.Property(item => item.DefinitionId).HasMaxLength(128).IsRequired();
        run.Property(item => item.DefinitionVersion).HasMaxLength(128).IsRequired();
        run.Property(item => item.SourceProfileId).HasMaxLength(128).IsRequired();
        run.Property(item => item.SourceProfileVersion).HasMaxLength(128).IsRequired();
        run.Property(item => item.Status).IsRequired();
        run.Property(item => item.CreatedAt).IsRequired();
        run.Property(item => item.UpdatedAt).IsRequired();
        run.Property(item => item.EvidenceConfirmed).IsRequired();
        ConfigureVersion(run.Property(item => item.Version));
        run.HasIndex(item => new { item.TenantId, item.RunId }).IsUnique();
        run.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var attempt = modelBuilder.Entity<MigrationAttemptEntity>();
        attempt.ToTable("MigrationAttempts", "migration");
        attempt.HasKey(item => item.AttemptId);
        attempt.Property(item => item.AttemptId).ValueGeneratedNever();
        ConfigureTenant(attempt.Property(item => item.TenantId));
        attempt.Property(item => item.RunId).IsRequired();
        attempt.Property(item => item.Sequence).IsRequired();
        attempt.Property(item => item.PreviousAttemptId).IsRequired(false);
        attempt.Property(item => item.Operation).IsRequired();
        attempt.Property(item => item.Outcome).IsRequired();
        attempt.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        attempt.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired();
        attempt.Property(item => item.StartedAt).IsRequired();
        attempt.Property(item => item.FinishedAt).IsRequired(false);
        attempt.Property(item => item.SafeOutcomeCode).HasMaxLength(128).IsRequired(false);
        attempt.Property(item => item.EvidenceConfirmed).IsRequired();
        ConfigureVersion(attempt.Property(item => item.Version));
        attempt.HasAlternateKey(item => new { item.TenantId, item.RunId, item.AttemptId });
        attempt.HasIndex(item => new { item.TenantId, item.RunId, item.Sequence }).IsUnique();

        // Run-qualified self reference for the retry lineage. PreviousAttemptId
        // stays optional, so the first attempt in a run references nothing.
        attempt.HasOne<MigrationAttemptEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.PreviousAttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        attempt.HasOne<MigrationRunEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        attempt.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var idempotency = modelBuilder.Entity<MigrationIdempotencyEntity>();
        idempotency.ToTable("MigrationIdempotency", "migration");
        idempotency.HasKey(item => new { item.TenantId, item.Operation, item.IdempotencyKey });
        ConfigureTenant(idempotency.Property(item => item.TenantId));
        idempotency.Property(item => item.RunId).IsRequired();
        idempotency.Property(item => item.Operation).IsRequired();
        idempotency.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        idempotency.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired();
        idempotency.Property(item => item.AttemptId).IsRequired(false);
        idempotency.Property(item => item.ResultKind).IsRequired();
        idempotency.Property(item => item.ResultCode).HasMaxLength(128).IsRequired();
        idempotency.Property(item => item.CreatedAt).IsRequired();
        idempotency.Property(item => item.EvidenceConfirmed).IsRequired();
        ConfigureVersion(idempotency.Property(item => item.Version));
        idempotency.HasIndex(item => new { item.TenantId, item.RunId, item.Operation, item.IdempotencyKey });
        idempotency.HasOne<MigrationRunEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        idempotency.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var intake = modelBuilder.Entity<MigrationIntakeEntity>();
        intake.ToTable("MigrationIntakes", "migration", table => table.HasCheckConstraint(
            "CK_MigrationIntakes_SourceTenant_Matches_Tenant",
            "[SourceTenantId] = [TenantId]"));
        intake.HasKey(item => item.RunId);
        intake.Property(item => item.RunId).ValueGeneratedNever();
        ConfigureTenant(intake.Property(item => item.TenantId));
        intake.Property(item => item.Operation).IsRequired();
        intake.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        intake.Property(item => item.FingerprintVersion).HasMaxLength(64).IsRequired();
        intake.Property(item => item.RequestFingerprint).HasMaxLength(128).IsRequired();
        intake.Property(item => item.SourceObjectId).IsRequired();
        ConfigureTenant(intake.Property(item => item.SourceTenantId));
        intake.Property(item => item.SourceCompanyId).IsRequired(false);
        intake.Property(item => item.SourceBranchId).IsRequired(false);
        intake.Property(item => item.SourceWarehouseId).IsRequired(false);
        intake.Property(item => item.SourceSha256).HasMaxLength(64).IsRequired();
        intake.Property(item => item.SourceLength).IsRequired();
        intake.Property(item => item.SourceConcurrencyVersion).IsRequired();
        intake.Property(item => item.CapturedAt).IsRequired();
        ConfigureVersion(intake.Property(item => item.Version));
        intake.HasIndex(item => new { item.TenantId, item.SourceObjectId });
        intake.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        intake.HasOne<MigrationRunEntity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        intake.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var staged = modelBuilder.Entity<MigrationStagedRecordEntity>();
        staged.ToTable("MigrationStagedRecords", "migration");
        staged.HasKey(item => item.StagedRecordId);
        staged.Property(item => item.StagedRecordId).ValueGeneratedNever();
        ConfigureTenant(staged.Property(item => item.TenantId));
        staged.Property(item => item.RunId).IsRequired();
        staged.Property(item => item.SourceSequence).IsRequired();
        staged.Property(item => item.SourceRecordId).HasMaxLength(256).IsRequired(false);
        staged.Property(item => item.RecordType).IsRequired();
        staged.Property(item => item.CanonicalPayload).HasMaxLength(2_000_000).IsRequired();
        staged.Property(item => item.PayloadHash).HasMaxLength(64).IsRequired();
        staged.Property(item => item.PackageHash).HasMaxLength(64).IsRequired();
        staged.Property(item => item.PackageVersion).HasMaxLength(64).IsRequired();
        staged.Property(item => item.SourceObjectId).IsRequired();
        staged.Property(item => item.SourceSnapshotHash).HasMaxLength(64).IsRequired();
        staged.Property(item => item.CapturedAt).IsRequired();
        staged.HasAlternateKey(item => new { item.TenantId, item.RunId, item.StagedRecordId });
        staged.HasIndex(item => new { item.TenantId, item.RunId, item.SourceSequence }).IsUnique();
        staged.HasIndex(item => new { item.TenantId, item.RunId, item.PackageHash });
        staged.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        staged.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var validation = modelBuilder.Entity<MigrationValidationResultEntity>();
        validation.ToTable("MigrationValidationResults", "migration");
        validation.HasKey(item => item.ValidationResultId);
        validation.Property(item => item.ValidationResultId).ValueGeneratedNever();
        ConfigureTenant(validation.Property(item => item.TenantId));
        validation.Property(item => item.RunId).IsRequired();
        validation.Property(item => item.AttemptId).IsRequired();
        validation.Property(item => item.PackageHash).HasMaxLength(64).IsRequired();
        validation.Property(item => item.SourceSnapshotHash).HasMaxLength(64).IsRequired();
        validation.Property(item => item.TotalStagedRecords).IsRequired();
        validation.Property(item => item.AcceptedCount).IsRequired();
        validation.Property(item => item.RejectedCount).IsRequired();
        validation.Property(item => item.QuarantinedCount).IsRequired();
        validation.Property(item => item.FindingCountsJson).HasMaxLength(32_000).IsRequired();
        validation.Property(item => item.CompletedAt).IsRequired();
        validation.HasAlternateKey(item => new { item.TenantId, item.RunId, item.AttemptId });
        validation.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        validation.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        validation.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var validationRecord = modelBuilder.Entity<MigrationValidationRecordEntity>();
        validationRecord.ToTable("MigrationValidationRecords", "migration");
        validationRecord.HasKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.StagedRecordId });
        ConfigureTenant(validationRecord.Property(item => item.TenantId));
        validationRecord.Property(item => item.SourceSequence).IsRequired();
        validationRecord.Property(item => item.RecordType).IsRequired();
        validationRecord.Property(item => item.Disposition).IsRequired();
        validationRecord.Property(item => item.FindingCodesJson).HasMaxLength(32_000).IsRequired();
        validationRecord.HasOne<MigrationValidationResultEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Cascade);
        validationRecord.HasOne<MigrationStagedRecordEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .OnDelete(DeleteBehavior.Restrict);
        validationRecord.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var finding = modelBuilder.Entity<MigrationValidationFindingEntity>();
        finding.ToTable("MigrationValidationFindings", "migration");
        finding.HasKey(item => item.FindingId);
        finding.Property(item => item.FindingId).ValueGeneratedNever();
        ConfigureTenant(finding.Property(item => item.TenantId));
        finding.Property(item => item.RunId).IsRequired();
        finding.Property(item => item.AttemptId).IsRequired();
        finding.Property(item => item.StagedRecordId).IsRequired(false);
        finding.Property(item => item.Category).IsRequired();
        finding.Property(item => item.Severity).IsRequired();
        finding.Property(item => item.IsBlocking).IsRequired();
        finding.Property(item => item.Code).HasMaxLength(128).IsRequired();
        finding.Property(item => item.Message).HasMaxLength(512).IsRequired();
        finding.Property(item => item.ReferenceId).HasMaxLength(128).IsRequired(false);
        finding.Property(item => item.CreatedAt).IsRequired();
        finding.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        finding.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        finding.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var preview = modelBuilder.Entity<MigrationDryRunPreviewEntity>();
        preview.ToTable("MigrationDryRunPreviews", "migration");
        preview.HasKey(item => item.PreviewId);
        preview.Property(item => item.PreviewId).ValueGeneratedNever();
        ConfigureTenant(preview.Property(item => item.TenantId));
        preview.Property(item => item.RunId).IsRequired();
        preview.Property(item => item.AttemptId).IsRequired();
        preview.Property(item => item.ValidationAttemptId).IsRequired();
        preview.Property(item => item.PackageHash).HasMaxLength(64).IsRequired();
        preview.Property(item => item.SourceSnapshotHash).HasMaxLength(64).IsRequired();
        preview.Property(item => item.TotalStagedRecords).IsRequired();
        preview.Property(item => item.AcceptedCount).IsRequired();
        preview.Property(item => item.RejectedCount).IsRequired();
        preview.Property(item => item.QuarantinedCount).IsRequired();
        preview.Property(item => item.FindingCountsJson).HasMaxLength(32_000).IsRequired();
        preview.Property(item => item.ControlTotalsJson).HasMaxLength(32_000).IsRequired();
        preview.Property(item => item.UnresolvedDependencyCount).IsRequired();
        preview.Property(item => item.ExceptionCount).IsRequired();
        preview.Property(item => item.CompletedAt).IsRequired();
        preview.HasAlternateKey(item => new { item.TenantId, item.PreviewId });
        preview.HasIndex(item => new { item.TenantId, item.RunId, item.AttemptId }).IsUnique();
        preview.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        preview.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        preview.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.ValidationAttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        preview.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var previewRow = modelBuilder.Entity<MigrationDryRunPreviewRowEntity>();
        previewRow.ToTable("MigrationDryRunPreviewRows", "migration");
        previewRow.HasKey(item => new { item.TenantId, item.PreviewId, item.StagedRecordId });
        ConfigureTenant(previewRow.Property(item => item.TenantId));
        previewRow.Property(item => item.PreviewId).IsRequired();
        previewRow.Property(item => item.RunId).IsRequired();
        previewRow.Property(item => item.StagedRecordId).IsRequired();
        previewRow.Property(item => item.SourceSequence).IsRequired();
        previewRow.Property(item => item.RecordType).IsRequired();
        previewRow.Property(item => item.Disposition).IsRequired();
        previewRow.Property(item => item.PlannedAction).IsRequired();
        previewRow.Property(item => item.Projection).HasMaxLength(256).IsRequired(false);
        previewRow.HasOne<MigrationDryRunPreviewEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PreviewId })
            .HasPrincipalKey(item => new { item.TenantId, item.PreviewId })
            .OnDelete(DeleteBehavior.Cascade);
        previewRow.HasOne<MigrationStagedRecordEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .OnDelete(DeleteBehavior.Restrict);
        previewRow.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var executionBatch = modelBuilder.Entity<MigrationExecutionBatchEntity>();
        executionBatch.ToTable("MigrationExecutionBatches", "migration");
        executionBatch.HasKey(item => item.Id);
        executionBatch.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(executionBatch.Property(item => item.TenantId));
        executionBatch.Property(item => item.RunId).IsRequired();
        executionBatch.Property(item => item.AttemptId).IsRequired();
        executionBatch.Property(item => item.RecordType).IsRequired();
        executionBatch.Property(item => item.State).IsRequired();
        executionBatch.Property(item => item.OwnerBatchId).IsRequired();
        executionBatch.Property(item => item.Fingerprint).HasMaxLength(128).IsRequired();
        executionBatch.Property(item => item.CreatedAt).IsRequired();
        executionBatch.Property(item => item.StartedAt).IsRequired(false);
        executionBatch.Property(item => item.CompletedAt).IsRequired(false);
        executionBatch.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        ConfigureVersion(executionBatch.Property(item => item.Version));
        executionBatch.HasAlternateKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.RecordType });
        executionBatch.HasIndex(item => new { item.TenantId, item.RunId, item.RecordType })
            .IsUnique()
            .HasFilter("[State] IN (1, 2, 3)");
        executionBatch.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId })
            .OnDelete(DeleteBehavior.Restrict);
        executionBatch.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .OnDelete(DeleteBehavior.Restrict);
        executionBatch.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var executionEffect = modelBuilder.Entity<MigrationExecutionEffectEntity>();
        executionEffect.ToTable("MigrationExecutionEffects", "migration");
        executionEffect.HasKey(item => item.Id);
        executionEffect.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(executionEffect.Property(item => item.TenantId));
        executionEffect.Property(item => item.RunId).IsRequired();
        executionEffect.Property(item => item.AttemptId).IsRequired();
        executionEffect.Property(item => item.StagedRecordId).IsRequired();
        executionEffect.Property(item => item.SourceSequence).IsRequired();
        executionEffect.Property(item => item.RecordType).IsRequired();
        executionEffect.Property(item => item.OwnerBatchId).IsRequired();
        executionEffect.Property(item => item.OwnerRowId).IsRequired(false);
        executionEffect.Property(item => item.ResultingResourceId).IsRequired(false);
        executionEffect.Property(item => item.ResultingResourceCode).HasMaxLength(128).IsRequired(false);
        executionEffect.Property(item => item.Disposition).IsRequired();
        executionEffect.Property(item => item.SafeCode).HasMaxLength(128).IsRequired(false);
        executionEffect.Property(item => item.CreatedAt).IsRequired();
        executionEffect.Property(item => item.EffectStartedAt).IsRequired(false);
        executionEffect.Property(item => item.CompletedAt).IsRequired(false);
        executionEffect.Property(item => item.CorrelationId).HasMaxLength(128).IsRequired();
        ConfigureVersion(executionEffect.Property(item => item.Version));
        executionEffect.HasIndex(item => new { item.TenantId, item.RunId, item.AttemptId, item.StagedRecordId }).IsUnique();
        executionEffect.HasAlternateKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.Id });
        executionEffect.HasOne<MigrationExecutionBatchEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.RecordType })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.RecordType })
            .OnDelete(DeleteBehavior.Restrict);
        executionEffect.HasOne<MigrationStagedRecordEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.StagedRecordId })
            .OnDelete(DeleteBehavior.Restrict);
        executionEffect.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var representation = modelBuilder.Entity<MigrationEconomicRepresentationEntity>();
        representation.ToTable("MigrationEconomicRepresentations", "migration");
        representation.HasKey(item => item.Id);
        representation.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(representation.Property(item => item.TenantId));
        representation.Property(item => item.RunId).IsRequired();
        representation.Property(item => item.AttemptId).IsRequired();
        representation.Property(item => item.EffectId).IsRequired();
        representation.Property(item => item.OwnerModule).IsRequired();
        representation.Property(item => item.Kind).IsRequired();
        representation.Property(item => item.OwnerId).IsRequired();
        representation.Property(item => item.OwnerReference).HasMaxLength(128).IsRequired(false);
        representation.Property(item => item.Status).HasMaxLength(64).IsRequired();
        representation.Property(item => item.EvidenceVersion).HasMaxLength(128).IsRequired();
        representation.Property(item => item.OccurredAt).IsRequired();
        representation.Property(item => item.RecordedAt).IsRequired();
        representation.Property(item => item.EvidenceConfirmed).IsRequired();
        representation.Property(item => item.SourceContract).HasMaxLength(128).IsRequired(false);
        representation.Property(item => item.SourceEvent).HasMaxLength(128).IsRequired(false);
        representation.Property(item => item.FunctionalAmount).HasPrecision(19, 8).IsRequired(false);
        representation.Property(item => item.PostingRuleId).IsRequired(false);
        representation.Property(item => item.PostingRuleVersionNumber).IsRequired(false);
        representation.Property(item => item.ControlAccountId).IsRequired(false);
        representation.Property(item => item.OffsetAccountId).IsRequired(false);
        representation.Property(item => item.Reversal).IsRequired(false);
        representation.Property(item => item.SourceEvidenceId).IsRequired(false);
        representation.Property(item => item.SourceEvidenceVersion).IsRequired(false);
        representation.Property(item => item.OwnerSourceId).IsRequired(false);
        representation.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false);
        representation.Property(item => item.TransactionAmount).HasPrecision(28, 8).IsRequired(false);
        representation.Property(item => item.ExpectedFunctionalCurrencyCode).HasMaxLength(16).IsRequired(false);
        representation.Property(item => item.RateDate).IsRequired(false);
        representation.Property(item => item.ExchangeRateId).IsRequired(false);
        representation.Property(item => item.ExchangeRateVersionId).IsRequired(false);
        representation.Property(item => item.ExchangeRateVersionNumber).IsRequired(false);
        representation.Property(item => item.AppliedRate).HasPrecision(28, 12).IsRequired(false);
        representation.Property(item => item.MonetaryPolicyId).IsRequired(false);
        representation.Property(item => item.MonetaryPolicyVersionNumber).IsRequired(false);
        representation.Property(item => item.RoundingScale).IsRequired(false);
        representation.Property(item => item.RoundingMode).HasMaxLength(32).IsRequired(false);
        representation.Property(item => item.ReportingCurrencyCode).HasMaxLength(16).IsRequired(false);
        representation.Property(item => item.ReportingExchangeRateId).IsRequired(false);
        representation.Property(item => item.ReportingExchangeRateVersionId).IsRequired(false);
        representation.Property(item => item.ReportingExchangeRateVersionNumber).IsRequired(false);
        representation.Property(item => item.ReportingAppliedRate).HasPrecision(28, 12).IsRequired(false);
        ConfigureVersion(representation.Property(item => item.Version));
        representation.HasIndex(item => new { item.TenantId, item.RunId, item.AttemptId, item.EffectId, item.OwnerModule, item.Kind, item.OwnerId, item.EvidenceVersion }).IsUnique();
        representation.HasOne<MigrationExecutionEffectEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.EffectId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        representation.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var reconciliation = modelBuilder.Entity<MigrationReconciliationEntity>();
        reconciliation.ToTable("MigrationReconciliations", "migration");
        reconciliation.HasKey(item => item.Id);
        reconciliation.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(reconciliation.Property(item => item.TenantId));
        reconciliation.Property(item => item.EvidenceFingerprint).HasMaxLength(64).IsRequired();
        reconciliation.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        reconciliation.Property(item => item.Status).IsRequired();
        reconciliation.Property(item => item.CreatedAt).IsRequired();
        reconciliation.Property(item => item.CalculatedAt).IsRequired();
        reconciliation.Property(item => item.VersionNumber).IsRequired();
        reconciliation.Property(item => item.SubmittedCount).IsRequired();
        reconciliation.Property(item => item.AcceptedCount).IsRequired();
        reconciliation.Property(item => item.RejectedCount).IsRequired();
        reconciliation.Property(item => item.DuplicateCount).IsRequired();
        reconciliation.Property(item => item.SkippedCount).IsRequired();
        reconciliation.Property(item => item.QuarantinedCount).IsRequired();
        reconciliation.Property(item => item.UnresolvedCount).IsRequired();
        reconciliation.Property(item => item.RequiredApprovalCount).IsRequired();
        reconciliation.Property(item => item.ApprovalPolicyId).HasMaxLength(128).IsRequired(false);
        reconciliation.Property(item => item.ApprovalPolicyVersion).IsRequired(false);
        reconciliation.Property(item => item.ApprovalPolicyCode).HasMaxLength(128).IsRequired();
        reconciliation.Property(item => item.ApprovalPolicyEffectiveFrom).IsRequired(false);
        reconciliation.Property(item => item.ApprovalPolicyEffectiveTo).IsRequired(false);
        reconciliation.Property(item => item.ApprovalEnforcesSeparationOfDuties).IsRequired();
        foreach (var property in new[] { nameof(MigrationReconciliationEntity.SourceDebit), nameof(MigrationReconciliationEntity.SourceCredit), nameof(MigrationReconciliationEntity.TargetDebit), nameof(MigrationReconciliationEntity.TargetCredit), nameof(MigrationReconciliationEntity.Variance) })
            reconciliation.Property<decimal>(property).HasPrecision(28, 8);
        ConfigureVersion(reconciliation.Property(item => item.Version));
        reconciliation.HasAlternateKey(item => new { item.TenantId, item.RunId, item.Id });
        reconciliation.HasIndex(item => new { item.TenantId, item.RunId, item.VersionNumber }).IsUnique();
        reconciliation.HasIndex(item => new { item.TenantId, item.RunId, item.EvidenceFingerprint }).IsUnique();
        reconciliation.HasIndex(item => new { item.TenantId, item.RunId, item.IdempotencyKey }).IsUnique();
        reconciliation.HasOne<MigrationRunEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId }).OnDelete(DeleteBehavior.Restrict);
        reconciliation.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId }).OnDelete(DeleteBehavior.Restrict);
        reconciliation.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var detail = modelBuilder.Entity<MigrationReconciliationDetailEntity>();
        detail.ToTable("MigrationReconciliationDetails", "migration");
        detail.HasKey(item => item.Id);
        detail.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(detail.Property(item => item.TenantId));
        detail.Property(item => item.Domain).IsRequired();
        detail.Property(item => item.ScopeKey).HasMaxLength(256).IsRequired();
        detail.Property(item => item.CompanyId).IsRequired(false);
        detail.Property(item => item.OpeningDate).IsRequired(false);
        detail.Property(item => item.CurrencyCode).HasMaxLength(16).IsRequired(false);
        detail.Property(item => item.TransactionCurrencyCode).HasMaxLength(16).IsRequired(false);
        detail.Property(item => item.FunctionalCurrencyCode).HasMaxLength(16).IsRequired(false);
        detail.Property(item => item.SourceContract).HasMaxLength(128).IsRequired(false);
        detail.Property(item => item.SourceEvent).HasMaxLength(128).IsRequired(false);
        detail.Property(item => item.RoundingMode).HasMaxLength(32).IsRequired(false);
        detail.Property(item => item.FindingCode).HasMaxLength(128).IsRequired(false);
        detail.Property(item => item.Explanation).HasMaxLength(512).IsRequired(false);
        detail.Property(item => item.SourceCount).IsRequired();
        detail.Property(item => item.ExchangeRateId).IsRequired(false);
        detail.Property(item => item.ExchangeRateVersionId).IsRequired(false);
        detail.Property(item => item.ExchangeRateVersionNumber).IsRequired(false);
        detail.Property(item => item.ControlAccountId).IsRequired(false);
        detail.Property(item => item.PostingRuleId).IsRequired(false);
        detail.Property(item => item.PostingRuleVersionNumber).IsRequired(false);
        detail.Property(item => item.OwnerSourceId).IsRequired(false);
        detail.Property(item => item.WarehouseId).IsRequired(false);
        detail.Property(item => item.ProductId).IsRequired(false);
        detail.Property(item => item.UnitOfMeasureId).IsRequired(false);
        detail.Property(item => item.RoundingPolicyId).IsRequired(false);
        detail.Property(item => item.RoundingPolicyVersionNumber).IsRequired(false);
        detail.Property(item => item.RoundingScale).IsRequired(false);
        detail.Property(item => item.IsBlocking).IsRequired();
        detail.Property(item => item.EffectId).IsRequired(false);
        detail.Property(item => item.OwnerReferenceId).IsRequired(false);
        detail.Property(item => item.LinkedAccountId).IsRequired(false);
        foreach (var property in new[] { nameof(MigrationReconciliationDetailEntity.SourceDebit), nameof(MigrationReconciliationDetailEntity.SourceCredit), nameof(MigrationReconciliationDetailEntity.TargetDebit), nameof(MigrationReconciliationDetailEntity.TargetCredit), nameof(MigrationReconciliationDetailEntity.Variance), nameof(MigrationReconciliationDetailEntity.SourceAmount), nameof(MigrationReconciliationDetailEntity.TargetAmount), nameof(MigrationReconciliationDetailEntity.AmountVariance), nameof(MigrationReconciliationDetailEntity.OwnerRoundingDifference), nameof(MigrationReconciliationDetailEntity.TransactionAmount), nameof(MigrationReconciliationDetailEntity.FunctionalAmount), nameof(MigrationReconciliationDetailEntity.SubsidiaryEstablishedAmount), nameof(MigrationReconciliationDetailEntity.GlControlAmount), nameof(MigrationReconciliationDetailEntity.AppliedRate), nameof(MigrationReconciliationDetailEntity.SourceQuantity), nameof(MigrationReconciliationDetailEntity.TargetQuantity), nameof(MigrationReconciliationDetailEntity.QuantityVariance) })
            detail.Property<decimal?>(property).HasPrecision(28, 8);
        detail.HasIndex(item => new { item.TenantId, item.ReconciliationId, item.Domain, item.ScopeKey }).IsUnique();
        detail.HasOne<MigrationReconciliationEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.ReconciliationId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        detail.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var requirement = modelBuilder.Entity<MigrationReconciliationRequirementEntity>();
        requirement.ToTable("MigrationReconciliationRequirements", "migration");
        requirement.HasKey(item => item.Id);
        requirement.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(requirement.Property(item => item.TenantId));
        requirement.Property(item => item.Domain).HasMaxLength(64).IsRequired();
        requirement.Property(item => item.RequirementKey).HasMaxLength(128).IsRequired();
        requirement.Property(item => item.PolicyId).HasMaxLength(128).IsRequired();
        requirement.Property(item => item.PolicyVersion).IsRequired();
        requirement.Property(item => item.RequiredCount).IsRequired();
        requirement.Property(item => item.EnforceSeparationOfDuties).IsRequired();
        requirement.Property(item => item.EligibleActorIdsJson).HasMaxLength(8000).IsRequired();
        requirement.HasIndex(item => new { item.TenantId, item.ReconciliationId, item.Domain, item.RequirementKey }).IsUnique();
        requirement.HasOne<MigrationReconciliationEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.ReconciliationId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        requirement.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var approval = modelBuilder.Entity<MigrationReconciliationApprovalEntity>();
        approval.ToTable("MigrationReconciliationApprovals", "migration");
        approval.HasKey(item => item.Id);
        approval.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(approval.Property(item => item.TenantId));
        approval.Property(item => item.EvidenceFingerprint).HasMaxLength(64).IsRequired();
        approval.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        approval.Property(item => item.ReconciliationVersion).IsRequired();
        approval.Property(item => item.PolicyVersion).IsRequired();
        approval.Property(item => item.ActorId).IsRequired();
        approval.Property(item => item.Domain).HasMaxLength(64).IsRequired();
        approval.Property(item => item.RequirementKey).HasMaxLength(128).IsRequired();
        approval.Property(item => item.PolicyId).HasMaxLength(128).IsRequired();
        approval.Property(item => item.Decision).IsRequired();
        approval.Property(item => item.Reason).HasMaxLength(512).IsRequired(false);
        approval.Property(item => item.DecidedAt).IsRequired();
        approval.Property(item => item.EvidenceConfirmed).IsRequired();
        ConfigureVersion(approval.Property(item => item.Version));
        approval.HasIndex(item => new { item.TenantId, item.ReconciliationId, item.ReconciliationVersion, item.Domain, item.RequirementKey, item.ActorId }).IsUnique();
        approval.HasIndex(item => new { item.TenantId, item.RunId, item.IdempotencyKey }).IsUnique();
        approval.HasOne<MigrationReconciliationEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.ReconciliationId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        approval.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId }).OnDelete(DeleteBehavior.Restrict);
        approval.HasQueryFilter(item => item.TenantId == TrustedTenantId);

        var readiness = modelBuilder.Entity<MigrationHandoverReadinessEntity>();
        readiness.ToTable("MigrationHandoverReadiness", "migration");
        readiness.HasKey(item => item.Id);
        readiness.Property(item => item.Id).ValueGeneratedNever();
        ConfigureTenant(readiness.Property(item => item.TenantId));
        readiness.Property(item => item.EvidenceFingerprint).HasMaxLength(64).IsRequired();
        readiness.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
        readiness.Property(item => item.ResultCode).HasMaxLength(128).IsRequired();
        readiness.Property(item => item.CreatedAt).IsRequired();
        readiness.Property(item => item.ReconciliationVersion).IsRequired();
        readiness.Property(item => item.BusinessReady).IsRequired();
        readiness.Property(item => item.ProductionReady).IsRequired();
        readiness.Property(item => item.Mesp48Complete).IsRequired();
        readiness.Property(item => item.Mesp50Complete).IsRequired();
        readiness.Property(item => item.TenantActivationPerformed).IsRequired();
        ConfigureVersion(readiness.Property(item => item.Version));
        readiness.HasIndex(item => new { item.TenantId, item.ReconciliationId, item.ReconciliationVersion }).IsUnique();
        readiness.HasIndex(item => new { item.TenantId, item.RunId, item.IdempotencyKey }).IsUnique();
        readiness.HasOne<MigrationReconciliationEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.ReconciliationId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        readiness.HasOne<MigrationAttemptEntity>().WithMany()
            .HasForeignKey(item => new { item.TenantId, item.RunId, item.AttemptId })
            .HasPrincipalKey(item => new { item.TenantId, item.RunId, item.AttemptId }).OnDelete(DeleteBehavior.Restrict);
        readiness.HasQueryFilter(item => item.TenantId == TrustedTenantId);
    }

    private void ConfigureTenant(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<TenantId> property) =>
        property.HasConversion(item => item.Value, value => new TenantId(value)).IsRequired();

    private void ConfigureVersion(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<byte[]> property)
    {
        property.IsRequired().IsConcurrencyToken();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            property.IsRowVersion();
        }
        else
        {
            property.ValueGeneratedNever();
        }
    }
}

#pragma warning restore CS1591
