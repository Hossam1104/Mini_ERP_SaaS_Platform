#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using Microsoft.EntityFrameworkCore;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal static class FinanceJournalMonetaryEvidenceFactory
{
    internal sealed record MigrationOpeningMonetaryResolution(bool Succeeded, string Code, FinanceMonetaryEvidence? Evidence);

    internal static async Task<MigrationOpeningMonetaryResolution> ResolveMigrationOpeningAsync(
        FinanceDbContext db,
        TenantContext tenantContext,
        IMasterDataExchangeRatePersistence exchangeRates,
        Guid companyId,
        DateOnly openingDate,
        string transactionCurrencyCode,
        decimal transactionAmount,
        string functionalCurrencyCode,
        FinanceMigrationOpeningExpectation? expected,
        CancellationToken cancellationToken)
    {
        var transactionCurrency = Normalize(transactionCurrencyCode);
        var functionalCurrency = Normalize(functionalCurrencyCode);
        if (transactionCurrency is null || functionalCurrency is null || transactionAmount <= 0m)
            return new(false, "migration_opening_currency_invalid", null);
        var policies = await db.MonetaryPolicies
            .Where(item => item.CompanyId == companyId
                && item.EffectiveFrom <= openingDate
                && (item.EffectiveTo == null || item.EffectiveTo >= openingDate))
            .OrderByDescending(item => item.VersionNumber)
            .ToListAsync(cancellationToken);
        if (policies.Count > 1)
            return new(false, "migration_opening_monetary_policy_ambiguous", null);

        var policy = policies.SingleOrDefault();
        var foreign = !string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        if (foreign && policy is null)
            return new(false, "migration_opening_monetary_policy_required", null);
        if (policy is null)
            return new(true, "succeeded", null);
        if (policy.RoundingScale is < 0 or > 8 || !IsRoundingMode(policy.RoundingMode))
            return new(false, "migration_opening_rounding_policy_invalid", null);

        FinanceExchangeRateEvidence? transactionRate = null;
        var sourceUnroundedFunctional = transactionAmount;
        if (foreign)
        {
            transactionRate = await ResolveRateAsync(
                tenantContext,
                exchangeRates,
                transactionCurrency,
                functionalCurrency,
                openingDate,
                expected?.ExchangeRateId,
                expected?.ExchangeRateVersionId,
                expected?.ExchangeRateVersionNumber,
                expected?.AppliedRate,
                cancellationToken,
                requireActive: true);
            if (transactionRate is null)
                return new(false, expected?.ExchangeRateId is null ? "migration_opening_exchange_rate_unavailable" : "migration_opening_exchange_rate_drift", null);
            sourceUnroundedFunctional = transactionAmount * transactionRate.Rate;
        }

        var functionalAmount = foreign
            ? Round(sourceUnroundedFunctional, policy.RoundingScale, policy.RoundingMode)
            : transactionAmount;
        if (functionalAmount is null)
            return new(false, "migration_opening_rounding_policy_invalid", null);

        var reportingCurrency = Normalize(policy.ReportingCurrencyCode);
        FinanceExchangeRateEvidence? reportingRate = null;
        decimal? reportingAmount = null;
        decimal? sourceUnroundedReporting = null;
        var reportingStatus = FinanceEvidenceStatus.NotCaptured;
        if (reportingCurrency is not null)
        {
            if (string.Equals(reportingCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                reportingAmount = functionalAmount.Value;
                sourceUnroundedReporting = functionalAmount.Value;
                reportingStatus = FinanceEvidenceStatus.Captured;
            }
            else
            {
                reportingRate = await ResolveRateAsync(
                    tenantContext,
                    exchangeRates,
                    functionalCurrency,
                    reportingCurrency,
                    openingDate,
                    expected?.ReportingExchangeRateId,
                    expected?.ReportingExchangeRateVersionId,
                    expected?.ReportingExchangeRateVersionNumber,
                    expected?.ReportingAppliedRate,
                    cancellationToken,
                    requireActive: true);
                if (reportingRate is null)
                    return new(false, expected?.ReportingExchangeRateId is null ? "migration_opening_reporting_rate_unavailable" : "migration_opening_reporting_rate_drift", null);
                sourceUnroundedReporting = functionalAmount.Value * reportingRate.Rate;
                reportingAmount = Round(sourceUnroundedReporting.Value, policy.RoundingScale, policy.RoundingMode);
                if (reportingAmount is null)
                    return new(false, "migration_opening_rounding_policy_invalid", null);
                reportingStatus = FinanceEvidenceStatus.Captured;
            }
        }

        var evidence = new FinanceMonetaryEvidence(
            transactionCurrency,
            transactionAmount,
            functionalCurrency,
            functionalAmount.Value,
            transactionRate,
            reportingCurrency,
            reportingAmount,
            reportingRate,
            sourceUnroundedFunctional,
            sourceUnroundedReporting,
            policy.RoundingScale,
            policy.RoundingMode,
            functionalAmount.Value - sourceUnroundedFunctional,
            reportingAmount is null || sourceUnroundedReporting is null ? null : reportingAmount.Value - sourceUnroundedReporting.Value,
            reportingStatus,
            policy.Id,
            policy.VersionNumber);
        return expected is not null && !MatchesExpected(expected, evidence, openingDate)
            ? new(false, "migration_opening_monetary_evidence_drift", null)
            : new(true, "succeeded", evidence);
    }

    internal static FinanceMigrationOpeningExpectation ApplyExpectation(
        FinanceMigrationOpeningExpectation expectation,
        FinanceMonetaryEvidence? evidence,
        DateOnly openingDate,
        string transactionCurrencyCode,
        decimal transactionAmount,
        string functionalCurrencyCode) => expectation with
    {
        TransactionCurrencyCode = Normalize(transactionCurrencyCode),
        TransactionAmount = transactionAmount,
        ExpectedFunctionalCurrencyCode = Normalize(functionalCurrencyCode),
        RateDate = evidence?.TransactionToFunctionalRate is null ? null : openingDate,
        ExchangeRateId = evidence?.TransactionToFunctionalRate?.ExchangeRateId,
        ExchangeRateVersionId = evidence?.TransactionToFunctionalRate?.ExchangeRateVersionId,
        ExchangeRateVersionNumber = evidence?.TransactionToFunctionalRate?.VersionNumber,
        AppliedRate = evidence?.TransactionToFunctionalRate?.Rate,
        MonetaryPolicyId = evidence?.MonetaryPolicyId,
        MonetaryPolicyVersionNumber = evidence?.MonetaryPolicyVersionNumber,
        RoundingScale = evidence?.RoundingScale,
        RoundingMode = evidence?.RoundingMode,
        ReportingCurrencyCode = evidence?.ReportingCurrencyCode,
        ReportingExchangeRateId = evidence?.FunctionalToReportingRate?.ExchangeRateId,
        ReportingExchangeRateVersionId = evidence?.FunctionalToReportingRate?.ExchangeRateVersionId,
        ReportingExchangeRateVersionNumber = evidence?.FunctionalToReportingRate?.VersionNumber,
        ReportingAppliedRate = evidence?.FunctionalToReportingRate?.Rate
    };

    private static bool MatchesExpected(FinanceMigrationOpeningExpectation expected, FinanceMonetaryEvidence actual, DateOnly openingDate) =>
        string.Equals(expected.TransactionCurrencyCode, actual.TransactionCurrencyCode, StringComparison.OrdinalIgnoreCase)
        && expected.TransactionAmount == actual.TransactionAmount
        && string.Equals(expected.ExpectedFunctionalCurrencyCode, actual.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
        && expected.FunctionalAmount == actual.FunctionalAmount
        && expected.RateDate == (actual.TransactionToFunctionalRate is null ? null : openingDate)
        && expected.ExchangeRateId == actual.TransactionToFunctionalRate?.ExchangeRateId
        && expected.ExchangeRateVersionId == actual.TransactionToFunctionalRate?.ExchangeRateVersionId
        && expected.ExchangeRateVersionNumber == actual.TransactionToFunctionalRate?.VersionNumber
        && expected.AppliedRate == actual.TransactionToFunctionalRate?.Rate
        && expected.MonetaryPolicyId == actual.MonetaryPolicyId
        && expected.MonetaryPolicyVersionNumber == actual.MonetaryPolicyVersionNumber
        && expected.RoundingScale == actual.RoundingScale
        && string.Equals(expected.RoundingMode, actual.RoundingMode, StringComparison.Ordinal)
        && string.Equals(expected.ReportingCurrencyCode, actual.ReportingCurrencyCode, StringComparison.OrdinalIgnoreCase)
        && expected.ReportingExchangeRateId == actual.FunctionalToReportingRate?.ExchangeRateId
        && expected.ReportingExchangeRateVersionId == actual.FunctionalToReportingRate?.ExchangeRateVersionId
        && expected.ReportingExchangeRateVersionNumber == actual.FunctionalToReportingRate?.VersionNumber
        && expected.ReportingAppliedRate == actual.FunctionalToReportingRate?.Rate;

    private static decimal? Round(decimal amount, int scale, string mode) =>
        scale is >= 0 and <= 28 && IsRoundingMode(mode)
            ? decimal.Round(amount, scale, string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase) ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero)
            : null;

    private static bool IsRoundingMode(string? mode) => string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "AwayFromZero", StringComparison.OrdinalIgnoreCase);

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
        CancellationToken cancellationToken,
        bool requireActive = false)
    {
        var records = await exchangeRates.ListExchangeRatesAsync(tenantContext, cancellationToken);
        var candidates = records
            .Where(item => (requireActive ? item.LifecycleState == MasterDataLifecycleState.Active : expectedId is not null || item.LifecycleState == MasterDataLifecycleState.Active)
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
