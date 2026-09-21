#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed partial class FinanceSettlementPersistence
{
    private const string MigrationGlContract = "migration-gl-opening.v1";
    private const string MigrationGlEvent = "recognition";
    private const decimal MigrationGlTolerance = 0.00000001m;

    public async Task<FinanceGlOpeningPreflightResult> PreflightMigrationGlOpeningAsync(FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        return (await ValidateMigrationGlOpeningAsync(db, context, command, cancellationToken)).Result;
    }

    public async Task<FinanceMigrationGlOpeningEvidence?> ReadMigrationGlOpeningAsync(FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, CancellationToken cancellationToken = default)
    {
        if (Company(context, command.CompanyId) is null) return null;
        await using var db = CreateContext(context);
        var validation = await ValidateMigrationGlOpeningAsync(db, context, command, cancellationToken);
        var evidence = await ReadMigrationGlOpeningEvidenceAsync(db, context, command, validation.Result.ResidualLines, cancellationToken);
        return evidence ?? (validation.Result.Ready && validation.Result.NonEffect
            ? new FinanceMigrationGlOpeningEvidence(null, null, validation.Result.SourceEvidenceId, validation.Result.ResidualLines)
            : null);
    }

    public async Task<FinanceOperationResult<FinanceMigrationGlOpeningEvidence>> CreateMigrationGlOpeningAsync(FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = CreateContext(context);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var validation = await ValidateMigrationGlOpeningAsync(db, context, command, cancellationToken);
            if (!validation.Result.Ready) return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure(validation.Result.Code);
            var existing = await ReadMigrationGlOpeningEvidenceAsync(db, context, command, validation.Result.ResidualLines, cancellationToken);
            if (existing is not null) return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Success(existing);
            if (validation.Result.NonEffect)
            {
                var empty = new FinanceMigrationGlOpeningEvidence(null, null, validation.Result.SourceEvidenceId, validation.Result.ResidualLines);
                await tx.CommitAsync(cancellationToken);
                return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Success(empty);
            }

            var company = Company(context, command.CompanyId)!;
            var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.StartDate <= command.OpeningDate && item.EndDate >= command.OpeningDate && item.State == FinanceFiscalPeriodState.Open, cancellationToken);
            if (period is null) return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure("period_not_open");
            var accountIds = validation.Result.ResidualLines.Select(item => item.AccountId).Distinct().ToArray();
            var accounts = await db.Accounts.Where(item => item.CompanyId == command.CompanyId && accountIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (accounts.Count != accountIds.Length || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active || item.EffectiveFrom > command.OpeningDate || item.EffectiveTo < command.OpeningDate))
                return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure("account_not_postable");

            var journalId = StableId($"migration-gl-journal:{context.TenantId.Value:D}:{command.CompanyId:D}:{command.CurrencyCode.Trim().ToUpperInvariant()}:{command.OpeningDate:yyyy-MM-dd}");
            var sequence = (await db.Journals.Where(item => item.CompanyId == command.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L;
            while (db.ChangeTracker.Entries<FinanceJournalEntity>().Any(item => item.State != EntityState.Deleted && item.Entity.CompanyId == command.CompanyId && item.Entity.JournalSequence == sequence)) sequence++;
            var lines = validation.Result.ResidualLines.Select(item =>
                new FinanceJournalLineCommand(item.AccountId, item.Debit, item.Credit, Math.Max(item.Debit, item.Credit), company.FunctionalCurrencyCode, null, $"GL opening {LineReference(command, item)}")).ToArray();
            var journalCommand = new FinanceJournalCommand(command.CompanyId, command.OpeningDate, command.OpeningDate, company.FunctionalCurrencyCode, 1m, null, null, null, MigrationGlContract, MigrationGlEvent, validation.Result.SourceEvidenceId, 1, null, "Migration residual GL opening", lines, journalId, command.IdempotencyKey, command.RequestFingerprint, FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
            var journal = new FinanceJournalEntity(context.TenantId, journalId, journalCommand, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow);
            journal.SetCorrelation(DurableFingerprint(command));
            journal.SetPeriod(period.FiscalYearId, period.Id);
            journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
            var number = 1;
            foreach (var line in validation.Result.ResidualLines)
            {
                var commandLine = lines[number - 1];
                journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journalId, number++, accounts[line.AccountId], commandLine, null, line.Debit, line.Credit, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            }
            var evidence = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, command.CompanyId, command.OpeningDate, company.FunctionalCurrencyCode, validation.Result.ResidualLines.Sum(item => item.Debit), company.FunctionalCurrencyCode, validation.Result.ResidualLines.Sum(item => item.Debit), 1m, null, null, null, cancellationToken);
            if (!evidence.Succeeded) return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure(evidence.Code);
            var now = DateTimeOffset.UtcNow;
            var sourceEffectId = StableId($"migration-gl-source-effect:{context.TenantId.Value:D}:{command.CompanyId:D}:{command.CurrencyCode.Trim().ToUpperInvariant()}:{command.OpeningDate:yyyy-MM-dd}");
            db.Journals.Add(journal);
            if (evidence.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journalId, command.CompanyId, null, evidence.Evidence, now));
            db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, sourceEffectId, command.CompanyId, MigrationGlContract, validation.Result.SourceEvidenceId, 1, journalId, now));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            var journalRecord = ToJournal(journal);
            var sourceEffect = ToSourceEffect(new FinanceSourceEffectEntity(context.TenantId, sourceEffectId, command.CompanyId, MigrationGlContract, validation.Result.SourceEvidenceId, 1, journalId, now));
            return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Success(new(journalRecord, sourceEffect, validation.Result.SourceEvidenceId, validation.Result.ResidualLines));
        }
        catch (DbUpdateException)
        {
            var evidence = await ReadMigrationGlOpeningAsync(context, command, CancellationToken.None);
            return evidence is not null
                ? FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Success(evidence)
                : FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure("migration_gl_opening_outcome_unknown");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return FinanceOperationResult<FinanceMigrationGlOpeningEvidence>.Failure("migration_gl_opening_outcome_unknown");
        }
    }

    private async Task<MigrationGlPreflight> ValidateMigrationGlOpeningAsync(FinanceDbContext db, FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, CancellationToken cancellationToken)
    {
        var company = Company(context, command.CompanyId);
        if (!InventoryResourceAuthorizationService.IsMigrationExecutionContext(context.FoundationContext)) return BlockGl("forbidden");
        if (company is null) return BlockGl("company_scope_denied");
        var currency = NormalizeCode(command.CurrencyCode);
        if (currency is null || currency != company.FunctionalCurrencyCode) return BlockGl("migration_gl_opening_currency_not_functional", company.FunctionalCurrencyCode);
        if (command.OpeningDate == default || command.Lines.Count == 0) return BlockGl("migration_gl_opening_payload_invalid", company.FunctionalCurrencyCode);
        if (command.Lines.Any(item => item.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(item.SourceLineReference) || item.SourceLineReference.Trim().Length > 256 || item.Debit < 0m || item.Credit < 0m || (item.Debit == 0m && item.Credit == 0m) || (item.Debit > 0m && item.Credit > 0m))) return BlockGl("migration_gl_opening_line_invalid", company.FunctionalCurrencyCode);
        if (command.Lines.GroupBy(item => item.AccountId).Any(group => group.Count() > 1)) return BlockGl("migration_gl_opening_duplicate_account", company.FunctionalCurrencyCode);
        if (command.Lines.GroupBy(item => item.SourceLineReference.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) return BlockGl("migration_gl_opening_duplicate_source_line_reference", company.FunctionalCurrencyCode);
        if (!SameAmount(command.Lines.Sum(item => item.Debit), command.Lines.Sum(item => item.Credit))) return BlockGl("migration_gl_opening_imbalanced", company.FunctionalCurrencyCode);
        var periods = await db.FiscalPeriods.Where(item => item.CompanyId == command.CompanyId && item.StartDate <= command.OpeningDate && item.EndDate >= command.OpeningDate).ToListAsync(cancellationToken);
        if (periods.Count == 0) return BlockGl("period_not_configured", company.FunctionalCurrencyCode);
        if (periods.Count != 1) return BlockGl("period_ambiguous", company.FunctionalCurrencyCode);
        if (periods[0].State != FinanceFiscalPeriodState.Open) return BlockGl(periods[0].State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed", company.FunctionalCurrencyCode);
        var approval = SourceApprovalPolicy.Resolve(MigrationGlContract, MigrationGlEvent);
        if (approval != FinanceApprovalRequirement.NotRequired) return BlockGl(approval == FinanceApprovalRequirement.Required ? "approval_required" : "approval_policy_not_configured", company.FunctionalCurrencyCode, approval);

        var acceptedContracts = AcceptedContracts();
        var prior = await db.Journals.AsNoTracking().AnyAsync(item => item.CompanyId == command.CompanyId && item.Status == FinanceJournalStatus.Posted && item.PostingDate <= command.OpeningDate && !acceptedContracts.Contains(item.SourceContract) && item.SourceContract != MigrationGlContract, cancellationToken);
        if (prior) return BlockGl("migration_gl_opening_prior_ledger_activity", company.FunctionalCurrencyCode);
        var controls = await ResolveControlsAsync(db, command, cancellationToken);
        if (!controls.Ready) return BlockGl(controls.Code, company.FunctionalCurrencyCode);
        var established = await EstablishedAsync(db, context, command, acceptedContracts, cancellationToken);
        if (!established.Ready) return BlockGl(established.Code, company.FunctionalCurrencyCode);
        var projections = await ProjectedAsync(db, command, controls, cancellationToken);
        if (!projections.Ready) return BlockGl(projections.Code, company.FunctionalCurrencyCode);
        var allControlAccounts = controls.ControlAccounts.Concat(established.ControlAccounts).ToHashSet();
        var establishedByAccount = established.Values.GroupBy(item => item.AccountId).ToDictionary(group => group.Key, group => group.Sum(item => item.SignedAmount));
        foreach (var item in projections.Values) establishedByAccount[item.AccountId] = establishedByAccount.GetValueOrDefault(item.AccountId) + item.Amount;
        var target = command.Lines.ToDictionary(item => item.AccountId, item => item.Debit - item.Credit);
        var residual = target.Keys.Concat(establishedByAccount.Keys).Distinct().Select(accountId =>
        {
            var amount = target.GetValueOrDefault(accountId) - establishedByAccount.GetValueOrDefault(accountId);
            var source = command.Lines.SingleOrDefault(item => item.AccountId == accountId)?.SourceLineReference?.Trim();
            var treatment = source is null ? "derived_offset_clearing" : allControlAccounts.Contains(accountId) ? "control_account" : "source_residual";
            return new FinanceMigrationGlOpeningResidualLine(accountId, target.GetValueOrDefault(accountId), establishedByAccount.GetValueOrDefault(accountId), amount > 0m ? amount : 0m, amount < 0m ? -amount : 0m, allControlAccounts.Contains(accountId), source, treatment);
        }).Where(item => Math.Abs(item.Debit - item.Credit) > MigrationGlTolerance).OrderBy(item => item.AccountId).ToArray();
        if (!SameAmount(residual.Sum(item => item.Debit), residual.Sum(item => item.Credit))) return BlockGl("migration_gl_opening_imbalanced", company.FunctionalCurrencyCode);
        foreach (var control in allControlAccounts)
        {
            var line = residual.FirstOrDefault(item => item.AccountId == control);
            var expected = target.GetValueOrDefault(control);
            if (Math.Abs(expected - establishedByAccount.GetValueOrDefault(control)) > MigrationGlTolerance || (line is not null && line.Debit != 0m || line is not null && line.Credit != 0m)) return BlockGl("migration_gl_control_account_mismatch", company.FunctionalCurrencyCode);
        }
        var expectations = command.Projections.Where(projection => projection.SourceRecordId != Guid.Empty).Select(projection =>
        {
            if (projection.AlreadyEstablishedExact)
            {
                return projection.PostingRuleId is { } ruleId
                    && projection.PostingRuleVersionNumber is { } ruleVersion
                    && projection.ControlAccountId is { } controlId
                    && projection.OffsetAccountId is { } offsetId
                    && projection.Reversal is { } reversal
                    ? new FinanceMigrationOpeningExpectation(projection.SourceRecordId, projection.SourceContract, projection.SourceEvent, projection.Amount, ruleId, ruleVersion, controlId, offsetId, reversal, projection.SourceEvidenceId, projection.SourceEvidenceVersion, projection.OwnerSourceId, projection.OwnerReference)
                    : null;
            }

            var spec = controls.Specs.SingleOrDefault(item => item.Contract == projection.SourceContract && item.Event == projection.SourceEvent);
            return spec is null
                ? null
                : new FinanceMigrationOpeningExpectation(projection.SourceRecordId, projection.SourceContract, projection.SourceEvent, projection.Amount, spec.Rule.Id, spec.Rule.VersionNumber, spec.Reversal ? spec.Rule.CreditAccountId : spec.Rule.DebitAccountId, spec.Reversal ? spec.Rule.DebitAccountId : spec.Rule.CreditAccountId, spec.Reversal, projection.SourceEvidenceId, projection.SourceEvidenceVersion, projection.OwnerSourceId, projection.OwnerReference);
        }).ToArray();
        if (expectations.Any(item => item is null)) return BlockGl("migration_gl_opening_economic_expectation_incomplete", company.FunctionalCurrencyCode);
        var sourceEvidenceId = MigrationGlSourceEvidenceId(context, command);
        if (!string.IsNullOrWhiteSpace(command.SourceGroupFingerprint) && await db.Journals.AsNoTracking().AnyAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == MigrationGlContract && item.CorrelationId.EndsWith(command.SourceGroupFingerprint) && item.SourceEvidenceId != sourceEvidenceId, cancellationToken))
            return BlockGl("migration_gl_source_conflict", company.FunctionalCurrencyCode, approval, false, sourceEvidenceId, residual);
        var existingEffect = await db.SourceEffects.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == MigrationGlContract && item.SourceEvidenceId == sourceEvidenceId && item.SourceEvidenceVersion == 1, cancellationToken);
        if (existingEffect is not null)
        {
            var existingJournal = await db.Journals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == existingEffect.JournalId && item.CompanyId == command.CompanyId, cancellationToken);
            if (existingJournal is null) return BlockGl("migration_gl_opening_outcome_unknown", company.FunctionalCurrencyCode, approval, false, sourceEvidenceId, residual);
            if (!FingerprintMatches(existingJournal.CorrelationId, command)) return BlockGl("migration_gl_source_conflict", company.FunctionalCurrencyCode, approval, false, sourceEvidenceId, residual);
            var existing = await ReadMigrationGlOpeningEvidenceAsync(db, context, command, residual, cancellationToken);
            if (existing is null) return BlockGl("migration_gl_opening_outcome_unknown", company.FunctionalCurrencyCode, approval, false, sourceEvidenceId, residual);
            if (!ResidualMatches(existing.ResidualLines, residual)) return BlockGl("migration_gl_source_conflict", company.FunctionalCurrencyCode, approval, false, sourceEvidenceId, residual);
        }
        return new(new(true, residual.Length == 0 ? "non_effect" : "ready", company.FunctionalCurrencyCode, approval, residual.Length == 0, sourceEvidenceId, residual, expectations.OfType<FinanceMigrationOpeningExpectation>().ToArray()), controls, projections.Values);
    }

    private async Task<FinanceMigrationGlOpeningEvidence?> ReadMigrationGlOpeningEvidenceAsync(FinanceDbContext db, FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, IReadOnlyList<FinanceMigrationGlOpeningResidualLine> residual, CancellationToken cancellationToken)
    {
        var sourceEvidenceId = MigrationGlSourceEvidenceId(context, command);
        var effect = await db.SourceEffects.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == MigrationGlContract && item.SourceEvidenceId == sourceEvidenceId && item.SourceEvidenceVersion == 1, cancellationToken);
        if (effect is null) return null;
        var journal = await db.Journals.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == effect.JournalId && item.CompanyId == command.CompanyId, cancellationToken);
        if (journal is null || journal.Status != FinanceJournalStatus.Posted || journal.SourceContract != MigrationGlContract || journal.SourceEvent != MigrationGlEvent || journal.SourceEvidenceId != sourceEvidenceId || journal.PostingRuleId is not null || !FingerprintMatches(journal.CorrelationId, command)) return null;
        var source = new FinanceSourceEffectRecord(effect.Id, effect.TenantId.Value, effect.CompanyId, effect.SourceContract, effect.SourceEvidenceId, effect.SourceEvidenceVersion, effect.JournalId, effect.CreatedAt);
        var journalRecord = ToJournal(journal);
        var recorded = journalRecord.Lines.Select(item => new { item.AccountId, item.Debit, item.Credit }).OrderBy(item => item.AccountId).ToArray();
        var expected = residual.Select(item => new { item.AccountId, item.Debit, item.Credit }).OrderBy(item => item.AccountId).ToArray();
        return recorded.Length == expected.Length && recorded.Zip(expected).All(item => item.First.AccountId == item.Second.AccountId && SameAmount(item.First.Debit, item.Second.Debit) && SameAmount(item.First.Credit, item.Second.Credit))
            ? new(journalRecord, source, sourceEvidenceId, residual)
            : null;
    }

    private async Task<GlControls> ResolveControlsAsync(FinanceDbContext db, FinanceMigrationGlOpeningCommand command, CancellationToken cancellationToken)
    {
        var specs = new (string Contract, string Event, bool Reversal)[]
        {
            (InventoryValuationContract, FinanceInventoryPostingClassifier.Classify(InventoryMovementSourceType.OpeningBalance, InventoryMovementDirection.Inbound), false),
            (MigrationArContract, "recognition", false),
            (MigrationApContract, "recognition", true),
            (MigrationCashBankContract, "recognition", false)
        };
        var resolved = new List<ControlSpec>();
        var required = CurrentProjectionRuleKeys(command.Projections);
        foreach (var spec in specs.Where(item => required.Contains($"{item.Contract}|{item.Event}")))
        {
            var matches = await db.PostingRules.Where(item => item.CompanyId == command.CompanyId && item.SourceContract == spec.Contract && item.SourceEvent == spec.Event && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= command.OpeningDate && (item.EffectiveTo == null || item.EffectiveTo >= command.OpeningDate)).ToListAsync(cancellationToken);
            if (matches.Count != 1) return new(false, matches.Count == 0 ? "posting_rule_not_configured" : "posting_rule_ambiguous", new HashSet<Guid>(), []);
            if (matches[0].CostCenterRequired || matches[0].DebitAccountId == matches[0].CreditAccountId) return new(false, "posting_rule_accounts_invalid", new HashSet<Guid>(), []);
            resolved.Add(new ControlSpec(spec.Contract, spec.Event, spec.Reversal, matches[0]));
        }
        var controls = resolved.Select(item => item.Reversal ? item.Rule.CreditAccountId : item.Rule.DebitAccountId).ToHashSet();
        var offsets = resolved.Select(item => item.Reversal ? item.Rule.DebitAccountId : item.Rule.CreditAccountId).ToHashSet();
        if (controls.Overlaps(offsets)) return new(false, "migration_gl_control_offset_collision", new HashSet<Guid>(), []);
        return new(true, "ready", controls, resolved);
    }

    internal static IReadOnlySet<string> CurrentProjectionRuleKeys(IReadOnlyList<FinanceMigrationOpeningProjection> projections) =>
        projections.Where(item => !item.AlreadyEstablishedExact)
            .Select(item => $"{item.SourceContract}|{item.SourceEvent}")
            .ToHashSet(StringComparer.Ordinal);

    private async Task<EstablishedResult> EstablishedAsync(FinanceDbContext db, FinanceRequestContext context, FinanceMigrationGlOpeningCommand command, IReadOnlySet<string> acceptedContracts, CancellationToken cancellationToken)
    {
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == command.CompanyId && item.Status == FinanceJournalStatus.Posted && item.PostingDate == command.OpeningDate && acceptedContracts.Contains(item.SourceContract) && !(item.SourceContract == MigrationGlContract && item.SourceEvidenceId == MigrationGlSourceEvidenceId(context, command))).ToListAsync(cancellationToken);
        var result = new List<EstablishedAmount>();
        var historicalControls = new HashSet<Guid>();
        foreach (var journal in journals)
        {
            if (journal.SourceEvidenceId is not { } evidenceId || journal.SourceEvidenceVersion is not { } evidenceVersion) return new(false, "migration_gl_subsidiary_evidence_incomplete", [], new HashSet<Guid>());
            var effect = await db.SourceEffects.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.SourceContract == journal.SourceContract && item.SourceEvidenceId == evidenceId && item.SourceEvidenceVersion == evidenceVersion && item.JournalId == journal.Id, cancellationToken);
            if (effect is null) return new(false, "migration_gl_subsidiary_evidence_incomplete", [], new HashSet<Guid>());
            var expectedEvent = journal.SourceContract switch { InventoryValuationContract => FinanceInventoryPostingClassifier.Classify(InventoryMovementSourceType.OpeningBalance, InventoryMovementDirection.Inbound), MigrationArContract or MigrationApContract or MigrationCashBankContract or MigrationGlContract => MigrationGlEvent, _ => string.Empty };
            if (!string.Equals(journal.SourceEvent, expectedEvent, StringComparison.Ordinal)) return new(false, "migration_gl_subsidiary_evidence_incomplete", [], new HashSet<Guid>());
            if (journal.SourceContract != MigrationGlContract)
            {
                if (journal.PostingRuleId is not { } ruleId || journal.PostingRuleVersionNumber is not { } ruleVersion) return new(false, "migration_gl_subsidiary_evidence_incomplete", [], new HashSet<Guid>());
                var rule = await db.PostingRules.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == command.CompanyId && item.Id == ruleId && item.VersionNumber == ruleVersion && item.SourceContract == journal.SourceContract && item.SourceEvent == journal.SourceEvent, cancellationToken);
                if (rule is null) return new(false, "migration_gl_subsidiary_evidence_incomplete", [], new HashSet<Guid>());
                historicalControls.Add(journal.SourceContract == MigrationApContract ? rule.CreditAccountId : rule.DebitAccountId);
            }
            result.AddRange(journal.Lines.Select(line => new EstablishedAmount(line.AccountId, line.FunctionalDebit - line.FunctionalCredit)));
        }
        return new(true, "ready", result, historicalControls);
    }

    private static async Task<ProjectedAmounts> ProjectedAsync(FinanceDbContext db, FinanceMigrationGlOpeningCommand command, GlControls controls, CancellationToken cancellationToken)
    {
        var values = new List<ProjectedAmount>();
        foreach (var projection in command.Projections.Where(item => !item.AlreadyEstablishedExact))
        {
            var spec = controls.Specs.SingleOrDefault(item => item.Contract == projection.SourceContract && item.Event == projection.SourceEvent);
            if (spec is null) return new(false, "migration_gl_projection_contract_invalid", []);
            if (projection.Amount <= 0m) return new(false, "migration_gl_projection_amount_invalid", []);
            values.Add(new(spec.Reversal ? spec.Rule.CreditAccountId : spec.Rule.DebitAccountId, spec.Reversal ? -projection.Amount : projection.Amount));
            values.Add(new(spec.Reversal ? spec.Rule.DebitAccountId : spec.Rule.CreditAccountId, spec.Reversal ? projection.Amount : -projection.Amount));
        }
        await Task.CompletedTask;
        return new(true, "ready", values);
    }

    private static IReadOnlySet<string> AcceptedContracts() => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        InventoryValuationContract, MigrationArContract, MigrationApContract, MigrationCashBankContract, MigrationGlContract
    };


    private static MigrationGlPreflight BlockGl(string code, string currency = "", FinanceApprovalRequirement approval = FinanceApprovalRequirement.NotConfigured, bool nonEffect = false, Guid sourceEvidenceId = default, IReadOnlyList<FinanceMigrationGlOpeningResidualLine>? residual = null) => new(new(false, code, currency, approval, nonEffect, sourceEvidenceId, residual ?? [], []), new(false, code, new HashSet<Guid>(), []), []);
    private static Guid MigrationGlSourceEvidenceId(FinanceRequestContext context, FinanceMigrationGlOpeningCommand command) => StableId($"migration-gl-group:{context.TenantId.Value:D}:{command.CompanyId:D}:{command.CurrencyCode.Trim().ToUpperInvariant()}:{command.OpeningDate:yyyy-MM-dd}");
    private static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    private static string DurableFingerprint(FinanceMigrationGlOpeningCommand command) => command.SourcePayloadFingerprint + (command.SourceGroupFingerprint ?? string.Empty);
    private static bool FingerprintMatches(string stored, FinanceMigrationGlOpeningCommand command) => string.Equals(stored, command.SourcePayloadFingerprint, StringComparison.Ordinal) || string.Equals(stored, DurableFingerprint(command), StringComparison.Ordinal);
    private static bool ResidualMatches(IReadOnlyList<FinanceMigrationGlOpeningResidualLine> left, IReadOnlyList<FinanceMigrationGlOpeningResidualLine> right) => left.Count == right.Count && left.OrderBy(item => item.AccountId).Zip(right.OrderBy(item => item.AccountId)).All(item => item.First.AccountId == item.Second.AccountId && SameAmount(item.First.Debit, item.Second.Debit) && SameAmount(item.First.Credit, item.Second.Credit));
    private static string LineReference(FinanceMigrationGlOpeningCommand command, FinanceMigrationGlOpeningResidualLine line) => line.SourceLineReference ?? "derived finance offset clearing";

    private sealed record MigrationGlPreflight(FinanceGlOpeningPreflightResult Result, GlControls Controls, IReadOnlyList<ProjectedAmount> Projections);
    private sealed record GlControls(bool Ready, string Code, IReadOnlySet<Guid> ControlAccounts, IReadOnlyList<ControlSpec> Specs);
    private sealed record ControlSpec(string Contract, string Event, bool Reversal, FinancePostingRuleEntity Rule);
    private sealed record EstablishedAmount(Guid AccountId, decimal SignedAmount);
    private sealed record EstablishedResult(bool Ready, string Code, IReadOnlyList<EstablishedAmount> Values, IReadOnlySet<Guid> ControlAccounts);
    private sealed record ProjectedAmount(Guid AccountId, decimal Amount);
    private sealed record ProjectedAmounts(bool Ready, string Code, IReadOnlyList<ProjectedAmount> Values);
}

#pragma warning restore CS1591
