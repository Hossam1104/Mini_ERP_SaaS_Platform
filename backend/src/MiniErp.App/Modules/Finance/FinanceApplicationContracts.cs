#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Finance;

public sealed class FinanceRequestContext
{
    private FinanceRequestContext(FoundationRequestContext foundationContext)
    {
        FoundationContext = foundationContext;
        TenantId = foundationContext.TenantContext!.TenantId;
        ActorId = foundationContext.ActorId!.Value;
        SessionId = foundationContext.SessionId!.Value;
        AuthorizationPath = foundationContext.TenantContext.AuthorizationPath;
    }

    public FoundationRequestContext FoundationContext { get; }
    public TenantId TenantId { get; }
    public Guid ActorId { get; }
    public Guid SessionId { get; }
    public TenantAuthorizationPath AuthorizationPath { get; }
    public TenantContext TenantContext => FoundationContext.TenantContext!;
    public string CorrelationId => TenantContext.CorrelationId?.Value ?? string.Empty;
    public InventoryRequestContext ToInventoryRequestContext() => InventoryRequestContext.FromFoundationContext(FoundationContext);

    public static bool TryCreate(FoundationRequestContext foundationContext, out FinanceRequestContext? context)
    {
        context = null;
        if (foundationContext is null
            || foundationContext.TenantContext is null
            || foundationContext.SecurityProfile is not (FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
            || foundationContext.ActorId is not { } actorId || actorId == Guid.Empty
            || foundationContext.SessionId is not { } sessionId || sessionId == Guid.Empty)
        {
            return false;
        }

        context = new FinanceRequestContext(foundationContext);
        return true;
    }
}

public sealed record FinanceAuthorizationResult(bool Allowed, string Code)
{
    public static FinanceAuthorizationResult Success() => new(true, "allowed");
    public static FinanceAuthorizationResult Denied(string code) => new(false, code);
}

public enum FinanceResourceType
{
    Account = 1,
    Calendar = 2,
    FiscalYear = 3,
    FiscalPeriod = 4,
    CostCenter = 5,
    PostingRule = 6,
    Journal = 7,
    InventoryHandoff = 8
}

public interface IFinanceSourceApprovalPolicy
{
    FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent);
}

public sealed class UnconfiguredFinanceSourceApprovalPolicy : IFinanceSourceApprovalPolicy
{
    public static UnconfiguredFinanceSourceApprovalPolicy Instance { get; } = new();
    public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) => FinanceApprovalRequirement.NotConfigured;
}

public static class FinanceInventoryPostingClassifier
{
    public static string Classify(InventoryMovementSourceType sourceType, InventoryMovementDirection direction) => $"{sourceType}:{direction}";
}

public sealed class FinanceAuthorizationService
{
    private readonly IFinanceCompanyProvider companies;

    public FinanceAuthorizationService(IFinanceCompanyProvider companies) => this.companies = companies;

    public FinanceAuthorizationResult Authorize(FinanceRequestContext context, string operationId, Guid? companyId = null)
    {
        if (!FoundationOperationCatalog.TryGet(operationId, out var descriptor)
            || descriptor.ExactPermissionCode is null
            || context.FoundationContext.Permission != descriptor.ExactPermissionCode)
        {
            return FinanceAuthorizationResult.Denied("permission_denied");
        }

        if (companyId is not { } targetCompany || targetCompany == Guid.Empty)
        {
            return FinanceAuthorizationResult.Success();
        }

        var companyOptions = companies.List(context.TenantId)
            .Where(item => item.CompanyId == targetCompany && item.IsActive)
            .ToArray();
        if (companyOptions.Length == 0 || companyOptions.Select(item => item.FunctionalCurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
        {
            return FinanceAuthorizationResult.Denied("company_scope_denied");
        }

        if (context.TenantContext.Scope is { } scope)
        {
            var value = scope.Value;
            if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase)
                && (!Guid.TryParse(value["Company:".Length..], out var scopedCompany) || scopedCompany != targetCompany))
            {
                return FinanceAuthorizationResult.Denied("company_scope_denied");
            }

            if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase)
                && (!Guid.TryParse(value["Branch:".Length..], out var branchId)
                    || !companyOptions.Any(item => item.BranchId == branchId)))
            {
                return FinanceAuthorizationResult.Denied("company_scope_denied");
            }
        }

        return FinanceAuthorizationResult.Success();
    }
}

