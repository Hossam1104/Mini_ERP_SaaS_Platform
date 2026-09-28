#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// MESP-118 application behavior. This service owns reusable Currency and
/// Payment Term identity, lifecycle, effective configuration, and deterministic
/// read/reference contracts. Finance owns all downstream accounting effects.
/// </summary>
public sealed class MasterDataCurrencyPaymentTermService
{
    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataCurrencyPaymentTermPersistence persistence;

    public MasterDataCurrencyPaymentTermService(
        MasterDataResourceAuthorizationService authorization,
        IMasterDataCurrencyPaymentTermPersistence persistence)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataCurrencyRecord>>> ListCurrenciesAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.Currency, context, null, "currency-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataCurrencyRecord>>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataCurrencyRecord>>.Success(
                await persistence.ListCurrenciesAsync(context.TenantContext, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataCurrencyRecord>>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCurrencyRecord>> GetCurrencyAsync(
        MasterDataRequestContext context,
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.Currency, context, currencyId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindCurrencyAsync(context.TenantContext, currencyId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.View, "currency_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataCurrencyRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCurrencyReferenceRecord>> GetCurrencyReferenceAsync(
        MasterDataRequestContext context,
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.Currency, context, currencyId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCurrencyReferenceRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindCurrencyAsync(context.TenantContext, currencyId, cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataCurrencyReferenceRecord>(context, resource, MasterDataOperation.View, "currency_not_found", cancellationToken);
            }

            var snapshot = new ReferenceSnapshot(
                MasterDataResourceKind.Currency,
                record.Id,
                new TenantOwnership(record.TenantId.Value),
                record.Revision,
                record.Code);
            return MasterDataOperationResult<MasterDataCurrencyReferenceRecord>.Success(
                new MasterDataCurrencyReferenceRecord(
                    record.Id,
                    record.TenantId,
                    record.Code,
                    record.Name,
                    record.LifecycleState,
                    record.Revision,
                    snapshot,
                    record.Version));
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyReferenceRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(
        MasterDataRequestContext context,
        CreateMasterDataCurrencyCommand command,
        CancellationToken cancellationToken = default)
    {
        var currencyId = Guid.NewGuid();
        string code;
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(MasterDataResourceKind.Currency, context, currencyId, "currency-create");
            return await FailedAsync<MasterDataCurrencyRecord>(context, invalidResource, MasterDataOperation.Create, "validation_failed", cancellationToken);
        }

        var resource = Resource(MasterDataResourceKind.Currency, context, currencyId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Create, authorized.Decision, authorized.Code, cancellationToken);
        }

        var normalizedCommand = command with { Code = code };
        var evidence = CreateEvidence(context, resource, MasterDataOperation.Create, authorized.Decision, FoundationAuditReason.Allowed, afterSummary: CurrencySummary(code, command.Name, MasterDataLifecycleState.Active, 1));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Create,
                await persistence.CreateCurrencyAsync(context.TenantContext, currencyId, normalizedCommand, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Create, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCurrencyRecord>> EditCurrencyAsync(
        MasterDataRequestContext context,
        EditMasterDataCurrencyCommand command,
        CancellationToken cancellationToken = default)
    {
        string code;
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
            ValidateVersion(command.ExpectedVersion);
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(MasterDataResourceKind.Currency, context, command?.CurrencyId ?? Guid.NewGuid(), "currency-edit");
            return await FailedAsync<MasterDataCurrencyRecord>(context, invalidResource, MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var resource = Resource(MasterDataResourceKind.Currency, context, command.CurrencyId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        MasterDataCurrencyRecord? current;
        try
        {
            current = await persistence.FindCurrencyAsync(context.TenantContext, command.CurrencyId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Edit, "currency_not_found", cancellationToken);
        }

        var evidence = CreateEvidence(context, resource, MasterDataOperation.Edit, authorized.Decision, FoundationAuditReason.Allowed, CurrencySummary(current), CurrencySummary(code, command.Name, current.LifecycleState, current.Revision + 1));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Edit,
                await persistence.EditCurrencyAsync(context.TenantContext, command with { Code = code }, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataCurrencyRecord>> DeactivateCurrencyAsync(MasterDataRequestContext context, Guid currencyId, byte[] expectedVersion, CancellationToken cancellationToken = default) =>
        SetCurrencyLifecycleAsync(context, currencyId, MasterDataLifecycleState.Inactive, MasterDataOperation.Deactivate, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataCurrencyRecord>> ReactivateCurrencyAsync(MasterDataRequestContext context, Guid currencyId, byte[] expectedVersion, CancellationToken cancellationToken = default) =>
        SetCurrencyLifecycleAsync(context, currencyId, MasterDataLifecycleState.Active, MasterDataOperation.Reactivate, expectedVersion, cancellationToken);

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataPaymentTermRecord>>> ListPaymentTermsAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, null, "payment-term-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataPaymentTermRecord>>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataPaymentTermRecord>>.Success(
                await persistence.ListPaymentTermsAsync(context.TenantContext, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataPaymentTermRecord>>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> GetPaymentTermAsync(
        MasterDataRequestContext context,
        Guid paymentTermId,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, paymentTermId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindPaymentTermAsync(context.TenantContext, paymentTermId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.View, "payment_term_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataPaymentTermRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPaymentTermReferenceRecord>> GetPaymentTermReferenceAsync(
        MasterDataRequestContext context,
        Guid paymentTermId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, paymentTermId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPaymentTermReferenceRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindPaymentTermAsync(context.TenantContext, paymentTermId, cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataPaymentTermReferenceRecord>(context, resource, MasterDataOperation.View, "payment_term_not_found", cancellationToken);
            }

            var version = record.Versions.SingleOrDefault(item =>
                item.EffectiveFrom <= effectiveOn
                && (item.EffectiveTo is null || effectiveOn <= item.EffectiveTo.Value));
            if (version is null)
            {
                return await FailedAsync<MasterDataPaymentTermReferenceRecord>(context, resource, MasterDataOperation.View, "payment_term_version_not_found", cancellationToken);
            }

            var snapshot = new ReferenceSnapshot(
                MasterDataResourceKind.PaymentTerm,
                record.Id,
                new TenantOwnership(record.TenantId.Value),
                version.VersionNumber,
                $"{version.Code};v{version.VersionNumber}",
                effectiveOn);
            return MasterDataOperationResult<MasterDataPaymentTermReferenceRecord>.Success(
                new MasterDataPaymentTermReferenceRecord(
                    record.Id,
                    record.TenantId,
                    version.Code,
                    version.Name,
                    record.LifecycleState,
                    version.VersionNumber,
                    version.Id,
                    effectiveOn,
                    version,
                    snapshot,
                    record.Version));
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermReferenceRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPaymentTermDueDatePreview>> PreviewPaymentTermAsync(
        MasterDataRequestContext context,
        Guid paymentTermId,
        DateOnly effectiveOn,
        DateOnly baseDate,
        CancellationToken cancellationToken = default)
    {
        var reference = await GetPaymentTermReferenceAsync(context, paymentTermId, effectiveOn, cancellationToken);
        if (!reference.Succeeded || reference.Value is null)
        {
            return MasterDataOperationResult<MasterDataPaymentTermDueDatePreview>.Failure(reference.Code, reference.Evidence);
        }

        var version = reference.Value.Version;
        var dueDates = PaymentTermDueDateCalculator.CalculateDueDates(
                baseDate,
                version.ScheduleMode,
                version.DueOffset.Days,
                version.DueOffset.Months,
                version.Installments.Select(item => new PaymentTermInstallmentResponse(
                    item.Sequence,
                    item.Percentage,
                    item.Offset.Days,
                    item.Offset.Months)).ToArray())
            .Select(item => new MasterDataPaymentTermDueDate(item.Sequence, item.Percentage, item.DueDate))
            .ToArray();
        DateOnly? discountDate = version.EarlySettlementDiscount.Enabled
            ? PaymentTermDueDateCalculator.AddOffset(
                baseDate,
                version.EarlySettlementDiscount.Offset.Days,
                version.EarlySettlementDiscount.Offset.Months)
            : null;
        return MasterDataOperationResult<MasterDataPaymentTermDueDatePreview>.Success(
            new MasterDataPaymentTermDueDatePreview(
                reference.Value.Id,
                reference.Value.TenantId,
                reference.Value.Code,
                reference.Value.VersionNumber,
                reference.Value.VersionId,
                reference.Value.EffectiveOn,
                baseDate,
                version.BaseDateRule,
                version.ScheduleMode,
                dueDates,
                discountDate,
                version.EarlySettlementDiscount.Percentage,
                reference.Value.Snapshot));
    }

    public async Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(
        MasterDataRequestContext context,
        CreateMasterDataPaymentTermCommand command,
        CancellationToken cancellationToken = default)
    {
        var paymentTermId = Guid.NewGuid();
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            var code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
            MasterDataCurrencyPaymentTermValuePolicy.ValidatePaymentTerm(command.EffectiveFrom, command.EffectiveTo, command.BaseDateRule, command.ScheduleMode, command.DueOffset, command.Installments, command.EarlySettlementDiscount);
            command = command with { Code = code };
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(MasterDataResourceKind.PaymentTerm, context, paymentTermId, "payment-term-create");
            return await FailedAsync<MasterDataPaymentTermRecord>(context, invalidResource, MasterDataOperation.Create, "validation_failed", cancellationToken);
        }

        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, paymentTermId, command.Code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Create, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(context, resource, MasterDataOperation.Create, authorized.Decision, FoundationAuditReason.Allowed, afterSummary: PaymentTermSummary(command.Code, command.Name, MasterDataLifecycleState.Active, 1, command.EffectiveFrom));
        try
        {
            return await CompletePersistenceAsync(context, resource, MasterDataOperation.Create, await persistence.CreatePaymentTermAsync(context.TenantContext, paymentTermId, command, evidence, cancellationToken), evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Create, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(
        MasterDataRequestContext context,
        EditMasterDataPaymentTermCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            var code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
            ValidateVersion(command.ExpectedVersion);
            MasterDataCurrencyPaymentTermValuePolicy.ValidatePaymentTerm(command.EffectiveFrom, command.EffectiveTo, command.BaseDateRule, command.ScheduleMode, command.DueOffset, command.Installments, command.EarlySettlementDiscount);
            command = command with { Code = code };
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(MasterDataResourceKind.PaymentTerm, context, command?.PaymentTermId ?? Guid.NewGuid(), "payment-term-edit");
            return await FailedAsync<MasterDataPaymentTermRecord>(context, invalidResource, MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, command.PaymentTermId, command.Code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        MasterDataPaymentTermRecord? current;
        try
        {
            current = await persistence.FindPaymentTermAsync(context.TenantContext, command.PaymentTermId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Edit, "payment_term_not_found", cancellationToken);
        }

        var evidence = CreateEvidence(context, resource, MasterDataOperation.Edit, authorized.Decision, FoundationAuditReason.Allowed, PaymentTermSummary(current), PaymentTermSummary(command.Code, command.Name, current.LifecycleState, current.CurrentVersionNumber + 1, command.EffectiveFrom));
        try
        {
            return await CompletePersistenceAsync(context, resource, MasterDataOperation.Edit, await persistence.EditPaymentTermAsync(context.TenantContext, command, evidence, cancellationToken), evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> DeactivatePaymentTermAsync(MasterDataRequestContext context, Guid paymentTermId, byte[] expectedVersion, CancellationToken cancellationToken = default) =>
        SetPaymentTermLifecycleAsync(context, paymentTermId, MasterDataLifecycleState.Inactive, MasterDataOperation.Deactivate, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> ReactivatePaymentTermAsync(MasterDataRequestContext context, Guid paymentTermId, byte[] expectedVersion, CancellationToken cancellationToken = default) =>
        SetPaymentTermLifecycleAsync(context, paymentTermId, MasterDataLifecycleState.Active, MasterDataOperation.Reactivate, expectedVersion, cancellationToken);

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        if (resourceKind is not (MasterDataResourceKind.Currency or MasterDataResourceKind.PaymentTerm))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind));
        }

        var resource = Resource(resourceKind, context, resourceId, resourceId is null ? "audit-history" : null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.ViewAuditHistory);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>.Success(
                await persistence.ReadAuditHistoryAsync(context.TenantContext, resourceKind, resourceId, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(
        MasterDataRequestContext context,
        Guid currencyId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        return await SetLifecycleAsync(
            context,
            currencyId,
            lifecycleState,
            operation,
            expectedVersion,
            async (tenant, evidence) => await persistence.SetCurrencyLifecycleAsync(tenant, currencyId, lifecycleState, expectedVersion, evidence, cancellationToken),
            CurrencySummary,
            cancellationToken);
    }

    private async Task<MasterDataOperationResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(
        MasterDataRequestContext context,
        Guid paymentTermId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        var resource = Resource(MasterDataResourceKind.PaymentTerm, context, paymentTermId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPaymentTermRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            ValidateVersion(expectedVersion);
            var current = await persistence.FindPaymentTermAsync(context.TenantContext, paymentTermId, cancellationToken);
            if (current is null)
            {
                return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, operation, "payment_term_not_found", cancellationToken);
            }

            var evidence = CreateEvidence(context, resource, operation, authorized.Decision, FoundationAuditReason.Allowed, PaymentTermSummary(current), PaymentTermSummary(current.Code, current.Name, lifecycleState, current.CurrentVersionNumber, current.Versions.LastOrDefault()?.EffectiveFrom ?? DateOnly.MinValue));
            return await CompletePersistenceAsync(context, resource, operation, await persistence.SetPaymentTermLifecycleAsync(context.TenantContext, paymentTermId, lifecycleState, expectedVersion, evidence, cancellationToken), evidence, cancellationToken);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, operation, "validation_failed", cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPaymentTermRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataCurrencyRecord>> SetLifecycleAsync(
        MasterDataRequestContext context,
        Guid currencyId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        Func<TenantContext, MasterDataAuditEvidence, Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>>> persist,
        Func<MasterDataCurrencyRecord, string> summary,
        CancellationToken cancellationToken)
    {
        var resource = Resource(MasterDataResourceKind.Currency, context, currencyId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCurrencyRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            ValidateVersion(expectedVersion);
            var current = await persistence.FindCurrencyAsync(context.TenantContext, currencyId, cancellationToken);
            if (current is null)
            {
                return await FailedAsync<MasterDataCurrencyRecord>(context, resource, operation, "currency_not_found", cancellationToken);
            }

            var evidence = CreateEvidence(context, resource, operation, authorized.Decision, FoundationAuditReason.Allowed, summary(current), CurrencySummary(current.Code, current.Name, lifecycleState, current.Revision + 1));
            return await CompletePersistenceAsync(context, resource, operation, await persist(context.TenantContext, evidence), evidence, cancellationToken);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, operation, "validation_failed", cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCurrencyRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<T>> CompletePersistenceAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPersistenceResult<T> result,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (result.Succeeded && result.Value is not null)
        {
            return MasterDataOperationResult<T>.Success(result.Value, evidence);
        }

        var failure = await FailedAsync<T>(context, resource, operation, result.Code, cancellationToken);
        return failure with { Evidence = evidence };
    }

    private async Task<MasterDataOperationResult<T>> DeniedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(context, resource, operation, decision, ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> FailedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(context, resource, operation, MasterDataPolicyDecision.Denied(code), ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> AppendDeniedEvidenceAsync<T>(
        MasterDataRequestContext context,
        MasterDataAuditEvidence evidence,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            var audit = await persistence.AppendAuditAsync(context.TenantContext, evidence, cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<T>.Failure(code, evidence)
                : MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
        catch
        {
            return MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
    }

    private static MasterDataResourceReference Resource(MasterDataResourceKind kind, MasterDataRequestContext context, Guid? stableId, string? businessCode) => new(
        kind,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        CurrencyPaymentTermScopePolicy.CreateScope(new TenantId(context.TenantId.Value)));

    private static MasterDataAuditEvidence CreateEvidence(MasterDataRequestContext context, MasterDataResourceReference resource, MasterDataOperation operation, MasterDataPolicyDecision decision, FoundationAuditReason reason, string? beforeSummary = null, string? afterSummary = null) =>
        MasterDataAuditEvidenceFactory.Create(context, resource, operation, decision, reason, beforeSummary, afterSummary);

    private static string CurrencySummary(MasterDataCurrencyRecord record) => CurrencySummary(record.Code, record.Name, record.LifecycleState, record.Revision);
    private static string CurrencySummary(string code, LocalizedName name, MasterDataLifecycleState state, int revision) => $"code={code};en={name.English ?? string.Empty};ar={name.Arabic ?? string.Empty};state={state};revision={revision}";
    private static string PaymentTermSummary(MasterDataPaymentTermRecord record) => $"code={record.Code};state={record.LifecycleState};current-version={record.CurrentVersionNumber};versions={record.Versions.Count}";
    private static string PaymentTermSummary(string code, LocalizedName name, MasterDataLifecycleState state, int version, DateOnly effectiveFrom) => $"code={code};en={name.English ?? string.Empty};ar={name.Arabic ?? string.Empty};state={state};version={version};effective-from={effectiveFrom:yyyy-MM-dd}";

    private static void ValidateVersion(byte[] version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Length == 0 || version.Length > 64)
        {
            throw new ArgumentException("An optimistic-concurrency version is required.", nameof(version));
        }
    }

    private static FoundationAuditReason ReasonFor(string code) => code switch
    {
        "cross_tenant_target_denied" => FoundationAuditReason.CrossTenantTargetDenied,
        "permission_denied" => FoundationAuditReason.PermissionDenied,
        "authorization_denied" or "resource_scope_denied" => FoundationAuditReason.AuthorizationDenied,
        "currency_not_found" or "payment_term_not_found" or "payment_term_version_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" or "payment_term_effective_overlap" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => FoundationAuditReason.InternalFailure,
        "validation_failed" or "currency_duplicate" or "payment_term_duplicate" or "currency_lifecycle_no_change" or "payment_term_lifecycle_no_change" => FoundationAuditReason.ValidationFailed,
        _ => FoundationAuditReason.AuthorizationDenied
    };
}

#pragma warning restore CS1591
