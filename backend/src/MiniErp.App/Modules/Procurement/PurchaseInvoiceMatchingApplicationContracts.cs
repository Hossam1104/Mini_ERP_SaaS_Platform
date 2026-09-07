#pragma warning disable CS1591

using Microsoft.Extensions.Options;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseInvoiceMatchingToleranceDefinition(
    string PolicyId,
    int Version,
    decimal QuantityAbsoluteTolerance,
    decimal QuantityPercentageTolerance,
    decimal PriceAbsoluteTolerance,
    decimal PricePercentageTolerance,
    decimal AmountAbsoluteTolerance,
    decimal AmountPercentageTolerance,
    decimal TaxAbsoluteTolerance,
    decimal TaxPercentageTolerance,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo)
{
    public static PurchaseInvoiceMatchingToleranceDefinition ExactSafe(DateTimeOffset now) => new(
        "exact-safe-default", 1, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, DateTimeOffset.MinValue, null);

    public PurchaseInvoiceMatchPolicyResponse ToResponse() => new(
        PolicyId, Version, QuantityAbsoluteTolerance, QuantityPercentageTolerance,
        PriceAbsoluteTolerance, PricePercentageTolerance, AmountAbsoluteTolerance,
        AmountPercentageTolerance, TaxAbsoluteTolerance, TaxPercentageTolerance,
        EffectiveFrom, EffectiveTo);

    public bool IsEffective(DateTimeOffset at) => EffectiveFrom <= at && (EffectiveTo is null || at < EffectiveTo);
}

public sealed record PurchaseInvoiceMatchingToleranceBinding(PurchaseRequestScope Scope, PurchaseInvoiceMatchingToleranceDefinition Definition);

/// <summary>
/// Generic configuration-led matching policy options. Tenant and
/// Company/Branch identifiers are configuration data, never customer logic.
/// An absent or non-applicable entry deliberately falls back to the exact-safe
/// policy.
/// </summary>
public sealed class PurchaseInvoiceMatchingPolicyOptions
{
    public List<PurchaseInvoiceMatchingTolerancePolicyOptions> TolerancePolicies { get; set; } = [];
    public List<PurchaseInvoiceMatchingResolutionPolicyOptions> ResolutionPolicies { get; set; } = [];
}

public sealed class PurchaseInvoiceMatchingTolerancePolicyOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public int Version { get; set; }
    public decimal QuantityAbsoluteTolerance { get; set; }
    public decimal QuantityPercentageTolerance { get; set; }
    public decimal PriceAbsoluteTolerance { get; set; }
    public decimal PricePercentageTolerance { get; set; }
    public decimal AmountAbsoluteTolerance { get; set; }
    public decimal AmountPercentageTolerance { get; set; }
    public decimal TaxAbsoluteTolerance { get; set; }
    public decimal TaxPercentageTolerance { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? EffectiveTo { get; set; }

    internal PurchaseInvoiceMatchingToleranceBinding ToBinding() => new(
        new PurchaseRequestScope(TenantId, CompanyId, BranchId),
        new PurchaseInvoiceMatchingToleranceDefinition(
            PolicyId,
            Version,
            QuantityAbsoluteTolerance,
            QuantityPercentageTolerance,
            PriceAbsoluteTolerance,
            PricePercentageTolerance,
            AmountAbsoluteTolerance,
            AmountPercentageTolerance,
            TaxAbsoluteTolerance,
            TaxPercentageTolerance,
            EffectiveFrom,
            EffectiveTo));
}

public sealed class PurchaseInvoiceMatchingResolutionPolicyOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool AllowResolution { get; set; }
    public bool RequireDifferentActor { get; set; }
    public bool RequireReason { get; set; } = true;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset? EffectiveTo { get; set; }

    internal PurchaseInvoiceMatchingResolutionPolicyBinding ToBinding() => new(
        new PurchaseRequestScope(TenantId, CompanyId, BranchId),
        new PurchaseInvoiceMatchingResolutionPolicyDefinition(
            PolicyId,
            Version,
            AllowResolution,
            RequireDifferentActor,
            RequireReason,
            EffectiveFrom,
            EffectiveTo));
}

public interface IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed class ExactSafePurchaseInvoiceMatchingTolerancePolicyProvider : IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    public Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(at));
}