public interface IFinanceCompanyProvider
{
    IReadOnlyList<FinanceCompanyOption> List(TenantId tenantId);

    IReadOnlyList<FinanceCompanyOption> ListAll(TenantId tenantId) => List(tenantId);
}

public sealed class NoFinanceCompanyProvider : IFinanceCompanyProvider
{
    public IReadOnlyList<FinanceCompanyOption> List(TenantId tenantId) => [];
}

public sealed class ConfiguredFinanceCompanyProvider(IEnumerable<FinanceCompanyOption> options) : IFinanceCompanyProvider
{
    private readonly IReadOnlyList<FinanceCompanyOption> options = options
        .Where(item => item.TenantId != Guid.Empty && item.CompanyId != Guid.Empty && !string.IsNullOrWhiteSpace(item.CompanyName)
            && !string.IsNullOrWhiteSpace(item.FunctionalCurrencyCode))
        .Select(item => item with { FunctionalCurrencyCode = item.FunctionalCurrencyCode.Trim().ToUpperInvariant() })
        .GroupBy(item => (item.TenantId, item.CompanyId, item.BranchId))
        .Select(group => group.First())
        .ToArray();

    public IReadOnlyList<FinanceCompanyOption> List(TenantId tenantId) => options
        .Where(item => item.TenantId == tenantId.Value && item.IsActive)
        .OrderBy(item => item.CompanyName, StringComparer.Ordinal)
        .ThenBy(item => item.CompanyId)
        .ToArray();

    public IReadOnlyList<FinanceCompanyOption> ListAll(TenantId tenantId) => options
        .Where(item => item.TenantId == tenantId.Value)
        .OrderBy(item => item.CompanyName, StringComparer.Ordinal)
        .ThenBy(item => item.CompanyId)
        .ToArray();
}

public sealed record FinanceAccountCommand(
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    Guid? ParentAccountId,
    FinanceAccountType AccountType,
    bool IsPostingAccount,
    FinanceCurrencyBehavior CurrencyBehavior,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid Id,
    byte[]? ExpectedVersion,
    string IdempotencyKey,
    string RequestFingerprint);

