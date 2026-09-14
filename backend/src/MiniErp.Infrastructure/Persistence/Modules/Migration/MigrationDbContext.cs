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