public sealed class ConfiguredPurchaseInvoiceMatchingTolerancePolicyProvider : IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    private readonly IReadOnlyList<PurchaseInvoiceMatchingToleranceBinding> bindings;

    public ConfiguredPurchaseInvoiceMatchingTolerancePolicyProvider(IEnumerable<PurchaseInvoiceMatchingToleranceBinding> bindings)
    {
        this.bindings = bindings?.ToArray() ?? throw new ArgumentNullException(nameof(bindings));
    }

    public Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(at);
        return Task.FromResult(selected);
    }
}

public sealed class ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider : IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    private readonly IOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions> options;

    public ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider(IOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions> options) =>
        this.options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = options.CurrentValue.TolerancePolicies
            .Select(item => item.ToBinding())
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(at);
        return Task.FromResult(selected);
    }
}

public sealed record PurchaseInvoiceMatchingResolutionPolicyDefinition(
    string PolicyId,
    int Version,
    bool AllowResolution,
    bool RequireDifferentActor,
    bool RequireReason,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo)
{
    public static PurchaseInvoiceMatchingResolutionPolicyDefinition Default(DateTimeOffset at) => new(
        "controlled-exception-resolution", 1, true, false, true, DateTimeOffset.MinValue, null);

    public bool IsEffective(DateTimeOffset at) => EffectiveFrom <= at && (EffectiveTo is null || at < EffectiveTo);

    public PurchaseInvoiceMatchResolutionPolicyResponse ToResponse() => new(
        PolicyId, Version, AllowResolution, RequireDifferentActor, RequireReason, EffectiveFrom, EffectiveTo);
}

public sealed record PurchaseInvoiceMatchingResolutionPolicyBinding(PurchaseRequestScope Scope, PurchaseInvoiceMatchingResolutionPolicyDefinition Definition);

public interface IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed class DefaultPurchaseInvoiceMatchingResolutionPolicyProvider : IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    public Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseInvoiceMatchingResolutionPolicyDefinition.Default(at));
}

public sealed class ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider : IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    private readonly IReadOnlyList<PurchaseInvoiceMatchingResolutionPolicyBinding> bindings;

    public ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider(IEnumerable<PurchaseInvoiceMatchingResolutionPolicyBinding> bindings)
    {
        this.bindings = bindings?.ToArray() ?? throw new ArgumentNullException(nameof(bindings));
    }

    public Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingResolutionPolicyDefinition.Default(at);
        return Task.FromResult(selected);
    }
}

public sealed class ConfigurationPurchaseInvoiceMatchingResolutionPolicyProvider : IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    private readonly IOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions> options;

    public ConfigurationPurchaseInvoiceMatchingResolutionPolicyProvider(IOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions> options) =>
        this.options = options ?? throw new ArgumentNullException(nameof(options));

    public Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = options.CurrentValue.ResolutionPolicies
            .Select(item => item.ToBinding())
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingResolutionPolicyDefinition.Default(at);
        return Task.FromResult(selected);
    }
}

public sealed record PurchaseInvoiceMatchVarianceRecord(
    string Classification,
    Guid? PurchaseOrderLineId,
    Guid? GoodsReceiptLineId,
    decimal? ExpectedValue,
    decimal? ActualValue,
    decimal? Variance,
    decimal AllowedTolerance,
    string? CurrencyCode,
    string? Details)
{
    public PurchaseInvoiceMatchVarianceResponse ToResponse() => new(Classification, PurchaseOrderLineId, GoodsReceiptLineId, ExpectedValue, ActualValue, Variance, AllowedTolerance, CurrencyCode, Details);
}

public sealed record PurchaseInvoiceMatchExchangeRateRecord(
    Guid ExchangeRateId,
    Guid ExchangeRateVersionId,
    int VersionNumber,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    decimal Rate,
    int Scale,
    string? Provenance,
    string? Source,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public PurchaseInvoiceMatchExchangeRateResponse ToResponse() => new(
        ExchangeRateId,
        ExchangeRateVersionId,
        VersionNumber,
        SourceCurrencyCode,
        TargetCurrencyCode,
        Rate,
        Scale,
        Provenance,
        Source,
        EffectiveOn,
        EffectiveFrom,
        EffectiveTo);
}