public sealed record FinanceFiscalCalendarCommand(Guid CompanyId, string Name, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceFiscalYearCommand(Guid CalendarId, int YearNumber, DateOnly StartDate, DateOnly EndDate, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceFiscalPeriodCommand(Guid FiscalYearId, int Sequence, string Code, string? EnglishName, string? ArabicName, DateOnly StartDate, DateOnly EndDate, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinancePeriodStateCommand(Guid PeriodId, FinanceFiscalPeriodState State, string? Reason, byte[] ExpectedVersion, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceCostCenterCommand(Guid CompanyId, string Code, string EnglishName, string? ArabicName, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinancePostingRuleCommand(Guid CompanyId, string SourceContract, string SourceEvent, Guid DebitAccountId, Guid CreditAccountId, bool CostCenterRequired, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceJournalLineCommand(Guid AccountId, decimal Debit, decimal Credit, decimal? TransactionAmount, string? TransactionCurrencyCode, Guid? CostCenterId, string? Description);
public sealed record FinanceJournalCommand(Guid CompanyId, DateOnly JournalDate, DateOnly PostingDate, string? TransactionCurrencyCode, decimal? ExchangeRate, Guid? ExchangeRateId, Guid? ExchangeRateVersionId, int? ExchangeRateVersionNumber, string SourceContract, string SourceEvent, Guid? SourceEvidenceId, int? SourceEvidenceVersion, Guid? PostingRuleId, string Description, IReadOnlyList<FinanceJournalLineCommand> Lines, Guid Id, string IdempotencyKey, string RequestFingerprint, FinanceJournalAmountAuthority AmountAuthority = FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement ApprovalRequirement = FinanceApprovalRequirement.Required);
public sealed record FinanceJournalActionCommand(Guid JournalId, byte[] ExpectedVersion, string? Reason, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceReversalCommand(Guid JournalId, DateOnly PostingDate, string Reason, Guid Id, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceHandoffProcessCommand(Guid HandoffId, string IdempotencyKey, string RequestFingerprint);
public sealed record FinanceGlQuery(Guid CompanyId, Guid? AccountId = null, Guid? FiscalPeriodId = null, DateOnly? From = null, DateOnly? To = null, Guid? CostCenterId = null, string? SourceContract = null);

public interface IFinancePersistence
{
    Task<IReadOnlyList<FinanceAccountRecord>> ListAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceAccountRecord>> CreateAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceAccountRecord>> EditAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceAccountRecord>> SetAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinanceAccountLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceFiscalCalendarRecord>> ListCalendarsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, FinanceResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceFiscalCalendarRecord>> CreateCalendarAsync(FinanceRequestContext context, FinanceFiscalCalendarCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceFiscalYearRecord>> ListYearsAsync(FinanceRequestContext context, Guid calendarId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceFiscalYearRecord>> CreateYearAsync(FinanceRequestContext context, FinanceFiscalYearCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceFiscalPeriodRecord>> ListPeriodsAsync(FinanceRequestContext context, Guid fiscalYearId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> CreatePeriodAsync(FinanceRequestContext context, FinanceFiscalPeriodCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> SetPeriodStateAsync(FinanceRequestContext context, FinancePeriodStateCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceCostCenterRecord>> ListCostCentersAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceCostCenterRecord>> CreateCostCenterAsync(FinanceRequestContext context, FinanceCostCenterCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancePostingRuleRecord>> ListPostingRulesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePostingRuleRecord>> CreatePostingRuleAsync(FinanceRequestContext context, FinancePostingRuleCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinancePostingRuleRecord>> SetPostingRuleLifecycleAsync(FinanceRequestContext context, Guid ruleId, Guid companyId, FinancePostingRuleLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceJournalRecord>> ListJournalsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> CreateJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> EditJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> TransitionJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, FinanceJournalStatus target, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> PostJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> ReverseJournalAsync(FinanceRequestContext context, FinanceReversalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceGlLineRecord>> QueryGlAsync(FinanceRequestContext context, FinanceGlQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceHandoffRecord>> ListHandoffsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceJournalRecord>> ProcessHandoffAsync(FinanceRequestContext context, FinanceHandoffProcessCommand command, CancellationToken cancellationToken = default);
}

public sealed class UnavailableFinancePersistence : IFinancePersistence
{
    private static readonly Task<IReadOnlyList<FinanceAccountRecord>> EmptyAccounts = Task.FromResult<IReadOnlyList<FinanceAccountRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceFiscalCalendarRecord>> EmptyCalendars = Task.FromResult<IReadOnlyList<FinanceFiscalCalendarRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceFiscalYearRecord>> EmptyYears = Task.FromResult<IReadOnlyList<FinanceFiscalYearRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceFiscalPeriodRecord>> EmptyPeriods = Task.FromResult<IReadOnlyList<FinanceFiscalPeriodRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceCostCenterRecord>> EmptyCostCenters = Task.FromResult<IReadOnlyList<FinanceCostCenterRecord>>([]);
    private static readonly Task<IReadOnlyList<FinancePostingRuleRecord>> EmptyRules = Task.FromResult<IReadOnlyList<FinancePostingRuleRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceJournalRecord>> EmptyJournals = Task.FromResult<IReadOnlyList<FinanceJournalRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceGlLineRecord>> EmptyGl = Task.FromResult<IReadOnlyList<FinanceGlLineRecord>>([]);
    private static readonly Task<IReadOnlyList<FinanceHandoffRecord>> EmptyHandoffs = Task.FromResult<IReadOnlyList<FinanceHandoffRecord>>([]);
    private static FinanceOperationResult<T> Unavailable<T>() => FinanceOperationResult<T>.Failure("finance_unavailable");
    public Task<IReadOnlyList<FinanceAccountRecord>> ListAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyAccounts;
    public Task<FinanceOperationResult<FinanceAccountRecord>> CreateAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceAccountRecord>());
    public Task<FinanceOperationResult<FinanceAccountRecord>> EditAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceAccountRecord>());
    public Task<FinanceOperationResult<FinanceAccountRecord>> SetAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinanceAccountLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceAccountRecord>());
    public Task<IReadOnlyList<FinanceFiscalCalendarRecord>> ListCalendarsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyCalendars;
    public Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, FinanceResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    public Task<FinanceOperationResult<FinanceFiscalCalendarRecord>> CreateCalendarAsync(FinanceRequestContext context, FinanceFiscalCalendarCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceFiscalCalendarRecord>());
    public Task<IReadOnlyList<FinanceFiscalYearRecord>> ListYearsAsync(FinanceRequestContext context, Guid calendarId, CancellationToken cancellationToken = default) => EmptyYears;
    public Task<FinanceOperationResult<FinanceFiscalYearRecord>> CreateYearAsync(FinanceRequestContext context, FinanceFiscalYearCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceFiscalYearRecord>());
    public Task<IReadOnlyList<FinanceFiscalPeriodRecord>> ListPeriodsAsync(FinanceRequestContext context, Guid fiscalYearId, CancellationToken cancellationToken = default) => EmptyPeriods;
    public Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> CreatePeriodAsync(FinanceRequestContext context, FinanceFiscalPeriodCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceFiscalPeriodRecord>());
    public Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> SetPeriodStateAsync(FinanceRequestContext context, FinancePeriodStateCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceFiscalPeriodRecord>());
    public Task<IReadOnlyList<FinanceCostCenterRecord>> ListCostCentersAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyCostCenters;
    public Task<FinanceOperationResult<FinanceCostCenterRecord>> CreateCostCenterAsync(FinanceRequestContext context, FinanceCostCenterCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceCostCenterRecord>());
    public Task<IReadOnlyList<FinancePostingRuleRecord>> ListPostingRulesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyRules;
    public Task<FinanceOperationResult<FinancePostingRuleRecord>> CreatePostingRuleAsync(FinanceRequestContext context, FinancePostingRuleCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinancePostingRuleRecord>());
    public Task<FinanceOperationResult<FinancePostingRuleRecord>> SetPostingRuleLifecycleAsync(FinanceRequestContext context, Guid ruleId, Guid companyId, FinancePostingRuleLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinancePostingRuleRecord>());
    public Task<IReadOnlyList<FinanceJournalRecord>> ListJournalsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyJournals;
    public Task<FinanceOperationResult<FinanceJournalRecord>> CreateJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
    public Task<FinanceOperationResult<FinanceJournalRecord>> EditJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
    public Task<FinanceOperationResult<FinanceJournalRecord>> TransitionJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, FinanceJournalStatus target, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
    public Task<FinanceOperationResult<FinanceJournalRecord>> PostJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
    public Task<FinanceOperationResult<FinanceJournalRecord>> ReverseJournalAsync(FinanceRequestContext context, FinanceReversalCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
    public Task<IReadOnlyList<FinanceGlLineRecord>> QueryGlAsync(FinanceRequestContext context, FinanceGlQuery query, CancellationToken cancellationToken = default) => EmptyGl;
    public Task<IReadOnlyList<FinanceHandoffRecord>> ListHandoffsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => EmptyHandoffs;
    public Task<FinanceOperationResult<FinanceJournalRecord>> ProcessHandoffAsync(FinanceRequestContext context, FinanceHandoffProcessCommand command, CancellationToken cancellationToken = default) => Task.FromResult(Unavailable<FinanceJournalRecord>());
}

public static class FinanceFingerprint
{
    public static string For(string operation, string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operation}|{value}")));
}

#pragma warning restore CS1591
