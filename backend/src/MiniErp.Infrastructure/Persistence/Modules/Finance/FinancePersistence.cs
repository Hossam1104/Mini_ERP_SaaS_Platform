#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinancePersistence(
    DbContextOptions options,
    IFinanceCompanyProvider companies,
    IInventoryValuationPersistence inventory,
    IMasterDataExchangeRatePersistence exchangeRates,
    IFinanceSourceApprovalPolicy? approvalPolicy = null) : IFinancePersistence
{
    private const string ManualContract = "manual-journal.v1";
    private const string InventoryContract = "inventory-valuation-finance.v1";
    private IFinanceSourceApprovalPolicy SourceApprovalPolicy => approvalPolicy ?? UnconfiguredFinanceSourceApprovalPolicy.Instance;

    private FinanceDbContext CreateContext(FinanceRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<FinanceAccountRecord>> ListAccountsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context);
        return (await db.Accounts.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Code).ToListAsync(cancellationToken)).Select(ToAccount).ToArray();
    }

    public async Task<FinanceOperationResult<FinanceAccountRecord>> CreateAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidCommand(command.CompanyId, command.Code, command.EnglishName, command.EffectiveFrom, command.EffectiveTo, out var code)) return Failure<FinanceAccountRecord>("invalid_account");
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceAccountRecord>(db, context, "finance.account.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var company = Company(context, command.CompanyId);
        if (company is null) return Failure<FinanceAccountRecord>("company_scope_denied");
        if (await db.Accounts.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code, cancellationToken)) return Failure<FinanceAccountRecord>("account_duplicate");
        if (command.ParentAccountId is { } parentId && (parentId == command.Id || !await db.Accounts.AnyAsync(item => item.Id == parentId && item.CompanyId == command.CompanyId, cancellationToken))) return Failure<FinanceAccountRecord>("account_parent_scope_invalid");
        var entity = new FinanceAccountEntity(context.TenantId, command.Id, command with { Code = code });
        db.Accounts.Add(entity);
        var now = DateTimeOffset.UtcNow;
        AddAudit(db, context, "finance.account.create", "account", entity.Id, "Succeeded", null, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceAccountRecord>.Success(ToAccount(entity));
        AddReplay(db, context, "finance.account.create", command.IdempotencyKey, command.RequestFingerprint, "account", entity.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceAccountRecord>> EditAccountAsync(FinanceRequestContext context, FinanceAccountCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ExpectedVersion is null || !ValidCommand(command.CompanyId, command.Code, command.EnglishName, command.EffectiveFrom, command.EffectiveTo, out var code)) return Failure<FinanceAccountRecord>("invalid_account");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceAccountRecord>(db, context, "finance.account.edit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Accounts.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (entity is null) return Failure<FinanceAccountRecord>("concurrency_conflict");
        if (Company(context, entity.CompanyId) is null || entity.CompanyId != command.CompanyId) return Failure<FinanceAccountRecord>("company_scope_denied");
        if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceAccountRecord>("concurrency_conflict");
        if (await db.Accounts.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code && item.Id != command.Id, cancellationToken)) return Failure<FinanceAccountRecord>("account_duplicate");
        if (command.ParentAccountId is { } parentId)
        {
            if (parentId == command.Id || !await db.Accounts.AnyAsync(item => item.Id == parentId && item.CompanyId == command.CompanyId, cancellationToken)) return Failure<FinanceAccountRecord>("account_parent_scope_invalid");
            if (await HasParentCycleAsync(db, command.Id, parentId, command.CompanyId, cancellationToken)) return Failure<FinanceAccountRecord>("account_parent_cycle");
        }
        entity.Edit(command with { Code = code });
        var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.account.edit", "account", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceAccountRecord>.Success(ToAccount(entity)); AddReplay(db, context, "finance.account.edit", command.IdempotencyKey, command.RequestFingerprint, "account", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceAccountRecord>> SetAccountLifecycleAsync(FinanceRequestContext context, Guid accountId, Guid companyId, FinanceAccountLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceAccountRecord>(db, context, "finance.account.lifecycle", idempotencyKey, fingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Accounts.SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken); if (entity is null) return Failure<FinanceAccountRecord>("concurrency_conflict"); if (Company(context, entity.CompanyId) is null || entity.CompanyId != companyId) return Failure<FinanceAccountRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinanceAccountRecord>("concurrency_conflict");
        entity.SetLifecycle(lifecycle); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.account.lifecycle", "account", accountId, "Succeeded", lifecycle.ToString(), idempotencyKey, now); await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceAccountRecord>.Success(ToAccount(entity)); AddReplay(db, context, "finance.account.lifecycle", idempotencyKey, fingerprint, "account", accountId, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceFiscalCalendarRecord>> ListCalendarsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context); return (await db.Calendars.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Name).ToListAsync(cancellationToken)).Select(ToCalendar).ToArray();
    }

    public async Task<Guid?> ResolveCompanyIdAsync(FinanceRequestContext context, FinanceResourceType resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        if (resourceId == Guid.Empty) return null;
        if (resourceType == FinanceResourceType.InventoryHandoff)
            return await inventory.ResolveFinanceHandoffCompanyIdAsync(context.ToInventoryRequestContext(), resourceId, cancellationToken);

        await using var db = CreateContext(context);
        return resourceType switch
        {
            FinanceResourceType.Account => await db.Accounts.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.Calendar => await db.Calendars.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.FiscalYear => await db.FiscalYears.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.FiscalPeriod => await db.FiscalPeriods.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.CostCenter => await db.CostCenters.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.PostingRule => await db.PostingRules.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            FinanceResourceType.Journal => await db.Journals.AsNoTracking().Where(item => item.Id == resourceId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    public async Task<FinanceOperationResult<FinanceFiscalCalendarRecord>> CreateCalendarAsync(FinanceRequestContext context, FinanceFiscalCalendarCommand command, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); if (company is null || string.IsNullOrWhiteSpace(command.Name)) return Failure<FinanceFiscalCalendarRecord>("company_scope_denied");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceFiscalCalendarRecord>(db, context, "finance.calendar.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        if (await db.Calendars.AnyAsync(item => item.CompanyId == command.CompanyId && item.Lifecycle == FinanceCalendarLifecycle.Active, cancellationToken)) return Failure<FinanceFiscalCalendarRecord>("active_calendar_exists");
        var entity = new FinanceFiscalCalendarEntity(context.TenantId, command.Id, command, company.FunctionalCurrencyCode); db.Calendars.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.calendar.create", "calendar", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceFiscalCalendarRecord>.Success(ToCalendar(entity)); AddReplay(db, context, "finance.calendar.create", command.IdempotencyKey, command.RequestFingerprint, "calendar", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceFiscalYearRecord>> ListYearsAsync(FinanceRequestContext context, Guid calendarId, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); var calendarCompanyId = await db.Calendars.AsNoTracking().Where(item => item.Id == calendarId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken); if (calendarCompanyId is null || Company(context, calendarCompanyId.Value) is null) return []; return (await db.FiscalYears.AsNoTracking().Where(item => item.CalendarId == calendarId).OrderBy(item => item.YearNumber).ToListAsync(cancellationToken)).Select(ToYear).ToArray(); }

    public async Task<FinanceOperationResult<FinanceFiscalYearRecord>> CreateYearAsync(FinanceRequestContext context, FinanceFiscalYearCommand command, CancellationToken cancellationToken = default)
    {
        if (command.StartDate > command.EndDate || command.YearNumber is < 1900 or > 9999) return Failure<FinanceFiscalYearRecord>("invalid_fiscal_year");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceFiscalYearRecord>(db, context, "finance.year.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var calendar = await db.Calendars.SingleOrDefaultAsync(item => item.Id == command.CalendarId && item.Lifecycle == FinanceCalendarLifecycle.Active, cancellationToken); if (calendar is null) return Failure<FinanceFiscalYearRecord>("calendar_not_configured"); if (Company(context, calendar.CompanyId) is null) return Failure<FinanceFiscalYearRecord>("company_scope_denied");
        if (await db.FiscalYears.AnyAsync(item => item.CalendarId == command.CalendarId && (item.YearNumber == command.YearNumber || (item.StartDate <= command.EndDate && item.EndDate >= command.StartDate)), cancellationToken)) return Failure<FinanceFiscalYearRecord>("fiscal_year_overlap");
        var entity = new FinanceFiscalYearEntity(context.TenantId, command.Id, command, calendar.CompanyId); db.FiscalYears.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.year.create", "fiscal-year", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceFiscalYearRecord>.Success(ToYear(entity)); AddReplay(db, context, "finance.year.create", command.IdempotencyKey, command.RequestFingerprint, "fiscal-year", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceFiscalPeriodRecord>> ListPeriodsAsync(FinanceRequestContext context, Guid fiscalYearId, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); var yearCompanyId = await db.FiscalYears.AsNoTracking().Where(item => item.Id == fiscalYearId).Select(item => (Guid?)item.CompanyId).SingleOrDefaultAsync(cancellationToken); if (yearCompanyId is null || Company(context, yearCompanyId.Value) is null) return []; return (await db.FiscalPeriods.AsNoTracking().Where(item => item.FiscalYearId == fiscalYearId).OrderBy(item => item.Sequence).ToListAsync(cancellationToken)).Select(ToPeriod).ToArray(); }

    public async Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> CreatePeriodAsync(FinanceRequestContext context, FinanceFiscalPeriodCommand command, CancellationToken cancellationToken = default)
    {
        if (command.StartDate > command.EndDate || command.Sequence <= 0 || string.IsNullOrWhiteSpace(command.Code)) return Failure<FinanceFiscalPeriodRecord>("invalid_fiscal_period");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceFiscalPeriodRecord>(db, context, "finance.period.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var year = await db.FiscalYears.SingleOrDefaultAsync(item => item.Id == command.FiscalYearId, cancellationToken); if (year is null || command.StartDate < year.StartDate || command.EndDate > year.EndDate) return Failure<FinanceFiscalPeriodRecord>("fiscal_year_scope_invalid"); if (Company(context, year.CompanyId) is null) return Failure<FinanceFiscalPeriodRecord>("company_scope_denied");
        if (await db.FiscalPeriods.AnyAsync(item => item.FiscalYearId == command.FiscalYearId && (item.Sequence == command.Sequence || item.Code == command.Code.Trim() || (item.StartDate <= command.EndDate && item.EndDate >= command.StartDate)), cancellationToken)) return Failure<FinanceFiscalPeriodRecord>("fiscal_period_overlap");
        var entity = new FinanceFiscalPeriodEntity(context.TenantId, command.Id, command with { Code = command.Code.Trim() }, year.CompanyId); db.FiscalPeriods.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.period.create", "fiscal-period", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceFiscalPeriodRecord>.Success(ToPeriod(entity)); AddReplay(db, context, "finance.period.create", command.IdempotencyKey, command.RequestFingerprint, "fiscal-period", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceFiscalPeriodRecord>> SetPeriodStateAsync(FinanceRequestContext context, FinancePeriodStateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceFiscalPeriodRecord>(db, context, "finance.period.state", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.Id == command.PeriodId, cancellationToken); if (entity is null) return Failure<FinanceFiscalPeriodRecord>("concurrency_conflict"); if (Company(context, entity.CompanyId) is null) return Failure<FinanceFiscalPeriodRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceFiscalPeriodRecord>("concurrency_conflict");
        if (entity.State != command.State && (entity.State is FinanceFiscalPeriodState.SoftClosed or FinanceFiscalPeriodState.Closed || command.State is FinanceFiscalPeriodState.SoftClosed or FinanceFiscalPeriodState.Closed) && string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceFiscalPeriodRecord>("period_state_reason_required");
        if (entity.State == command.State) return FinanceOperationResult<FinanceFiscalPeriodRecord>.Success(ToPeriod(entity));
        entity.SetState(command.State); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.period.state", "fiscal-period", entity.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceFiscalPeriodRecord>.Success(ToPeriod(entity)); AddReplay(db, context, "finance.period.state", command.IdempotencyKey, command.RequestFingerprint, "fiscal-period", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceCostCenterRecord>> ListCostCentersAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = CreateContext(context); return (await db.CostCenters.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.Code).ToListAsync(cancellationToken)).Select(ToCostCenter).ToArray(); }

    public async Task<FinanceOperationResult<FinanceCostCenterRecord>> CreateCostCenterAsync(FinanceRequestContext context, FinanceCostCenterCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidCommand(command.CompanyId, command.Code, command.EnglishName, command.EffectiveFrom, command.EffectiveTo, out var code) || Company(context, command.CompanyId) is null) return Failure<FinanceCostCenterRecord>("invalid_cost_center");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceCostCenterRecord>(db, context, "finance.cost-center.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        if (await db.CostCenters.AnyAsync(item => item.CompanyId == command.CompanyId && item.Code == code, cancellationToken)) return Failure<FinanceCostCenterRecord>("cost_center_duplicate");
        var entity = new FinanceCostCenterEntity(context.TenantId, command.Id, command with { Code = code }); db.CostCenters.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.cost-center.create", "cost-center", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceCostCenterRecord>.Success(ToCostCenter(entity)); AddReplay(db, context, "finance.cost-center.create", command.IdempotencyKey, command.RequestFingerprint, "cost-center", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinancePostingRuleRecord>> ListPostingRulesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = CreateContext(context); return (await db.PostingRules.AsNoTracking().Where(item => item.CompanyId == companyId).OrderBy(item => item.SourceContract).ThenBy(item => item.SourceEvent).ThenBy(item => item.VersionNumber).ToListAsync(cancellationToken)).Select(ToRule).ToArray(); }

    public async Task<FinanceOperationResult<FinancePostingRuleRecord>> CreatePostingRuleAsync(FinanceRequestContext context, FinancePostingRuleCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SourceContract) || string.IsNullOrWhiteSpace(command.SourceEvent) || Company(context, command.CompanyId) is null || (command.EffectiveTo.HasValue && command.EffectiveTo.Value < command.EffectiveFrom)) return Failure<FinancePostingRuleRecord>("invalid_posting_rule");
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinancePostingRuleRecord>(db, context, "finance.posting-rule.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var debit = await db.Accounts.SingleOrDefaultAsync(item => item.Id == command.DebitAccountId && item.CompanyId == command.CompanyId, cancellationToken); var credit = await db.Accounts.SingleOrDefaultAsync(item => item.Id == command.CreditAccountId && item.CompanyId == command.CompanyId, cancellationToken);
        if (debit is null || credit is null || debit.Id == credit.Id || debit.Lifecycle != FinanceAccountLifecycle.Active || credit.Lifecycle != FinanceAccountLifecycle.Active || !debit.IsPostingAccount || !credit.IsPostingAccount) return Failure<FinancePostingRuleRecord>("posting_rule_account_invalid");
        var source = command.SourceContract.Trim(); var evt = command.SourceEvent.Trim(); var existing = await db.PostingRules.Where(item => item.CompanyId == command.CompanyId && item.SourceContract == source && item.SourceEvent == evt).ToListAsync(cancellationToken); if (existing.Any(item => item.EffectiveFrom <= (command.EffectiveTo ?? DateOnly.MaxValue) && (item.EffectiveTo ?? DateOnly.MaxValue) >= command.EffectiveFrom && item.Lifecycle == FinancePostingRuleLifecycle.Enabled)) return Failure<FinancePostingRuleRecord>("posting_rule_effective_overlap");
        var entity = new FinancePostingRuleEntity(context.TenantId, command.Id, command with { SourceContract = source, SourceEvent = evt }, existing.Select(item => item.VersionNumber).DefaultIfEmpty(0).Max() + 1, debit.Code, credit.Code); db.PostingRules.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.posting-rule.create", "posting-rule", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinancePostingRuleRecord>.Success(ToRule(entity)); AddReplay(db, context, "finance.posting-rule.create", command.IdempotencyKey, command.RequestFingerprint, "posting-rule", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinancePostingRuleRecord>> SetPostingRuleLifecycleAsync(FinanceRequestContext context, Guid ruleId, Guid companyId, FinancePostingRuleLifecycle lifecycle, byte[] expectedVersion, string idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinancePostingRuleRecord>(db, context, "finance.posting-rule.lifecycle", idempotencyKey, fingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.PostingRules.SingleOrDefaultAsync(item => item.Id == ruleId && item.CompanyId == companyId, cancellationToken);
        if (entity is null) return Failure<FinancePostingRuleRecord>("concurrency_conflict");
        if (Company(context, entity.CompanyId) is null) return Failure<FinancePostingRuleRecord>("company_scope_denied");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinancePostingRuleRecord>("concurrency_conflict");
        if (lifecycle == FinancePostingRuleLifecycle.Enabled)
        {
            var overlap = await db.PostingRules.AnyAsync(item => item.Id != ruleId && item.CompanyId == companyId && item.SourceContract == entity.SourceContract && item.SourceEvent == entity.SourceEvent && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= (entity.EffectiveTo ?? DateOnly.MaxValue) && (item.EffectiveTo ?? DateOnly.MaxValue) >= entity.EffectiveFrom, cancellationToken);
            if (overlap) return Failure<FinancePostingRuleRecord>("posting_rule_effective_overlap");
        }
        entity.SetLifecycle(lifecycle); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.posting-rule.lifecycle", "posting-rule", entity.Id, "Succeeded", lifecycle.ToString(), idempotencyKey, now); await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinancePostingRuleRecord>.Success(ToRule(entity)); AddReplay(db, context, "finance.posting-rule.lifecycle", idempotencyKey, fingerprint, "posting-rule", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceJournalRecord>> ListJournalsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = CreateContext(context); return (await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).OrderByDescending(item => item.JournalSequence).Take(500).ToListAsync(cancellationToken)).Select(ToJournal).ToArray(); }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> CreateJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateJournalInputAsync(context, command, cancellationToken); if (!validation.Succeeded) return Failure<FinanceJournalRecord>(validation.Code);
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        if (command.SourceEvidenceId is { } sourceId && await db.Journals.AnyAsync(item => item.SourceContract == command.SourceContract && item.SourceEvidenceId == sourceId && item.SourceEvidenceVersion == command.SourceEvidenceVersion, cancellationToken)) return Failure<FinanceJournalRecord>("source_effect_exists");
        var company = Company(context, command.CompanyId)!; var sequence = (await db.Journals.Where(item => item.CompanyId == command.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L; var entity = new FinanceJournalEntity(context.TenantId, command.Id, command with { TransactionCurrencyCode = Normalize(command.TransactionCurrencyCode), SourceContract = string.IsNullOrWhiteSpace(command.SourceContract) ? ManualContract : command.SourceContract.Trim(), SourceEvent = string.IsNullOrWhiteSpace(command.SourceEvent) ? "manual" : command.SourceEvent.Trim() }, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); entity.SetCorrelation(context.CorrelationId);
        var accountIds = command.Lines.Select(line => line.AccountId).ToArray(); var costCenterIds = command.Lines.Where(line => line.CostCenterId.HasValue).Select(line => line.CostCenterId!.Value).ToArray();
        var accounts = await db.Accounts.Where(item => item.CompanyId == command.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); var centers = await db.CostCenters.Where(item => item.CompanyId == command.CompanyId && costCenterIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var currency = Normalize(command.TransactionCurrencyCode) ?? company.FunctionalCurrencyCode;
        for (var index = 0; index < command.Lines.Count; index++) { var line = command.Lines[index] with { TransactionCurrencyCode = currency }; if (!accounts.TryGetValue(line.AccountId, out var account)) return Failure<FinanceJournalRecord>("account_not_found"); if (line.CostCenterId is { } costCenterId && (!centers.TryGetValue(costCenterId, out var center) || !DimensionIsEffective(center, command.PostingDate))) return Failure<FinanceJournalRecord>("dimension_invalid"); centers.TryGetValue(line.CostCenterId ?? Guid.Empty, out var selectedCenter); var functional = command.AmountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency ? (true, "valid", line.Debit, line.Credit) : FunctionalAmounts(line, company.FunctionalCurrencyCode, currency, command.ExchangeRate); if (!functional.Item1) return Failure<FinanceJournalRecord>(functional.Item2); entity.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, index + 1, account, line, selectedCenter, functional.Item3, functional.Item4, command.AmountAuthority)); }
        db.Journals.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.journal.create", "journal", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(entity)); AddReplay(db, context, "finance.journal.create", command.IdempotencyKey, command.RequestFingerprint, "journal", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> EditJournalAsync(FinanceRequestContext context, FinanceJournalCommand command, byte[] expectedVersion, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateJournalInputAsync(context, command, cancellationToken); if (!validation.Succeeded) return Failure<FinanceJournalRecord>(validation.Code);
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal.edit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null) return Failure<FinanceJournalRecord>("concurrency_conflict"); if (Company(context, entity.CompanyId) is null || entity.CompanyId != command.CompanyId) return Failure<FinanceJournalRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<FinanceJournalRecord>("concurrency_conflict"); if (entity.Status is not (FinanceJournalStatus.Draft or FinanceJournalStatus.Rejected)) return Failure<FinanceJournalRecord>("journal_immutable");
        var accountIds = command.Lines.Select(line => line.AccountId).ToArray(); var accounts = await db.Accounts.Where(item => item.CompanyId == command.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); var centers = await db.CostCenters.Where(item => item.CompanyId == command.CompanyId).ToDictionaryAsync(item => item.Id, cancellationToken); var lines = new List<FinanceJournalLineEntity>(); var company = Company(context, command.CompanyId)!; var amountAuthority = entity.AmountAuthority;
        var currency = Normalize(command.TransactionCurrencyCode) ?? company.FunctionalCurrencyCode;
        foreach (var (line, index) in command.Lines.Select((value, index) => (value with { TransactionCurrencyCode = currency }, index))) { if (!accounts.TryGetValue(line.AccountId, out var account)) return Failure<FinanceJournalRecord>("account_not_found"); if (line.CostCenterId is { } costCenterId && (!centers.TryGetValue(costCenterId, out var center) || !DimensionIsEffective(center, command.PostingDate))) return Failure<FinanceJournalRecord>("dimension_invalid"); centers.TryGetValue(line.CostCenterId ?? Guid.Empty, out var selectedCenter); var functional = amountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency ? (true, "valid", line.Debit, line.Credit) : FunctionalAmounts(line, company.FunctionalCurrencyCode, currency, command.ExchangeRate); if (!functional.Item1) return Failure<FinanceJournalRecord>(functional.Item2); lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, index + 1, account, line, selectedCenter, functional.Item3, functional.Item4, amountAuthority)); }
        entity.UpdateHeader(command); entity.ReplaceLines(lines); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.journal.edit", "journal", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(entity)); AddReplay(db, context, "finance.journal.edit", command.IdempotencyKey, command.RequestFingerprint, "journal", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> TransitionJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, FinanceJournalStatus target, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal." + target.ToString().ToLowerInvariant(), command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.JournalId, cancellationToken); if (entity is null) return Failure<FinanceJournalRecord>("concurrency_conflict"); if (Company(context, entity.CompanyId) is null) return Failure<FinanceJournalRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceJournalRecord>("concurrency_conflict");
        if (!AllowedTransition(entity.Status, target) || target == FinanceJournalStatus.Reversed || target == FinanceJournalStatus.Posted) return Failure<FinanceJournalRecord>("invalid_journal_transition"); if (target == FinanceJournalStatus.Rejected && string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceJournalRecord>("reason_required");
        if (target == FinanceJournalStatus.Approved && (entity.CreatedBy == context.ActorId || entity.SubmittedBy == context.ActorId)) return Failure<FinanceJournalRecord>("self_approval_forbidden");
        entity.SetStatus(target, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; var operation = "finance.journal." + target.ToString().ToLowerInvariant(); AddAudit(db, context, operation, "journal", entity.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(entity)); AddReplay(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, "journal", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> PostJournalAsync(FinanceRequestContext context, FinanceJournalActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var entity = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.JournalId, cancellationToken); if (entity is null) return Failure<FinanceJournalRecord>("concurrency_conflict"); if (Company(context, entity.CompanyId) is null) return Failure<FinanceJournalRecord>("company_scope_denied"); if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceJournalRecord>("concurrency_conflict");
        var validation = await ValidatePostingAsync(db, context, entity, cancellationToken); if (!validation.Succeeded) return Failure<FinanceJournalRecord>(validation.Code);
        var evidenceBuild = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(
            db,
            context.TenantContext,
            exchangeRates,
            entity.CompanyId,
            entity.PostingDate,
            entity.TransactionCurrencyCode ?? entity.FunctionalCurrencyCode,
            entity.Lines.Where(line => line.Debit > 0m).Sum(line => line.TransactionAmount ?? line.Debit),
            entity.FunctionalCurrencyCode,
            entity.Lines.Sum(line => line.FunctionalDebit),
            entity.ExchangeRate,
            entity.ExchangeRateId,
            entity.ExchangeRateVersionId,
            entity.ExchangeRateVersionNumber,
            cancellationToken);
        if (!evidenceBuild.Succeeded) return Failure<FinanceJournalRecord>(evidenceBuild.Code);
        var now = DateTimeOffset.UtcNow; entity.SetPeriod(validation.YearId, validation.PeriodId); if (validation.Rule is not null) entity.SetRule(validation.Rule.Id, validation.Rule.VersionNumber); entity.SetStatus(FinanceJournalStatus.Posted, context.ActorId, now); if (evidenceBuild.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), entity.Id, entity.CompanyId, null, evidenceBuild.Evidence, now)); if (entity.SourceEvidenceId is { } evidenceId && entity.SourceEvidenceVersion is { } evidenceVersion) db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), entity.CompanyId, entity.SourceContract, evidenceId, evidenceVersion, entity.Id, now)); AddAudit(db, context, "finance.journal.post", "journal", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(entity)); AddReplay(db, context, "finance.journal.post", command.IdempotencyKey, command.RequestFingerprint, "journal", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> ReverseJournalAsync(FinanceRequestContext context, FinanceReversalCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal.reverse", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var original = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.JournalId, cancellationToken); if (original is null || Company(context, original.CompanyId) is null) return Failure<FinanceJournalRecord>("company_scope_denied"); if (original.Status != FinanceJournalStatus.Posted) return Failure<FinanceJournalRecord>("journal_not_posted"); if (original.ReversalJournalId is not null) return Failure<FinanceJournalRecord>("journal_already_reversed"); if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceJournalRecord>("reason_required");
        var reversalCommand = new FinanceJournalCommand(original.CompanyId, original.JournalDate, command.PostingDate, original.TransactionCurrencyCode, original.ExchangeRate, original.ExchangeRateId, original.ExchangeRateVersionId, original.ExchangeRateVersionNumber, "finance-reversal.v1", "reversal", null, null, null, command.Reason, original.Lines.OrderBy(item => item.LineNumber).Select(item => new FinanceJournalLineCommand(item.AccountId, item.Credit, item.Debit, item.TransactionAmount, item.TransactionCurrencyCode, item.CostCenterId, item.Description)).ToArray(), command.Id, command.IdempotencyKey, command.RequestFingerprint, FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var company = Company(context, original.CompanyId)!; var sequence = (await db.Journals.Where(item => item.CompanyId == original.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L; var reversal = new FinanceJournalEntity(context.TenantId, Guid.NewGuid(), reversalCommand, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); reversal.SetCorrelation(context.CorrelationId); reversal.LinkOriginal(original.Id);
        foreach (var sourceLine in original.Lines.OrderBy(item => item.LineNumber)) { var account = await db.Accounts.SingleAsync(item => item.Id == sourceLine.AccountId, cancellationToken); FinanceCostCenterEntity? center = null; if (sourceLine.CostCenterId is { } centerId) center = await db.CostCenters.SingleOrDefaultAsync(item => item.Id == centerId, cancellationToken); var line = new FinanceJournalLineCommand(sourceLine.AccountId, sourceLine.Credit, sourceLine.Debit, sourceLine.TransactionAmount, sourceLine.TransactionCurrencyCode, sourceLine.CostCenterId, sourceLine.Description); reversal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), reversal.Id, sourceLine.LineNumber, account, line, center, sourceLine.FunctionalCredit, sourceLine.FunctionalDebit, FinanceJournalAmountAuthority.ManualTransactionCurrency)); }
        db.Journals.Add(reversal); var validation = await ValidatePostingAsync(db, context, reversal, cancellationToken); if (!validation.Succeeded) return Failure<FinanceJournalRecord>(validation.Code); var originalEvidence = await db.JournalMonetaryEvidence.AsNoTracking().SingleOrDefaultAsync(item => item.JournalId == original.Id, cancellationToken); FinanceMonetaryEvidence? reversalEvidence = null; if (originalEvidence is not null) { var parsed = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(originalEvidence.MonetaryEvidenceJson); if (parsed is null) return Failure<FinanceJournalRecord>("reporting_evidence_invalid"); reversalEvidence = FinanceJournalMonetaryEvidenceFactory.Negate(parsed); } else { var built = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, reversal.CompanyId, reversal.PostingDate, reversal.TransactionCurrencyCode ?? reversal.FunctionalCurrencyCode, reversal.Lines.Where(line => line.Debit > 0m).Sum(line => line.TransactionAmount ?? line.Debit), reversal.FunctionalCurrencyCode, reversal.Lines.Sum(line => line.FunctionalDebit), reversal.ExchangeRate, reversal.ExchangeRateId, reversal.ExchangeRateVersionId, reversal.ExchangeRateVersionNumber, cancellationToken); if (!built.Succeeded) return Failure<FinanceJournalRecord>(built.Code); reversalEvidence = built.Evidence; } reversal.SetPeriod(validation.YearId, validation.PeriodId); reversal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); if (reversalEvidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), reversal.Id, reversal.CompanyId, null, reversalEvidence, DateTimeOffset.UtcNow)); original.LinkReversal(reversal.Id); original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.journal.reverse", "journal", original.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(reversal)); AddReplay(db, context, "finance.journal.reverse", command.IdempotencyKey, command.RequestFingerprint, "journal", reversal.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceGlLineRecord>> QueryGlAsync(FinanceRequestContext context, FinanceGlQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is null) return [];
        await using var db = CreateContext(context); var journals = db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == query.CompanyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed)); if (query.FiscalPeriodId is { } period) journals = journals.Where(item => item.FiscalPeriodId == period); if (query.From is { } from) journals = journals.Where(item => item.PostingDate >= from); if (query.To is { } to) journals = journals.Where(item => item.PostingDate <= to); if (!string.IsNullOrWhiteSpace(query.SourceContract)) journals = journals.Where(item => item.SourceContract == query.SourceContract); var values = await journals.OrderBy(item => item.PostingDate).ThenBy(item => item.JournalSequence).Take(5000).ToListAsync(cancellationToken); return values.SelectMany(journal => journal.Lines.Where(line => !query.AccountId.HasValue || line.AccountId == query.AccountId.Value).Where(line => !query.CostCenterId.HasValue || line.CostCenterId == query.CostCenterId.Value).Select(line => new FinanceGlLineRecord(journal.Id, journal.JournalNumber, journal.PostingDate, journal.FunctionalCurrencyCode, line.AccountId, line.AccountCode, line.AccountName, journal.FiscalPeriodId, line.CostCenterId, line.CostCenterCode, line.Debit, line.Credit, line.FunctionalDebit, line.FunctionalCredit, journal.SourceContract, journal.SourceEvidenceId, journal.Status == FinanceJournalStatus.Reversed || journal.ReversalOfJournalId is not null))).ToArray();
    }

    public async Task<IReadOnlyList<FinanceHandoffRecord>> ListHandoffsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        var inventoryContext = context.ToInventoryRequestContext(); var handoffs = await inventory.ListFinanceHandoffsAsync(inventoryContext, new InventoryValuationQuery(companyId), cancellationToken); await using var db = CreateContext(context); var effects = await db.SourceEffects.AsNoTracking().Where(item => item.CompanyId == companyId && item.SourceContract == InventoryContract).ToListAsync(cancellationToken); var journals = await db.Journals.AsNoTracking().Where(item => item.CompanyId == companyId && item.SourceContract == InventoryContract).ToListAsync(cancellationToken); var rules = await db.PostingRules.AsNoTracking().Where(item => item.CompanyId == companyId && item.SourceContract == InventoryContract && item.Lifecycle == FinancePostingRuleLifecycle.Enabled).ToListAsync(cancellationToken); return handoffs.Select(item => { var effect = effects.SingleOrDefault(value => value.SourceEvidenceId == item.ValuationEvidenceId && value.SourceEvidenceVersion == item.ValuationEvidenceVersion); var journal = journals.SingleOrDefault(value => value.SourceEvidenceId == item.ValuationEvidenceId && value.SourceEvidenceVersion == item.ValuationEvidenceVersion); var eventName = FinanceInventoryPostingClassifier.Classify(item.SourceType, item.Direction); var matchingRules = rules.Where(rule => rule.SourceEvent == eventName && rule.EffectiveFrom <= DateOnly.FromDateTime(item.AsOf.UtcDateTime) && (rule.EffectiveTo == null || rule.EffectiveTo >= DateOnly.FromDateTime(item.AsOf.UtcDateTime))).ToArray(); var status = effect is not null ? FinanceSourceHandoffStatus.Posted : journal?.Status == FinanceJournalStatus.Submitted ? FinanceSourceHandoffStatus.PendingApproval : matchingRules.Length == 0 ? FinanceSourceHandoffStatus.PendingMapping : matchingRules.Length > 1 ? FinanceSourceHandoffStatus.Blocked : FinanceSourceHandoffStatus.Ready; return ToHandoff(item, effect, journals, status); }).ToArray();
    }

    public async Task<FinanceInventoryOpeningPreflightResult> PreflightInventoryOpeningAsync(
        FinanceRequestContext context,
        Guid companyId,
        DateOnly postingDate,
        CancellationToken cancellationToken = default)
    {
        static FinanceInventoryOpeningPreflightResult Block(string code, string currency = "") =>
            new(false, code, currency, FinanceApprovalRequirement.NotConfigured);

        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(context.FoundationContext))
            return Block("forbidden");
        var company = Company(context, companyId);
        if (company is null || string.IsNullOrWhiteSpace(company.FunctionalCurrencyCode))
            return Block("company_scope_denied");

        await using var db = CreateContext(context);
        var periods = await db.FiscalPeriods.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.StartDate <= postingDate && item.EndDate >= postingDate)
            .ToListAsync(cancellationToken);
        if (periods.Count == 0) return Block("period_not_configured", company.FunctionalCurrencyCode);
        if (periods.Count != 1) return Block("period_ambiguous", company.FunctionalCurrencyCode);
        if (periods[0].State != FinanceFiscalPeriodState.Open)
            return Block(periods[0].State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", company.FunctionalCurrencyCode);

        var eventName = FinanceInventoryPostingClassifier.Classify(InventoryMovementSourceType.OpeningBalance, InventoryMovementDirection.Inbound);
        var rules = await db.PostingRules.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.SourceContract == InventoryContract
                && item.SourceEvent == eventName
                && item.Lifecycle == FinancePostingRuleLifecycle.Enabled
                && item.EffectiveFrom <= postingDate
                && (item.EffectiveTo == null || item.EffectiveTo >= postingDate))
            .ToListAsync(cancellationToken);
        if (rules.Count == 0) return Block("pending_mapping", company.FunctionalCurrencyCode);
        if (rules.Count != 1) return Block("ambiguous_mapping", company.FunctionalCurrencyCode);

        var rule = rules[0];
        if (rule.DebitAccountId == rule.CreditAccountId) return Block("posting_rule_accounts_invalid", company.FunctionalCurrencyCode);
        var accountIds = new[] { rule.DebitAccountId, rule.CreditAccountId }.Distinct().ToArray();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(item => item.CompanyId == companyId && accountIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (accounts.Count != accountIds.Length
            || accounts.Any(item => !item.IsPostingAccount
                || item.Lifecycle != FinanceAccountLifecycle.Active
                || item.EffectiveFrom > postingDate
                || item.EffectiveTo < postingDate))
            return Block("account_not_postable", company.FunctionalCurrencyCode);
        if (rule.CostCenterRequired) return Block("dimension_required", company.FunctionalCurrencyCode);

        var approval = SourceApprovalPolicy.Resolve(InventoryContract, eventName);
        if (approval == FinanceApprovalRequirement.NotConfigured)
            return Block("approval_policy_not_configured", company.FunctionalCurrencyCode);
        return new(true, approval == FinanceApprovalRequirement.Required ? "approval_required" : "ready", company.FunctionalCurrencyCode, approval);
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> ProcessHandoffAsync(FinanceRequestContext context, FinanceHandoffProcessCommand command, CancellationToken cancellationToken = default)
    {
        var inventoryContext = context.ToInventoryRequestContext(); var handoffCompanyId = await inventory.ResolveFinanceHandoffCompanyIdAsync(inventoryContext, command.HandoffId, cancellationToken); if (handoffCompanyId is null || Company(context, handoffCompanyId.Value) is null) return Failure<FinanceJournalRecord>("company_scope_denied"); var handoff = (await inventory.ListFinanceHandoffsAsync(inventoryContext, new InventoryValuationQuery(handoffCompanyId.Value), cancellationToken)).SingleOrDefault(item => item.Id == command.HandoffId); if (handoff is null) return Failure<FinanceJournalRecord>("handoff_not_found"); if (handoff.Status != InventoryFinanceValuationHandoffStatus.ReadyForFinance) return Failure<FinanceJournalRecord>("handoff_not_ready");
        await using var db = CreateContext(context); var existing = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SourceContract == InventoryContract && item.SourceEvidenceId == handoff.ValuationEvidenceId && item.SourceEvidenceVersion == handoff.ValuationEvidenceVersion, cancellationToken); if (existing is not null && existing.Status == FinanceJournalStatus.Posted) return FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(existing));
        var eventName = FinanceInventoryPostingClassifier.Classify(handoff.SourceType, handoff.Direction); var rules = await db.PostingRules.Where(item => item.CompanyId == handoff.CompanyId && item.SourceContract == InventoryContract && item.SourceEvent == eventName && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= DateOnly.FromDateTime(handoff.AsOf.UtcDateTime) && (item.EffectiveTo == null || item.EffectiveTo >= DateOnly.FromDateTime(handoff.AsOf.UtcDateTime))).ToListAsync(cancellationToken); if (rules.Count == 0) return Failure<FinanceJournalRecord>("pending_mapping"); if (rules.Count > 1) return Failure<FinanceJournalRecord>("ambiguous_mapping"); var rule = rules[0];
        var approval = SourceApprovalPolicy.Resolve(InventoryContract, eventName); if (approval == FinanceApprovalRequirement.NotConfigured) return Failure<FinanceJournalRecord>("approval_policy_not_configured");
        var amount = Math.Abs(handoff.SignedBaseAmount); var commandId = existing?.Id ?? Guid.NewGuid(); var transactionCurrency = Normalize(handoff.TransactionCurrencyCode); var sameCurrency = transactionCurrency is null || string.Equals(transactionCurrency, handoff.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase); var journalCommand = new FinanceJournalCommand(handoff.CompanyId, DateOnly.FromDateTime(handoff.AsOf.UtcDateTime), DateOnly.FromDateTime(handoff.AsOf.UtcDateTime), transactionCurrency, sameCurrency ? 1m : handoff.ExchangeRate, sameCurrency ? null : handoff.ExchangeRateId, sameCurrency ? null : handoff.ExchangeRateVersionId, sameCurrency ? null : handoff.ExchangeRateVersionNumber, InventoryContract, eventName, handoff.ValuationEvidenceId, handoff.ValuationEvidenceVersion, rule.Id, $"Inventory valuation {handoff.LedgerSequence}", [new FinanceJournalLineCommand(rule.DebitAccountId, amount, 0m, null, transactionCurrency, null, "Inventory valuation debit"), new FinanceJournalLineCommand(rule.CreditAccountId, 0m, amount, null, transactionCurrency, null, "Inventory valuation credit")], commandId, command.IdempotencyKey + ":create", command.RequestFingerprint, FinanceJournalAmountAuthority.SourceFunctionalCurrency, approval);
        var created = existing is null ? await CreateJournalAsync(context, journalCommand, cancellationToken) : FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(existing)); if (!created.Succeeded || created.Value is null) return created; var current = created.Value; if (approval == FinanceApprovalRequirement.Required) { if (current.Status == FinanceJournalStatus.Draft) { var submitted = await TransitionJournalAsync(context, new FinanceJournalActionCommand(current.Id, current.Version, "inventory handoff submission", command.IdempotencyKey + ":submit", command.RequestFingerprint), FinanceJournalStatus.Submitted, cancellationToken); if (!submitted.Succeeded || submitted.Value is null) return submitted; current = submitted.Value; } return new FinanceOperationResult<FinanceJournalRecord>(true, "pending_approval", current); } if (current.Status == FinanceJournalStatus.Draft) return await PostJournalAsync(context, new FinanceJournalActionCommand(current.Id, current.Version, "inventory handoff post", command.IdempotencyKey + ":post", command.RequestFingerprint), cancellationToken); return FinanceOperationResult<FinanceJournalRecord>.Success(current);
    }

    private async Task<(bool Succeeded, string Code)> ValidateJournalInputAsync(FinanceRequestContext context, FinanceJournalCommand command, CancellationToken cancellationToken)
    {
        var company = Company(context, command.CompanyId); if (company is null) return (false, "company_scope_denied"); if (command.AmountAuthority == FinanceJournalAmountAuthority.SourceFunctionalCurrency && (!string.Equals(command.SourceContract, InventoryContract, StringComparison.OrdinalIgnoreCase) || command.SourceEvidenceId is null)) return (false, "source_amount_authority_invalid"); if (command.Lines is null || command.Lines.Count < 2) return (false, "journal_requires_two_lines"); if (command.Lines.Any(line => line.Debit < 0m || line.Credit < 0m || (line.Debit == 0m && line.Credit == 0m) || (line.Debit > 0m && line.Credit > 0m))) return (false, "journal_line_side_invalid"); if (command.Lines.Sum(line => line.Debit) == 0m && command.Lines.Sum(line => line.Credit) == 0m) return (false, "journal_zero_amount"); if (command.AmountAuthority == FinanceJournalAmountAuthority.ManualTransactionCurrency && command.Lines.Any(line => line.TransactionAmount.HasValue && line.TransactionAmount.Value != Math.Max(line.Debit, line.Credit))) return (false, "transaction_amount_mismatch");
        var currency = Normalize(command.TransactionCurrencyCode) ?? company.FunctionalCurrencyCode; if (command.Lines.Any(line => line.TransactionCurrencyCode is not null && Normalize(line.TransactionCurrencyCode) != currency)) return (false, "transaction_currency_mismatch"); if (command.SourceEvidenceId.HasValue != command.SourceEvidenceVersion.HasValue || command.SourceEvidenceVersion is <= 0) return (false, "source_evidence_invalid"); if (currency == company.FunctionalCurrencyCode) { if (command.ExchangeRate != 1m || command.ExchangeRateId is not null || command.ExchangeRateVersionId is not null || command.ExchangeRateVersionNumber is not null) return (false, "functional_currency_rate_must_be_explicit_one"); } else if (command.ExchangeRate is not > 0m || command.ExchangeRateId is null || command.ExchangeRateId == Guid.Empty || command.ExchangeRateVersionId is null || command.ExchangeRateVersionId == Guid.Empty || command.ExchangeRateVersionNumber is not > 0) return (false, "exact_exchange_rate_evidence_required");
        if (currency != company.FunctionalCurrencyCode)
        {
            var exchangeRate = await exchangeRates.FindExchangeRateAsync(context.TenantContext, command.ExchangeRateId!.Value, cancellationToken);
            var version = exchangeRate?.Versions.SingleOrDefault(item => item.Id == command.ExchangeRateVersionId!.Value && item.VersionNumber == command.ExchangeRateVersionNumber!.Value);
            if (exchangeRate is null
                || exchangeRate.LifecycleState != MasterDataLifecycleState.Active
                || version is null
                || !string.Equals(exchangeRate.SourceCurrencyCode, currency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(exchangeRate.TargetCurrencyCode, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
                || version.Rate != command.ExchangeRate
                || version.EffectiveFrom > command.PostingDate
                || (version.EffectiveTo is not null && version.EffectiveTo.Value < command.PostingDate))
            {
                return (false, "exchange_rate_evidence_mismatch");
            }
        }
        if (command.EffectiveDateInvalid()) return (false, "invalid_journal_date");
        return (true, "valid");
    }

    private async Task<PostingValidation> ValidatePostingAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity entity, CancellationToken cancellationToken)
    {
        if (entity.ApprovalRequirement == FinanceApprovalRequirement.Required && entity.Status != FinanceJournalStatus.Approved) return PostingValidation.Failure("journal_must_be_approved"); if (entity.ApprovalRequirement == FinanceApprovalRequirement.NotConfigured) return PostingValidation.Failure("approval_policy_not_configured"); if (entity.ApprovalRequirement == FinanceApprovalRequirement.NotRequired && entity.Status is not (FinanceJournalStatus.Draft or FinanceJournalStatus.Submitted)) return PostingValidation.Failure("invalid_journal_transition"); var period = await db.FiscalPeriods.Where(item => item.CompanyId == entity.CompanyId && item.StartDate <= entity.PostingDate && item.EndDate >= entity.PostingDate).SingleOrDefaultAsync(cancellationToken); if (period is null) return PostingValidation.Failure("period_not_configured"); if (period.State != FinanceFiscalPeriodState.Open) return PostingValidation.Failure(period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed");
        var accountIds = entity.Lines.Select(line => line.AccountId).Distinct().ToArray(); var accounts = await db.Accounts.Where(item => item.CompanyId == entity.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); if (accounts.Count != accountIds.Length || entity.Lines.Any(line => !accounts[line.AccountId].IsPostingAccount || accounts[line.AccountId].Lifecycle != FinanceAccountLifecycle.Active || accounts[line.AccountId].EffectiveFrom > entity.PostingDate || accounts[line.AccountId].EffectiveTo < entity.PostingDate)) return PostingValidation.Failure("account_not_postable"); if (entity.AmountAuthority == FinanceJournalAmountAuthority.ManualTransactionCurrency && Normalize(entity.TransactionCurrencyCode) != Normalize(entity.FunctionalCurrencyCode) && accounts.Values.Any(item => item.CurrencyBehavior == FinanceCurrencyBehavior.FunctionalOnly)) return PostingValidation.Failure("account_currency_behavior_invalid"); var centerIds = entity.Lines.Where(line => line.CostCenterId.HasValue).Select(line => line.CostCenterId!.Value).Distinct().ToArray(); var centers = await db.CostCenters.Where(item => item.CompanyId == entity.CompanyId && centerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); if (centers.Count != centerIds.Length || centers.Values.Any(center => !DimensionIsEffective(center, entity.PostingDate))) return PostingValidation.Failure("dimension_invalid"); var debit = entity.Lines.Sum(item => item.FunctionalDebit); var credit = entity.Lines.Sum(item => item.FunctionalCredit); if (debit != credit || debit <= 0m) return PostingValidation.Failure("journal_not_balanced");
        FinancePostingRuleEntity? rule = null; if (entity.ReversalOfJournalId is null && (!string.Equals(entity.SourceContract, ManualContract, StringComparison.OrdinalIgnoreCase) || entity.PostingRuleId is not null)) { var rules = await db.PostingRules.Where(item => item.CompanyId == entity.CompanyId && (entity.PostingRuleId == null || item.Id == entity.PostingRuleId) && item.SourceContract == entity.SourceContract && item.SourceEvent == entity.SourceEvent && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= entity.PostingDate && (item.EffectiveTo == null || item.EffectiveTo >= entity.PostingDate)).ToListAsync(cancellationToken); if (rules.Count != 1) return PostingValidation.Failure(rules.Count == 0 ? "pending_mapping" : "ambiguous_mapping"); rule = rules[0]; if (rule.DebitAccountId != entity.Lines.FirstOrDefault(item => item.FunctionalDebit > 0m)?.AccountId || rule.CreditAccountId != entity.Lines.FirstOrDefault(item => item.FunctionalCredit > 0m)?.AccountId) return PostingValidation.Failure("posting_rule_account_mismatch"); if (rule.CostCenterRequired && entity.Lines.Any(line => line.CostCenterId is null)) return PostingValidation.Failure("dimension_required"); }
        if (entity.SourceEvidenceId is { } evidenceId && entity.SourceEvidenceVersion is { } evidenceVersion && await db.SourceEffects.AnyAsync(item => item.CompanyId == entity.CompanyId && item.SourceContract == entity.SourceContract && item.SourceEvidenceId == evidenceId && item.SourceEvidenceVersion == evidenceVersion, cancellationToken)) return PostingValidation.Failure("source_effect_exists"); return PostingValidation.Valid(period.FiscalYearId, period.Id, rule);
    }

    private static bool AllowedTransition(FinanceJournalStatus from, FinanceJournalStatus to) => (from, to) switch { (FinanceJournalStatus.Draft, FinanceJournalStatus.Submitted or FinanceJournalStatus.Cancelled) => true, (FinanceJournalStatus.Submitted, FinanceJournalStatus.Approved or FinanceJournalStatus.Rejected or FinanceJournalStatus.Cancelled) => true, (FinanceJournalStatus.Approved, FinanceJournalStatus.Cancelled) => true, (FinanceJournalStatus.Rejected, FinanceJournalStatus.Draft or FinanceJournalStatus.Cancelled) => true, _ => false };
    private FinanceCompanyOption? Company(FinanceRequestContext context, Guid companyId)
    {
        var matches = companies.List(context.TenantId)
            .Where(item => item.CompanyId == companyId && item.IsActive)
            .ToArray();
        if (matches.Length == 0 || matches.Select(item => item.FunctionalCurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
            return null;
        if (context.TenantContext.Scope is { } scope)
        {
            var value = scope.Value;
            if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase)
                && (!Guid.TryParse(value["Company:".Length..], out var scopedCompany) || scopedCompany != companyId))
                return null;
            if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase)
                && (!Guid.TryParse(value["Branch:".Length..], out var branchId) || !matches.Any(item => item.BranchId == branchId)))
                return null;
        }
        return matches.OrderBy(item => item.BranchId.HasValue).ThenBy(item => item.BranchId).First();
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool DimensionIsEffective(FinanceCostCenterEntity center, DateOnly date) => center.Lifecycle == FinanceAccountLifecycle.Active && center.EffectiveFrom <= date && (center.EffectiveTo is null || center.EffectiveTo >= date);
    private static bool ValidCommand(Guid companyId, string code, string name, DateOnly from, DateOnly? to, out string normalized) { normalized = Normalize(code) ?? string.Empty; return companyId != Guid.Empty && normalized.Length > 0 && normalized.Length <= 64 && !string.IsNullOrWhiteSpace(name) && (!to.HasValue || to.Value >= from); }
    private static (bool Valid, string Code, decimal Debit, decimal Credit) FunctionalAmounts(FinanceJournalLineCommand line, string functional, string? transaction, decimal? rate) { if (Normalize(transaction) == functional) return (true, "valid", line.Debit, line.Credit); if (rate is not > 0m) return (false, "exact_exchange_rate_evidence_required", 0m, 0m); return (true, "valid", decimal.Round(line.Debit * rate.Value, 8, MidpointRounding.ToEven), decimal.Round(line.Credit * rate.Value, 8, MidpointRounding.ToEven)); }
    private static FinanceOperationResult<T> Failure<T>(string code) => FinanceOperationResult<T>.Failure(code);
    private static async Task<bool> HasParentCycleAsync(FinanceDbContext db, Guid accountId, Guid parentId, Guid companyId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var currentId = (Guid?)parentId;
        while (currentId is { } current)
        {
            if (!visited.Add(current)) return true;
            if (current == accountId) return true;
            var parent = await db.Accounts.AsNoTracking().Where(item => item.Id == current && item.CompanyId == companyId).Select(item => new { item.ParentAccountId }).SingleOrDefaultAsync(cancellationToken);
            if (parent is null) return false;
            currentId = parent.ParentAccountId;
        }
        return false;
    }
    private static void AddAudit(FinanceDbContext db, FinanceRequestContext context, string operation, string resource, Guid id, string result, string? reason, string? key, DateTimeOffset at) => db.AuditEvents.Add(new FinanceAuditEntity(context.TenantId, Guid.NewGuid(), operation, resource, id, context.ActorId, context.SessionId, result, reason, context.CorrelationId, key, at));
    private static void AddReplay<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, string resource, Guid id, T value, DateTimeOffset at) { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key, fingerprint, resource, id, JsonSerializer.Serialize(value), at)); }
    private static async Task<FinanceOperationResult<T>?> ReadReplayAsync<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken) { if (string.IsNullOrWhiteSpace(key)) return null; var item = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(value => value.ActorId == context.ActorId && value.OperationId == operation && value.Key == key, cancellationToken); if (item is null) return null; if (!string.Equals(item.Fingerprint, fingerprint, StringComparison.Ordinal)) return Failure<T>("idempotency_conflict"); var value = JsonSerializer.Deserialize<T>(item.SnapshotJson); return value is null ? Failure<T>("idempotency_snapshot_invalid") : FinanceOperationResult<T>.Success(value); }

    private sealed record PostingValidation(bool Succeeded, string Code, Guid YearId, Guid PeriodId, FinancePostingRuleEntity? Rule) { internal static PostingValidation Failure(string code) => new(false, code, Guid.Empty, Guid.Empty, null); internal static PostingValidation Valid(Guid year, Guid period, FinancePostingRuleEntity? rule) => new(true, "valid", year, period, rule); }

    private static FinanceAccountRecord ToAccount(FinanceAccountEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.Code, item.EnglishName, item.ArabicName, item.ParentAccountId, item.AccountType, item.IsPostingAccount, item.Lifecycle, item.CurrencyBehavior, item.EffectiveFrom, item.EffectiveTo, item.Version);
    private static FinanceFiscalCalendarRecord ToCalendar(FinanceFiscalCalendarEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.Name, item.FunctionalCurrencyCode, item.Lifecycle, item.Version);
    private static FinanceFiscalYearRecord ToYear(FinanceFiscalYearEntity item) => new(item.Id, item.CalendarId, item.TenantId.Value, item.CompanyId, item.YearNumber, item.StartDate, item.EndDate, item.State, item.Version);
    private static FinanceFiscalPeriodRecord ToPeriod(FinanceFiscalPeriodEntity item) => new(item.Id, item.FiscalYearId, item.TenantId.Value, item.CompanyId, item.Sequence, item.Code, item.EnglishName, item.ArabicName, item.StartDate, item.EndDate, item.State, item.Version);
    private static FinanceCostCenterRecord ToCostCenter(FinanceCostCenterEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.Code, item.EnglishName, item.ArabicName, item.Lifecycle, item.EffectiveFrom, item.EffectiveTo, item.Version);
    private static FinancePostingRuleRecord ToRule(FinancePostingRuleEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.SourceContract, item.SourceEvent, item.VersionNumber, item.DebitAccountId, item.DebitAccountCode, item.CreditAccountId, item.CreditAccountCode, item.CostCenterRequired, item.EffectiveFrom, item.EffectiveTo, item.Lifecycle, item.Version);
    private static FinanceJournalRecord ToJournal(FinanceJournalEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.JournalSequence, item.JournalNumber, item.JournalDate, item.PostingDate, item.FiscalYearId, item.FiscalPeriodId, item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.ExchangeRate, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.SourceContract, item.SourceEvent, item.SourceEvidenceId, item.SourceEvidenceVersion, item.PostingRuleId, item.PostingRuleVersionNumber, item.Description, item.Status, item.CreatedBy, item.SubmittedBy, item.ApprovedBy, item.PostedBy, item.ReversedBy, item.ReversalOfJournalId, item.ReversalJournalId, item.CorrelationId, item.CreatedAt, item.PostedAt, item.Lines.OrderBy(line => line.LineNumber).Select(ToLine).ToArray(), item.Version, item.AmountAuthority, item.ApprovalRequirement);
    private static FinanceJournalLineRecord ToLine(FinanceJournalLineEntity item) => new(item.Id, item.LineNumber, item.AccountId, item.AccountCode, item.AccountName, item.Debit, item.Credit, item.FunctionalDebit, item.FunctionalCredit, item.TransactionAmount, item.TransactionCurrencyCode, item.CostCenterId, item.CostCenterCode, item.Description);
    private static FinanceHandoffRecord ToHandoff(InventoryFinanceValuationHandoffRecord item, FinanceSourceEffectEntity? effect, IReadOnlyList<FinanceJournalEntity> journals, FinanceSourceHandoffStatus status) { var journal = effect is null ? journals.SingleOrDefault(value => value.SourceEvidenceId == item.ValuationEvidenceId && value.SourceEvidenceVersion == item.ValuationEvidenceVersion) : journals.SingleOrDefault(value => value.Id == effect.JournalId); return new(item.Id, item.TenantId, item.CompanyId, item.MovementId, item.LedgerSequence, item.SourceType.ToString(), item.SourceDocumentId, item.SourceLineId, item.ValuationEvidenceId, item.ValuationEvidenceVersion, item.Quantity, item.Direction.ToString(), item.BaseUnitCost, item.BaseAmount, item.SignedBaseAmount, item.RoundingAdjustmentAmount, item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.PolicyId, item.PolicyVersionNumber, item.CorrectionOfMovementId, status, journal?.Id, item.ContractVersion, item.CorrelationId, item.AsOf, item.Version); }
}

file static class FinanceJournalCommandExtensions
{
    internal static bool EffectiveDateInvalid(this FinanceJournalCommand command) => command.JournalDate == default || command.PostingDate == default;
}

#pragma warning restore CS1591