public sealed record PurchaseInvoiceMatchExchangeRateResolution(
    bool Succeeded,
    string Code,
    PurchaseInvoiceMatchExchangeRateRecord? Value)
{
    public static PurchaseInvoiceMatchExchangeRateResolution Success(PurchaseInvoiceMatchExchangeRateRecord value) => new(true, "resolved", value);
    public static PurchaseInvoiceMatchExchangeRateResolution Failure(string code) => new(false, code, null);
}

public interface IPurchaseInvoiceMatchingExchangeRateReferenceProvider
{
    Task<PurchaseInvoiceMatchExchangeRateResolution> ResolveAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        string sourceCurrencyCode,
        string targetCurrencyCode,
        DateOnly? supplierInvoiceDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves only a stable Exchange Rate identity through the existing MESP-120
/// Master Data persistence contract. The returned rate/version/provenance are
/// server-owned evidence and are safe to snapshot into Procurement matching.
/// </summary>
public sealed class MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider : IPurchaseInvoiceMatchingExchangeRateReferenceProvider
{
    private readonly IMasterDataExchangeRatePersistence persistence;

    public MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(IMasterDataExchangeRatePersistence persistence) =>
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));

    public async Task<PurchaseInvoiceMatchExchangeRateResolution> ResolveAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        string sourceCurrencyCode,
        string targetCurrencyCode,
        DateOnly? supplierInvoiceDate,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext is null || exchangeRateId == Guid.Empty
            || string.IsNullOrWhiteSpace(sourceCurrencyCode)
            || string.IsNullOrWhiteSpace(targetCurrencyCode))
        {
            return PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable");
        }

        if (supplierInvoiceDate is null) return PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable");

        MasterDataExchangeRateRecord? record;
        try
        {
            record = await persistence.FindExchangeRateAsync(tenantContext, exchangeRateId, cancellationToken);
        }
        catch
        {
            return PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable");
        }

        if (record is null
            || record.TenantId.Value != tenantContext.TenantId.Value
            || record.LifecycleState != MasterDataLifecycleState.Active
            || !string.Equals(record.SourceCurrencyCode, sourceCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(record.TargetCurrencyCode, targetCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable");
        }

        var version = record.Versions
            .Where(item => item.EffectiveFrom <= supplierInvoiceDate.Value
                && (item.EffectiveTo is null || supplierInvoiceDate.Value <= item.EffectiveTo.Value))
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefault();
        if (version is null
            || version.Rate <= 0m
            || version.RateScale <= 0
            || !string.Equals(version.SourceCurrencyCode, sourceCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(version.TargetCurrencyCode, targetCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable");
        }

        return PurchaseInvoiceMatchExchangeRateResolution.Success(new PurchaseInvoiceMatchExchangeRateRecord(
            record.Id,
            version.Id,
            version.VersionNumber,
            version.SourceCurrencyCode,
            version.TargetCurrencyCode,
            version.Rate,
            version.RateScale,
            version.Provenance.ToString(),
            version.SourceNotes,
            supplierInvoiceDate.Value,
            version.EffectiveFrom,
            version.EffectiveTo));
    }
}

public sealed class UnavailablePurchaseInvoiceMatchingExchangeRateReferenceProvider : IPurchaseInvoiceMatchingExchangeRateReferenceProvider
{
    public Task<PurchaseInvoiceMatchExchangeRateResolution> ResolveAsync(TenantContext tenantContext, Guid exchangeRateId, string sourceCurrencyCode, string targetCurrencyCode, DateOnly? supplierInvoiceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable"));
}

public sealed record PurchaseInvoiceMatchRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid EvaluatedByActorId,
    Guid? ResolvedByActorId,
    DateTimeOffset? ResolvedAt,
    string? ResolutionReason,
    string SourceFingerprint,
    byte[] PurchaseOrderVersion,
    byte[] HandoffVersion,
    Guid? DeclaredEvidenceId,
    int? DeclaredEvidenceVersion,
    PurchaseInvoiceMatchingToleranceDefinition Policy,
    PurchaseInvoiceMatchingResolutionPolicyDefinition? ResolutionPolicy,
    PurchaseInvoiceMatchExchangeRateRecord? AppliedExchangeRate,
    IReadOnlyList<PurchaseInvoiceMatchVarianceRecord> Variances,
    string? SourceSnapshot,
    byte[] Version);

public sealed record PurchaseInvoiceMatchListRecord(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid? ResolvedByActorId,
    int VarianceCount,
    byte[] Version);

public sealed record PurchaseInvoiceMatchReportingRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid? ResolvedByActorId,
    DateTimeOffset? ResolvedAt,
    string? ResolutionReason,
    Guid? DeclaredEvidenceId,
    int? DeclaredEvidenceVersion,
    string SourceFingerprint,
    IReadOnlyList<PurchaseInvoiceMatchVarianceRecord> Variances,
    byte[] Version);

public interface IPurchaseInvoiceMatchReportingReadPort
{
    Task<ReportingSourcePage<PurchaseInvoiceMatchReportingRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        PurchaseInvoiceMatchResult? result,
        DateOnly? fromDate,
        DateOnly? toDate,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default);
}

