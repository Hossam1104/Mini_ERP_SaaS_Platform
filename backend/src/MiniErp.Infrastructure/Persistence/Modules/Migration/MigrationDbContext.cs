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
