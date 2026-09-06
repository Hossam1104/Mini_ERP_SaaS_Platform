#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using Microsoft.EntityFrameworkCore;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal static class FinanceJournalMonetaryEvidenceFactory
{
    internal static async Task<(bool Succeeded, string Code, FinanceMonetaryEvidence? Evidence)> BuildAsync(
        FinanceDbContext db,
        TenantContext tenantContext,
        IMasterDataExchangeRatePersistence exchangeRates,
        Guid companyId,
        DateOnly date,
        string transactionCurrencyCode,
        decimal transactionAmount,
        string functionalCurrencyCode,
        decimal functionalAmount,
        decimal? transactionToFunctionalRate,
        Guid? transactionToFunctionalRateId,
        Guid? transactionToFunctionalRateVersionId,
        int? transactionToFunctionalRateVersionNumber,
        CancellationToken cancellationToken)
    {
        var policy = await db.MonetaryPolicies
            .Where(item => item.CompanyId == companyId
                && item.EffectiveFrom <= date
                && (item.EffectiveTo == null || item.EffectiveTo >= date))
            .OrderByDescending(item => item.VersionNumber)
            .SingleOrDefaultAsync(cancellationToken);
        if (policy is null) return (true, "monetary_policy_not_configured", null);

        var transactionCurrency = Normalize(transactionCurrencyCode);
        var functionalCurrency = Normalize(functionalCurrencyCode);
        if (transactionCurrency is null || functionalCurrency is null) return (false, "exact_exchange_rate_evidence_required", null);

        FinanceExchangeRateEvidence? transactionRate = null;
        decimal sourceUnroundedFunctionalAmount;
        if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            sourceUnroundedFunctionalAmount = functionalAmount;
        }
        else
        {
            if (transactionToFunctionalRate is not > 0m
                || transactionToFunctionalRateId is null
                || transactionToFunctionalRateVersionId is null
                || transactionToFunctionalRateVersionNumber is not > 0)
                return (false, "exact_exchange_rate_evidence_required", null);

            transactionRate = await ResolveRateAsync(
                tenantContext,
                exchangeRates,
                transactionCurrency,
                functionalCurrency,
                date,
                transactionToFunctionalRateId,
                transactionToFunctionalRateVersionId,
                transactionToFunctionalRateVersionNumber,
                transactionToFunctionalRate,
                cancellationToken);
            if (transactionRate is null) return (false, "exchange_rate_evidence_mismatch", null);
            sourceUnroundedFunctionalAmount = transactionAmount * transactionRate.Rate;
        }

        var reportingCurrency = Normalize(policy.ReportingCurrencyCode);
        if (reportingCurrency is null)
        {
            return (true, "succeeded", new FinanceMonetaryEvidence(
                transactionCurrency,
                transactionAmount,
                functionalCurrency,
                functionalAmount,
                transactionRate,
                null,
                null,
                null,
                sourceUnroundedFunctionalAmount,
                null,
                policy.RoundingScale,
                policy.RoundingMode,
                functionalAmount - sourceUnroundedFunctionalAmount,
                null,
                FinanceEvidenceStatus.NotCaptured));
        }

        if (string.Equals(reportingCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "succeeded", new FinanceMonetaryEvidence(
                transactionCurrency,
                transactionAmount,
                functionalCurrency,
                functionalAmount,
                transactionRate,
                functionalCurrency,
                functionalAmount,
                null,
                sourceUnroundedFunctionalAmount,
                sourceUnroundedFunctionalAmount,
                policy.RoundingScale,
                policy.RoundingMode,
                functionalAmount - sourceUnroundedFunctionalAmount,
                functionalAmount - sourceUnroundedFunctionalAmount,
                FinanceEvidenceStatus.Captured));
        }

        var reportingRate = await ResolveRateAsync(
            tenantContext,
            exchangeRates,
            functionalCurrency,
            reportingCurrency,
            date,
            null,
            null,
            null,
            null,
            cancellationToken);
        if (reportingRate is null) return (false, "reporting_exchange_rate_required", null);
        var sourceUnroundedReportingAmount = functionalAmount * reportingRate.Rate;
        var reportingAmount = decimal.Round(
            sourceUnroundedReportingAmount,
            policy.RoundingScale,
            Rounding(policy.RoundingMode));

        return (true, "succeeded", new FinanceMonetaryEvidence(
            transactionCurrency,
            transactionAmount,
            functionalCurrency,
            functionalAmount,
            transactionRate,
            reportingCurrency,
            reportingAmount,
            reportingRate,
            sourceUnroundedFunctionalAmount,
            sourceUnroundedReportingAmount,
            policy.RoundingScale,
            policy.RoundingMode,
            functionalAmount - sourceUnroundedFunctionalAmount,
            reportingAmount - sourceUnroundedReportingAmount,
            FinanceEvidenceStatus.Captured));
    }

    internal static FinanceMonetaryEvidence Negate(FinanceMonetaryEvidence evidence) => evidence with
    {
        TransactionAmount = -evidence.TransactionAmount,
        FunctionalAmount = -evidence.FunctionalAmount,
        ReportingAmount = evidence.ReportingAmount is null ? null : -evidence.ReportingAmount,
        SourceUnroundedFunctionalAmount = -evidence.SourceUnroundedFunctionalAmount,
        SourceUnroundedReportingAmount = evidence.SourceUnroundedReportingAmount is null ? null : -evidence.SourceUnroundedReportingAmount,
        FunctionalRoundingDifference = -evidence.FunctionalRoundingDifference,
        ReportingRoundingDifference = evidence.ReportingRoundingDifference is null ? null : -evidence.ReportingRoundingDifference
    };

    private static async Task<FinanceExchangeRateEvidence?> ResolveRateAsync(
        TenantContext tenantContext,
        IMasterDataExchangeRatePersistence exchangeRates,
        string source,
        string target,
        DateOnly date,
        Guid? expectedId,
        Guid? expectedVersionId,
        int? expectedVersionNumber,
        decimal? expectedRate,
        CancellationToken cancellationToken)
    {
        var records = await exchangeRates.ListExchangeRatesAsync(tenantContext, cancellationToken);
        var candidates = records
            .Where(item => (expectedId is not null || item.LifecycleState == MasterDataLifecycleState.Active)
                && string.Equals(item.SourceCurrencyCode, source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.TargetCurrencyCode, target, StringComparison.OrdinalIgnoreCase))
            .SelectMany(item => item.Versions
                .Where(version => version.EffectiveFrom <= date
                    && (version.EffectiveTo == null || version.EffectiveTo >= date))
                .Select(version => (item, version)))
            .Where(value => (expectedId is null || value.item.Id == expectedId)
                && (expectedVersionId is null || value.version.Id == expectedVersionId)
                && (expectedVersionNumber is null || value.version.VersionNumber == expectedVersionNumber)
                && (expectedRate is null || value.version.Rate == expectedRate))
            .ToArray();
        if (candidates.Length != 1) return null;
        var selected = candidates[0];
        return new FinanceExchangeRateEvidence(
            selected.item.Id,
            selected.version.Id,
            selected.version.VersionNumber,
            source,
            target,
            date,
            selected.version.Rate,
            selected.version.RateScale,
            selected.version.Provenance.ToString(),
            selected.version.SourceNotes,
            $"{source}->{target};v{selected.version.VersionNumber}@{date:yyyy-MM-dd}",
            selected.version.EffectiveFrom,
            selected.version.EffectiveTo);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static MidpointRounding Rounding(string mode) => string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase) ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;
}

#pragma warning restore CS1591