public sealed record PurchaseInvoiceMatchHistoryRecord(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    PurchaseInvoiceMatchResult Result,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record PurchaseInvoiceMatchAuditRecord(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    string OperationId,
    Guid TenantId,
    Guid ActorId,
    string Decision,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchEvaluateCommand(
    Guid PurchaseInvoiceHandoffId,
    byte[] ExpectedHandoffVersion,
    Guid ActorId,
    PurchaseInvoiceMatchingToleranceDefinition Policy,
    PurchaseInvoiceMatchExchangeRateRecord? AppliedExchangeRate,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchResolveCommand(
    Guid MatchEvaluationId,
    byte[] ExpectedMatchVersion,
    Guid ActorId,
    PurchaseInvoiceMatchingResolutionPolicyDefinition Policy,
    string Reason,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchAuditEvidence(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    Guid TenantId,
    Guid ActorId,
    string OperationId,
    string CorrelationId,
    string Decision,
    string? Reason,
    string? IdempotencyKey,
    string? RequestFingerprint,
    DateTimeOffset OccurredAt);

public enum PurchaseInvoiceMatchPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record PurchaseInvoiceMatchPersistenceResult<T>(PurchaseInvoiceMatchPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == PurchaseInvoiceMatchPersistenceOutcome.Succeeded;
    public static PurchaseInvoiceMatchPersistenceResult<T> Success(T value) => new(PurchaseInvoiceMatchPersistenceOutcome.Succeeded, "persisted", value);
    public static PurchaseInvoiceMatchPersistenceResult<T> Denied(PurchaseInvoiceMatchPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public interface IPurchaseInvoiceMatchPersistence
{
    Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(TenantContext tenantContext, Guid? handoffId, PurchaseInvoiceMatchResult? result, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchRecord?> FindAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(TenantContext tenantContext, Guid handoffId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(TenantContext tenantContext, PurchaseInvoiceMatchEvaluateCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(TenantContext tenantContext, PurchaseInvoiceMatchResolveCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
}

public sealed class UnavailablePurchaseInvoiceMatchPersistence : IPurchaseInvoiceMatchPersistence, IPurchaseInvoiceMatchReportingReadPort
{
    private static Task<PurchaseInvoiceMatchPersistenceResult<T>> Unavailable<T>() => Task.FromResult(PurchaseInvoiceMatchPersistenceResult<T>.Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable"));
    public Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(TenantContext tenantContext, Guid? handoffId, PurchaseInvoiceMatchResult? result, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>([]);
    public Task<PurchaseInvoiceMatchRecord?> FindAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(null);
    public Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(TenantContext tenantContext, Guid handoffId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(null);
    public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(TenantContext tenantContext, PurchaseInvoiceMatchEvaluateCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchRecord>();
    public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(TenantContext tenantContext, PurchaseInvoiceMatchResolveCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchRecord>();
    public Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>([]);
    public Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>([]);
    public Task<ReportingSourcePage<PurchaseInvoiceMatchReportingRecord>> ListReportingPageAsync(TenantContext tenantContext, PurchaseInvoiceMatchResult? result, DateOnly? fromDate, DateOnly? toDate, ReportingPageRequest page, CancellationToken cancellationToken = default) => throw new InvalidOperationException("purchase_invoice_match_reporting_unavailable");
}

#pragma warning restore CS1591
