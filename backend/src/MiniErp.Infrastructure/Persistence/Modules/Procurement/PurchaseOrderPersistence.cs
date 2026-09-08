#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Reporting;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class PurchaseOrderPersistence : IPurchaseOrderPersistence, IPurchaseOrderReportingReadPort
{
    private const string CreateOperationId = "procurement.purchase-order.create";
    private const int ReplayResponseSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions options;

    internal PurchaseOrderPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<PurchaseOrderListRecord>> ListAsync(
        TenantContext tenantContext,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.PurchaseOrders.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus)
        {
            query = query.Where(item => item.Status == selectedStatus);
        }

        var entities = await query
            .Include(item => item.Lines)
            .ToListAsync(cancellationToken);
        var records = entities
            .Select(item => new PurchaseOrderListRecord(
                item.Id,
                item.TenantId.Value,
                new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId),
                item.Status,
                item.SupplierCode,
                item.SupplierName,
                item.SupplierQuotationReference,
                item.CurrencyCode,
                item.Lines.Sum(CommercialLineTotal),
                item.Lines.Count,
                item.CreatedAt,
                item.UpdatedAt,
                item.Version.ToArray()))
            .ToArray();
        return records.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToArray();
    }

    public async Task<ReportingSourcePage<PurchaseOrderListRecord>> ListReportingPageAsync(
        TenantContext tenantContext,
        PurchaseOrderStatus? status,
        ReportingPageRequest page,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.PurchaseOrders.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus) query = query.Where(item => item.Status == selectedStatus);
        var total = await query.CountAsync(cancellationToken);
        var asOf = total == 0 ? null : await query.Select(item => (DateTimeOffset?)item.UpdatedAt).MaxAsync(cancellationToken);
        var ordered = page.SortBy?.ToLowerInvariant() switch
        {
            "supplier" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.SupplierName).ThenByDescending(item => item.Id) : query.OrderBy(item => item.SupplierName).ThenBy(item => item.Id),
            "status" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "currency" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.CurrencyCode).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CurrencyCode).ThenBy(item => item.Id),
            "createdat" => page.SortDirection == "desc" ? query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id),
            _ => page.SortDirection == "asc" ? query.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id) : query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
        };
        var entities = await ordered.Include(item => item.Lines).Skip(page.Offset).Take(page.PageSize).ToListAsync(cancellationToken);
        var records = entities.Select(item => new PurchaseOrderListRecord(
            item.Id, item.TenantId.Value, new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId), item.Status,
            item.SupplierCode, item.SupplierName, item.SupplierQuotationReference, item.CurrencyCode,
            item.Lines.Sum(CommercialLineTotal), item.Lines.Count, item.CreatedAt, item.UpdatedAt, item.Version.ToArray())).ToArray();
        return new ReportingSourcePage<PurchaseOrderListRecord>(records, total, "updatedAt,id", asOf);
    }

    private static decimal CommercialLineTotal(PurchaseOrderLineEntity line)
    {
        var gross = line.OrderedQuantity * line.UnitPrice;
        var discount = line.DiscountAmount ?? (line.DiscountPercentage is { } percentage ? gross * percentage / 100m : 0m);
        var taxable = Math.Max(0m, gross - discount);
        var tax = line.TaxAmount ?? (line.TaxRatePercentage is { } rate ? taxable * rate / 100m : 0m);
        return taxable + tax;
    }

    public async Task<bool> SourceDecisionConsumedAsync(
        TenantContext tenantContext,
        Guid sourceDecisionId,
        CancellationToken cancellationToken = default)
    {
        if (sourceDecisionId == Guid.Empty)
        {
            return false;
        }

        await using var db = CreateContext(tenantContext);
        return await db.PurchaseOrders
            .AsNoTracking()
            .AnyAsync(item => item.SourceDecisionId == sourceDecisionId, cancellationToken);
    }

    /// <summary>
    /// Read-only durable replay probe over the persisted Tenant-scoped audit evidence. It lets the
    /// application layer resolve an identical, already-successful retry before any state-dependent
    /// business validation runs, without duplicating the mutation engine. The persistence-side replay
    /// check inside each mutation remains in place as defense in depth.
    /// </summary>
    public async Task<PurchaseOrderReplayProbe> ProbeReplayAsync(
        TenantContext tenantContext,
        PurchaseOrderReplayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = CreateContext(tenantContext);
        var lookup = await FindReplayAsync(db, query, cancellationToken);
        return lookup.Outcome switch
        {
            ReplayOutcome.Replay when lookup.Record is not null => PurchaseOrderReplayProbe.ForReplay(lookup.Record),
            ReplayOutcome.Conflict => PurchaseOrderReplayProbe.Conflict,
            _ => PurchaseOrderReplayProbe.NotFound
        };
    }

    public async Task<PurchaseOrderRecord?> FindAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken);
        return entity is null ? null : await ToRecordAsync(db, entity, cancellationToken);
    }

    public async Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CreateAsync(
        TenantContext tenantContext,
        PurchaseOrderCreateCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(PurchaseOrderPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(replay.Record!);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value || command.Id == Guid.Empty)
            {
                return Denied(PurchaseOrderPersistenceOutcome.Duplicate, "purchase_order_duplicate");
            }

            if (await db.PurchaseOrders.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return Denied(PurchaseOrderPersistenceOutcome.Duplicate, "purchase_order_duplicate");
            }

            if (await db.PurchaseOrders.AnyAsync(item => item.SourceDecisionId == command.Source.SourceDecisionId, cancellationToken))
            {
                return Denied(PurchaseOrderPersistenceOutcome.Duplicate, "purchase_order_duplicate");
            }

            var sourceValidation = await ValidateSourceAsync(db, tenantContext, command, cancellationToken);
            if (!sourceValidation.Succeeded)
            {
                return Denied(sourceValidation.Outcome, sourceValidation.Code);
            }

            var entity = new PurchaseOrderEntity(command, tenantContext.TenantId);
            foreach (var sourceLine in command.Lines)
            {
                entity.Lines.Add(new PurchaseOrderLineEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    Guid.NewGuid(),
                    sourceLine));
            }

            entity.TouchVersion();
            db.PurchaseOrders.Add(entity);
            db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                entity.Id,
                PurchaseOrderStatus.Draft,
                PurchaseOrderStatus.Draft,
                PurchaseOrderHistoryAction.Created,
                command.CreatedByActorId,
                null,
                evidence.CorrelationId,
                null,
                null,
                null,
                null,
                command.OccurredAt));
            var audit = new PurchaseOrderAuditEntity(evidence);
            db.PurchaseOrderAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = await ToRecordAsync(db, entity, cancellationToken);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(PurchaseOrderPersistenceOutcome.Duplicate, "purchase_order_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> EditAsync(
        TenantContext tenantContext,
        PurchaseOrderEditCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange))
                {
                    return MutationDecision.Failure("edit_not_allowed");
                }

                var existing = entity.Lines.ToDictionary(item => item.Id);
                if (command.Lines.Count != existing.Count || command.Lines.Any(item => !existing.ContainsKey(item.PurchaseOrderLineId)))
                {
                    return MutationDecision.Failure("line_set_mismatch");
                }

                foreach (var edit in command.Lines)
                {
                    if (edit.OrderedQuantity < existing[edit.PurchaseOrderLineId].ConfirmedQuantity)
                    {
                        return MutationDecision.Failure("proposed_quantity_below_confirmed");
                    }
                }

                foreach (var edit in command.Lines)
                {
                    existing[edit.PurchaseOrderLineId].Edit(edit.OrderedQuantity, edit.UnitPrice, edit.DeliveryDate, edit.Notes);
                    existing[edit.PurchaseOrderLineId].TouchVersion();
                }

                entity.ReplaceDraft(command.Notes, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    fromStatus,
                    PurchaseOrderHistoryAction.Edited,
                    evidence.ActorId,
                    null,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> SubmitAsync(
        TenantContext tenantContext,
        PurchaseOrderSubmitCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange))
                {
                    return MutationDecision.Failure("submit_not_allowed");
                }

                entity.Submit(
                    command.Policy,
                    JsonSerializer.Serialize(command.Policy, JsonOptions),
                    command.IsReapproval,
                    command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    PurchaseOrderStatus.PendingApproval,
                    PurchaseOrderHistoryAction.Submitted,
                    evidence.ActorId,
                    null,
                    evidence.CorrelationId,
                    command.Policy.PolicyId,
                    command.Policy.Version,
                    command.Policy.Stages.OrderBy(item => item.Sequence).FirstOrDefault()?.StageKey,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveAsync(
        TenantContext tenantContext,
        PurchaseOrderApprovalCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus != PurchaseOrderStatus.PendingApproval)
                {
                    return MutationDecision.Failure("decision_not_allowed");
                }

                var policy = ReadPolicy(entity);
                if (!PurchaseRequestValuePolicy.IsValidPolicy(policy))
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var stages = policy!.Stages.OrderBy(item => item.Sequence).ToArray();
                if (entity.CurrentApprovalStageIndex < 0 || entity.CurrentApprovalStageIndex >= stages.Length)
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var stage = stages[entity.CurrentApprovalStageIndex];
                var approvers = ReadApprovers(entity).ToList();
                if (approvers.Contains(command.ActorId))
                {
                    return MutationDecision.Failure("approval_duplicate");
                }

                approvers.Add(command.ActorId);
                var stageComplete = approvers.Count >= stage.RequiredApprovals;
                var nextStageIndex = entity.CurrentApprovalStageIndex + (stageComplete ? 1 : 0);
                var resultingStatus = stageComplete && nextStageIndex >= stages.Length
                    ? PurchaseOrderStatus.Approved
                    : PurchaseOrderStatus.PendingApproval;
                var nextApprovers = stageComplete ? [] : approvers.ToArray();
                entity.RecordApproval(
                    resultingStatus,
                    nextStageIndex >= stages.Length ? entity.CurrentApprovalStageIndex : nextStageIndex,
                    stageComplete ? 0 : approvers.Count,
                    JsonSerializer.Serialize(nextApprovers, JsonOptions),
                    command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    resultingStatus,
                    PurchaseOrderHistoryAction.ApprovalRecorded,
                    command.ActorId,
                    null,
                    evidence.CorrelationId,
                    policy.PolicyId,
                    policy.Version,
                    stage.StageKey,
                    command.DelegatedFromActorId,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        DecisionAsync(tenantContext, command, evidence, PurchaseOrderStatus.Rejected, PurchaseOrderHistoryAction.Rejected, cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ReturnForChangeAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        DecisionAsync(tenantContext, command, evidence, PurchaseOrderStatus.ReturnedForChange, PurchaseOrderHistoryAction.ReturnedForChange, cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> IssueAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            (db, entity, fromStatus) =>
            {
                if (fromStatus != PurchaseOrderStatus.Approved)
                {
                    return Task.FromResult(MutationDecision.Failure("issue_not_allowed"));
                }

                entity.SetStatus(PurchaseOrderStatus.Issued, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    PurchaseOrderStatus.Issued,
                    PurchaseOrderHistoryAction.Issued,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return Task.FromResult(MutationDecision.Success());
            },
            cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CancelAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            (db, entity, fromStatus) =>
            {
                if (fromStatus is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange or PurchaseOrderStatus.Approved or PurchaseOrderStatus.Issued or PurchaseOrderStatus.PartiallyConfirmed or PurchaseOrderStatus.NoResponse))
                {
                    return Task.FromResult(MutationDecision.Failure("cancel_not_allowed"));
                }

                entity.SetStatus(PurchaseOrderStatus.Cancelled, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    PurchaseOrderStatus.Cancelled,
                    PurchaseOrderHistoryAction.Cancelled,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return Task.FromResult(MutationDecision.Success());
            },
            cancellationToken);

    public async Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RecordConfirmationAsync(
        TenantContext tenantContext,
        PurchaseOrderConfirmationCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
            if (entity is null)
            {
                return Denied(PurchaseOrderPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(PurchaseOrderPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(replay.Record!);
            }

            if (!VersionMatches(entity.Version, command.PurchaseOrderVersion))
            {
                return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (entity.Status is not (PurchaseOrderStatus.Issued or PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyConfirmed or PurchaseOrderStatus.NoResponse))
            {
                return Denied(PurchaseOrderPersistenceOutcome.InvalidState, "confirmation_not_allowed");
            }

            var lineMap = entity.Lines.ToDictionary(item => item.Id);
            if (command.Lines.Count != lineMap.Count || command.Lines.Any(item => !lineMap.ContainsKey(item.PurchaseOrderLineId)))
            {
                return Denied(PurchaseOrderPersistenceOutcome.InvalidState, "line_set_mismatch");
            }

            var confirmation = new PurchaseOrderConfirmationEntity(tenantContext.TenantId, command);
            var hasChanges = false;
            foreach (var lineCommand in command.Lines)
            {
                var line = lineMap[lineCommand.PurchaseOrderLineId];
                if (lineCommand.ConfirmedQuantity > line.OrderedQuantity)
                {
                    return Denied(PurchaseOrderPersistenceOutcome.InvalidState, "confirmation_quantity_exceeds_ordered");
                }

                if (lineCommand.ProposedQuantity is { } proposedQuantity
                    && proposedQuantity < Math.Max(line.ConfirmedQuantity, lineCommand.ConfirmedQuantity))
                {
                    return Denied(PurchaseOrderPersistenceOutcome.InvalidState, "proposed_quantity_below_confirmed");
                }
            }

            foreach (var lineCommand in command.Lines)
            {
                var line = lineMap[lineCommand.PurchaseOrderLineId];
                if (lineCommand.OrderedQuantityAtResponse != line.OrderedQuantity)
                {
                    return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
                }

                var lineEntity = new PurchaseOrderConfirmationLineEntity(tenantContext.TenantId, confirmation.Id, lineCommand);
                confirmation.Lines.Add(lineEntity);
                if (lineCommand.ProposedQuantity.HasValue || lineCommand.ProposedUnitPrice.HasValue || lineCommand.ProposedDeliveryDate.HasValue)
                {
                    hasChanges = true;
                    var change = new PurchaseOrderSupplierChangeEntity(
                        tenantContext.TenantId,
                        confirmation.Id,
                        line,
                        lineCommand,
                        command.ActorId,
                        command.OccurredAt);
                    confirmation.Changes.Add(change);
                }
            }

            if (hasChanges && (command.ReapprovalPolicy is null || !PurchaseRequestValuePolicy.IsValidPolicy(command.ReapprovalPolicy)))
            {
                return Denied(PurchaseOrderPersistenceOutcome.InvalidState, "approval_policy_not_configured");
            }

            foreach (var item in command.Evidence)
            {
                confirmation.Evidence.Add(new PurchaseOrderEvidenceEntity(tenantContext.TenantId, confirmation.Id, item));
            }

            confirmation.TouchVersion();
            db.PurchaseOrderConfirmations.Add(confirmation);
            var fromStatus = entity.Status;
            foreach (var lineCommand in command.Lines)
            {
                var line = lineMap[lineCommand.PurchaseOrderLineId];
                line.RecordConfirmation(lineCommand.ConfirmedQuantity, lineCommand.ExpectedDeliveryDate);
                line.TouchVersion();
            }

            if (!hasChanges)
            {
                entity.SetLatestConfirmation(command.Id, command.Status, command.OccurredAt);
            }
            else
            {
                entity.SetLatestConfirmation(command.Id, command.Status, command.OccurredAt);
                entity.BeginSupplierChangeApproval(fromStatus, command.OccurredAt);
                entity.SetReapprovalPolicy(
                    command.ReapprovalPolicy!,
                    JsonSerializer.Serialize(command.ReapprovalPolicy, JsonOptions));
            }

            entity.TouchVersion();
            db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                entity.Id,
                fromStatus,
                entity.Status,
                hasChanges
                    ? PurchaseOrderHistoryAction.SupplierChangeProposed
                    : command.Status switch
                    {
                        PurchaseOrderConfirmationStatus.Confirmed => PurchaseOrderHistoryAction.ConfirmationRecorded,
                        PurchaseOrderConfirmationStatus.PartiallyConfirmed => PurchaseOrderHistoryAction.PartiallyConfirmed,
                        PurchaseOrderConfirmationStatus.Rejected => PurchaseOrderHistoryAction.SupplierRejected,
                        _ => PurchaseOrderHistoryAction.NoResponse
                    },
                command.ActorId,
                command.Reason,
                evidence.CorrelationId,
                command.ReapprovalPolicy?.PolicyId,
                command.ReapprovalPolicy?.Version,
                command.ReapprovalPolicy?.Stages.OrderBy(item => item.Sequence).FirstOrDefault()?.StageKey,
                null,
                command.OccurredAt));
            var audit = new PurchaseOrderAuditEntity(evidence with
            {
                BeforeStatus = fromStatus,
                AfterStatus = entity.Status,
                BeforeSummary = fromStatus.ToString(),
                AfterSummary = entity.Status.ToString()
            });
            db.PurchaseOrderAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = await ToRecordAsync(db, entity, cancellationToken);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveSupplierChangeAsync(
        TenantContext tenantContext,
        PurchaseOrderApprovalCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus != PurchaseOrderStatus.ChangedPendingApproval)
                {
                    return MutationDecision.Failure("supplier_change_approval_not_allowed");
                }

                var policy = ReadPolicy(entity);
                if (!PurchaseRequestValuePolicy.IsValidPolicy(policy))
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var stages = policy!.Stages.OrderBy(item => item.Sequence).ToArray();
                var stage = stages.ElementAtOrDefault(entity.CurrentApprovalStageIndex);
                if (stage is null)
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var approvers = ReadApprovers(entity).ToList();
                if (approvers.Contains(command.ActorId))
                {
                    return MutationDecision.Failure("approval_duplicate");
                }

                approvers.Add(command.ActorId);
                var stageComplete = approvers.Count >= stage.RequiredApprovals;
                var nextStageIndex = entity.CurrentApprovalStageIndex + (stageComplete ? 1 : 0);
                if (!stageComplete || nextStageIndex < stages.Length)
                {
                    Guid[] nextApprovers = stageComplete ? [] : approvers.ToArray();
                    entity.RecordApproval(
                        PurchaseOrderStatus.ChangedPendingApproval,
                        nextStageIndex,
                        stageComplete ? 0 : approvers.Count,
                        JsonSerializer.Serialize(nextApprovers, JsonOptions),
                        command.OccurredAt);
                    entity.TouchVersion();
                    db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        entity.Id,
                        fromStatus,
                        PurchaseOrderStatus.ChangedPendingApproval,
                        PurchaseOrderHistoryAction.ApprovalRecorded,
                        command.ActorId,
                        null,
                        evidence.CorrelationId,
                        policy.PolicyId,
                        policy.Version,
                        stage.StageKey,
                        command.DelegatedFromActorId,
                        command.OccurredAt));
                    return MutationDecision.Success();
                }

                var changes = await db.PurchaseOrderSupplierChanges
                    .Where(item => item.PurchaseOrderLineId != Guid.Empty
                        && item.Status == PurchaseOrderSupplierChangeStatus.PendingApproval
                        && db.PurchaseOrderConfirmations.Any(confirmation => confirmation.Id == item.PurchaseOrderConfirmationId && confirmation.PurchaseOrderId == entity.Id))
                    .ToListAsync(cancellationToken);
                var lines = entity.Lines.ToDictionary(item => item.Id);
                foreach (var change in changes)
                {
                    if (change.ProposedQuantity is { } proposedQuantity
                        && (!lines.TryGetValue(change.PurchaseOrderLineId, out var line)
                            || proposedQuantity < line.ConfirmedQuantity))
                    {
                        return MutationDecision.Failure("proposed_quantity_below_confirmed");
                    }
                }

                foreach (var change in changes)
                {
                    if (lines.TryGetValue(change.PurchaseOrderLineId, out var line))
                    {
                        if (!line.TryApplySupplierChange(change.ProposedQuantity, change.ProposedUnitPrice, change.ProposedDeliveryDate))
                        {
                            return MutationDecision.Failure("proposed_quantity_below_confirmed");
                        }

                        line.TouchVersion();
                    }

                    change.Decide(PurchaseOrderSupplierChangeStatus.Approved, command.OccurredAt, null);
                    change.TouchVersion();
                }

                var resultingStatus = DeriveConfirmationStatus(entity);
                entity.CompleteSupplierChangeApproval(resultingStatus, command.OccurredAt);
                entity.ResetApprovalState();
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    resultingStatus,
                    PurchaseOrderHistoryAction.SupplierChangeApproved,
                    command.ActorId,
                    null,
                    evidence.CorrelationId,
                    policy.PolicyId,
                    policy.Version,
                    stage.StageKey,
                    command.DelegatedFromActorId,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectSupplierChangeAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus != PurchaseOrderStatus.ChangedPendingApproval)
                {
                    return MutationDecision.Failure("supplier_change_rejection_not_allowed");
                }

                var changes = await db.PurchaseOrderSupplierChanges
                    .Where(item => item.Status == PurchaseOrderSupplierChangeStatus.PendingApproval
                        && db.PurchaseOrderConfirmations.Any(confirmation => confirmation.Id == item.PurchaseOrderConfirmationId && confirmation.PurchaseOrderId == entity.Id))
                    .ToListAsync(cancellationToken);
                foreach (var change in changes)
                {
                    change.Decide(PurchaseOrderSupplierChangeStatus.Rejected, command.OccurredAt, command.Reason);
                    change.TouchVersion();
                }

                entity.RejectSupplierChange(DeriveConfirmationStatus(entity), command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    entity.Status,
                    PurchaseOrderHistoryAction.SupplierChangeRejected,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public async Task<IReadOnlyList<PurchaseOrderConfirmationRecord>> ReadConfirmationsAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseOrderConfirmations
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.Evidence)
            .Include(item => item.Changes)
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
        return records
            .OrderBy(item => item.RecordedAt)
            .ThenBy(item => item.Id)
            .Select(ToConfirmationRecord)
            .ToArray();
    }

    public async Task<IReadOnlyList<PurchaseOrderHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseOrderHistory
            .AsNoTracking()
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .Select(item => new PurchaseOrderHistoryRecord(
                item.Id,
                item.PurchaseOrderId,
                item.OccurredAt,
                item.FromStatus,
                item.ToStatus,
                item.Action,
                item.ActorId,
                item.Reason,
                item.CorrelationId,
                item.PolicyId,
                item.PolicyVersion,
                item.StageKey,
                item.DelegatedFromActorId))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    public async Task<IReadOnlyList<PurchaseOrderAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseOrderAudit
            .AsNoTracking()
            .Where(item => item.PurchaseOrderId == purchaseOrderId)
            .Select(item => new PurchaseOrderAuditRecord(
                item.Id,
                item.PurchaseOrderId,
                item.OccurredAt,
                item.OperationId,
                item.CorrelationId,
                item.TenantId.Value,
                item.ActorId,
                item.SessionId,
                item.AuthorizationPath,
                item.Decision,
                item.Reason,
                item.BeforeStatus,
                item.AfterStatus,
                item.CompanyId,
                item.BranchId,
                item.BeforeSummary,
                item.AfterSummary,
                item.IdempotencyKey))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    private Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> DecisionAsync(
        TenantContext tenantContext,
        PurchaseOrderActionCommand command,
        PurchaseOrderAuditEvidence evidence,
        PurchaseOrderStatus targetStatus,
        PurchaseOrderHistoryAction action,
        CancellationToken cancellationToken) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            (db, entity, fromStatus) =>
            {
                if (fromStatus != PurchaseOrderStatus.PendingApproval)
                {
                    return Task.FromResult(MutationDecision.Failure("decision_not_allowed"));
                }

                entity.SetStatus(targetStatus, command.OccurredAt);
                entity.TouchVersion();
                var policy = ReadPolicy(entity);
                var stage = policy?.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(entity.CurrentApprovalStageIndex);
                db.PurchaseOrderHistory.Add(new PurchaseOrderHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    targetStatus,
                    action,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    policy?.PolicyId,
                    policy?.Version,
                    stage?.StageKey,
                    null,
                    command.OccurredAt));
                return Task.FromResult(MutationDecision.Success());
            },
            cancellationToken);

    private async Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> MutateAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        PurchaseOrderAuditEvidence evidence,
        Func<ProcurementDbContext, PurchaseOrderEntity, PurchaseOrderStatus, Task<MutationDecision>> mutation,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken);
            if (entity is null)
            {
                return Denied(PurchaseOrderPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(PurchaseOrderPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(replay.Record!);
            }

            if (!VersionMatches(entity.Version, expectedVersion))
            {
                return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            var fromStatus = entity.Status;
            var decision = await mutation(db, entity, fromStatus);
            if (!decision.Succeeded)
            {
                return Denied(PurchaseOrderPersistenceOutcome.InvalidState, decision.Code);
            }

            var audit = new PurchaseOrderAuditEntity(evidence with
            {
                BeforeStatus = evidence.BeforeStatus ?? fromStatus,
                AfterStatus = evidence.AfterStatus ?? entity.Status,
                BeforeSummary = evidence.BeforeSummary ?? fromStatus.ToString(),
                AfterSummary = evidence.AfterSummary ?? entity.Status.ToString()
            });
            db.PurchaseOrderAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = await ToRecordAsync(db, entity, cancellationToken);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseOrderPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    private async Task<SourceValidation> ValidateSourceAsync(
        ProcurementDbContext db,
        TenantContext tenantContext,
        PurchaseOrderCreateCommand command,
        CancellationToken cancellationToken)
    {
        var decision = await db.SupplierSourceDecisions
            .SingleOrDefaultAsync(item => item.Id == command.Source.SourceDecisionId, cancellationToken);
        if (decision is null)
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.NotFound, "source_decision_not_found");
        }

        var source = await db.PurchaseRequests
            .SingleOrDefaultAsync(item => item.Id == decision.PurchaseRequestId, cancellationToken);
        var quotation = await db.SupplierQuotations
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == decision.SelectedQuotationId, cancellationToken);
        if (source is null || quotation is null)
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.NotFound, "source_not_found");
        }

        if (source.Status != PurchaseRequestStatus.Approved)
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.InvalidState, "purchase_request_not_approved");
        }

        if (quotation.Status != SupplierQuotationStatus.Submitted)
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.InvalidState, "source_quotation_not_eligible");
        }

        if (decision.SelectedQuotationId != command.Source.SupplierQuotationId
            || decision.PurchaseRequestId != command.Source.PurchaseRequestId
            || source.CompanyId != command.Scope.CompanyId
            || source.BranchId != command.Scope.BranchId
            || quotation.CompanyId != command.Scope.CompanyId
            || quotation.BranchId != command.Scope.BranchId
            || quotation.SupplierId != command.Source.Supplier.Id
            || quotation.CurrencyId != command.Source.Currency.Id)
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.InvalidState, "source_lineage_invalid");
        }

        var sourceLineIds = quotation.Lines.Select(item => item.Id).ToHashSet();
        if (command.Lines.Count != quotation.Lines.Count
            || command.Lines.Any(item => !sourceLineIds.Contains(item.SourceQuotationLineId)))
        {
            return SourceValidation.Denied(PurchaseOrderPersistenceOutcome.InvalidState, "source_lineage_invalid");
        }

        return SourceValidation.Success();
    }

    private Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        PurchaseOrderAuditEvidence evidence,
        CancellationToken cancellationToken) =>
        FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);

    // Create has no pre-existing target: `evidence.PurchaseOrderId` is a freshly generated id for this
    // call and must not be compared against the previously created target's id.
    private static PurchaseOrderReplayQuery ToReplayQuery(PurchaseOrderAuditEvidence evidence) => new(
        evidence.ActorId,
        evidence.OperationId,
        evidence.IdempotencyKey,
        evidence.OperationId == CreateOperationId ? null : evidence.PurchaseOrderId,
        evidence.RequestFingerprint);

    private async Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        PurchaseOrderReplayQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.IdempotencyKey))
        {
            return ReplayLookup.NotFound;
        }

        // Tenant scoping is applied transparently by the `ProcurementDbContext` Tenant query filter.
        var replayCandidates = await db.PurchaseOrderAudit
            .Where(item => item.ActorId == query.ActorId
                && item.OperationId == query.OperationId
                && item.IdempotencyKey == query.IdempotencyKey)
            .Select(item => new
            {
                item.Id,
                item.PurchaseOrderId,
                item.OccurredAt,
                item.RequestFingerprint,
                item.ReplayResponseSchemaVersion,
                item.ReplayResponseSnapshotJson
            })
            .ToListAsync(cancellationToken);
        if (replayCandidates.Count == 0)
        {
            return ReplayLookup.NotFound;
        }

        if (replayCandidates.Select(item => item.PurchaseOrderId).Distinct().Count() != 1)
        {
            return ReplayLookup.Conflict;
        }

        if (query.PurchaseOrderId is { } targetId
            && replayCandidates.Any(item => item.PurchaseOrderId != targetId))
        {
            return ReplayLookup.Conflict;
        }

        if (replayCandidates.Any(item => !string.Equals(item.RequestFingerprint, query.RequestFingerprint, StringComparison.Ordinal)))
        {
            return ReplayLookup.Conflict;
        }

        var original = replayCandidates
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .First();
        if (original.ReplayResponseSchemaVersion != ReplayResponseSchemaVersion
            || string.IsNullOrWhiteSpace(original.ReplayResponseSnapshotJson))
        {
            return ReplayLookup.Conflict;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ReplayResponseSnapshot>(original.ReplayResponseSnapshotJson, JsonOptions);
            if (snapshot is null
                || snapshot.SchemaVersion != ReplayResponseSchemaVersion
                || snapshot.Response is null
                || snapshot.Response.Id != original.PurchaseOrderId)
            {
                return ReplayLookup.Conflict;
            }

            return ReplayLookup.ForReplay(snapshot.Response);
        }
        catch (JsonException)
        {
            return ReplayLookup.Conflict;
        }
    }

    private async Task<PurchaseOrderRecord> ToRecordAsync(
        ProcurementDbContext db,
        PurchaseOrderEntity entity,
        CancellationToken cancellationToken)
    {
        var pendingChanges = await db.PurchaseOrderSupplierChanges
            .AsNoTracking()
            .Where(item => item.Status == PurchaseOrderSupplierChangeStatus.PendingApproval
                && db.PurchaseOrderConfirmations.Any(confirmation => confirmation.Id == item.PurchaseOrderConfirmationId && confirmation.PurchaseOrderId == entity.Id))
            .ToListAsync(cancellationToken);
        return ToRecord(entity, pendingChanges
            .OrderBy(item => item.ProposedAt)
            .ThenBy(item => item.Id)
            .ToArray());
    }

    private static string SerializeReplayResponse(PurchaseOrderRecord response) =>
        JsonSerializer.Serialize(new ReplayResponseSnapshot(ReplayResponseSchemaVersion, response), JsonOptions);

    private static PurchaseOrderRecord ToRecord(PurchaseOrderEntity entity, IReadOnlyList<PurchaseOrderSupplierChangeEntity> pendingChanges)
    {
        var policy = ReadPolicy(entity);
        var stage = policy?.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(entity.CurrentApprovalStageIndex);
        return new PurchaseOrderRecord(
            entity.Id,
            entity.TenantId.Value,
            entity.CreatedByActorId,
            new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
            entity.Status,
            ToSourceResponse(entity),
            entity.Notes,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.SubmittedAt,
            entity.ApprovedAt,
            entity.IssuedAt,
            entity.CancelledAt,
            entity.LatestConfirmationId,
            entity.LatestConfirmationStatus,
            policy,
            policy is null || stage is null
                ? null
                : new PurchaseOrderApprovalRecord(
                    policy.PolicyId,
                    policy.Version,
                    entity.CurrentApprovalStageIndex,
                    stage.StageKey,
                    stage.RequiredApprovals,
                    entity.CurrentStageApprovalCount,
                    stage.AllowDelegation,
                    policy.AllowRequesterCancellationWhilePending,
                    entity.IsReapproval),
            entity.Lines
                .OrderBy(item => item.Id)
                .Select(item => new PurchaseOrderLineRecord(
                    item.Id,
                    item.SourceQuotationLineId,
                    item.PurchaseRequestLineId,
                    item.ProductSku,
                    item.ProductName,
                    item.UnitOfMeasureCode,
                    item.OrderedQuantity,
                    item.ConfirmedQuantity,
                    item.RemainingQuantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.DiscountPercentage,
                    item.TaxCode,
                    item.TaxName,
                    item.TaxRatePercentage,
                    item.TaxAmount,
                    item.RequestedNeedByDate,
                    item.DeliveryDate,
                    item.Notes,
                    item.Version.ToArray()))
                .ToArray(),
            pendingChanges.Select(ToSupplierChangeRecord).ToArray(),
            entity.Version.ToArray());
    }

    private static PurchaseOrderConfirmationRecord ToConfirmationRecord(PurchaseOrderConfirmationEntity entity) =>
        new(
            entity.Id,
            entity.PurchaseOrderId,
            entity.Status,
            entity.ResponseDate,
            entity.SupplierReference,
            entity.SupplierContact,
            entity.Reason,
            entity.Notes,
            entity.RecordedByActorId,
            entity.RecordedAt,
            entity.PurchaseOrderVersion.ToArray(),
            entity.Lines.OrderBy(item => item.Id).Select(item => new PurchaseOrderConfirmationLineRecord(
                item.Id,
                item.PurchaseOrderLineId,
                item.OrderedQuantityAtResponse,
                item.ConfirmedQuantity,
                item.RemainingQuantity,
                item.ExpectedDeliveryDate,
                item.ProposedQuantity,
                item.ProposedUnitPrice,
                item.ProposedDeliveryDate,
                item.ChangeReason,
                item.Version.ToArray())).ToArray(),
            entity.Evidence.OrderBy(item => item.RecordedAt).ThenBy(item => item.Id).Select(item => new PurchaseOrderEvidenceReference(
                item.Id,
                item.ReferenceId,
                item.FileName,
                item.ContentType,
                item.Description,
                item.Source,
                item.ExternalReference,
                item.RecordedByActorId,
                item.RecordedAt,
                item.Version.ToArray())).ToArray(),
            entity.Changes.OrderBy(item => item.ProposedAt).ThenBy(item => item.Id).Select(ToSupplierChangeRecord).ToArray(),
            entity.Version.ToArray());

    private static PurchaseOrderSupplierChangeRecord ToSupplierChangeRecord(PurchaseOrderSupplierChangeEntity item) => new(
        item.Id,
        item.PurchaseOrderConfirmationId,
        item.PurchaseOrderLineId,
        item.PreviousOrderedQuantity,
        item.ProposedQuantity,
        item.PreviousUnitPrice,
        item.ProposedUnitPrice,
        item.PreviousDeliveryDate,
        item.ProposedDeliveryDate,
        item.Status,
        item.Reason,
        item.ActorId,
        item.ProposedAt,
        item.DecidedAt,
        item.DecisionReason,
        item.Version.ToArray());

    private static PurchaseOrderSourceResponse ToSourceResponse(PurchaseOrderEntity entity) => new(
        entity.PurchaseRequestId,
        entity.SupplierQuotationId,
        entity.SourceDecisionId,
        entity.SourcePurchaseRequestReference,
        entity.SourcePurchaseRequestPurpose,
        entity.SupplierQuotationReference,
        new PurchaseOrderSupplierResponse(entity.SupplierId, entity.SupplierCode, entity.SupplierName),
        new PurchaseOrderCurrencyResponse(entity.CurrencyId, entity.CurrencyCode, entity.CurrencyName),
        entity.PaymentTermId is { } paymentTermId
            ? new PurchaseOrderPaymentTermResponse(paymentTermId, entity.PaymentTermCode ?? string.Empty, entity.PaymentTermName ?? entity.PaymentTermCode ?? string.Empty, entity.PaymentTermVersion ?? 0)
            : null,
        entity.SourceDecisionRationale,
        entity.SourceSelectedAt);

    private ProcurementDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static IQueryable<PurchaseOrderEntity> ApplyTrustedScope(IQueryable<PurchaseOrderEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference)
        {
            return query;
        }

        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
        {
            return query.Where(_ => false);
        }

        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static bool VersionMatches(byte[] actual, byte[] expected) => actual.SequenceEqual(expected);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 }
                || current is SqliteException { SqliteErrorCode: 19 })
            {
                return true;
            }
        }

        return false;
    }

    private static PurchaseRequestApprovalPolicyDefinition? ReadPolicy(PurchaseOrderEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.ApprovalPolicySnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PurchaseRequestApprovalPolicyDefinition>(entity.ApprovalPolicySnapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PurchaseOrderStatus DeriveConfirmationStatus(PurchaseOrderEntity entity) =>
        entity.LatestConfirmationStatus switch
        {
            PurchaseOrderConfirmationStatus.Rejected => PurchaseOrderStatus.Rejected,
            PurchaseOrderConfirmationStatus.NoResponse => PurchaseOrderStatus.NoResponse,
            _ when entity.Lines.Count > 0 && entity.Lines.All(item => item.ConfirmedQuantity >= item.OrderedQuantity) => PurchaseOrderStatus.Confirmed,
            _ => PurchaseOrderStatus.PartiallyConfirmed
        };

    private static IReadOnlyList<Guid> ReadApprovers(PurchaseOrderEntity entity)
    {
        try
        {
            return JsonSerializer.Deserialize<Guid[]>(entity.CurrentStageApproverIdsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PurchaseOrderPersistenceResult<PurchaseOrderRecord> Denied(PurchaseOrderPersistenceOutcome outcome, string code) =>
        PurchaseOrderPersistenceResult<PurchaseOrderRecord>.Denied(outcome, code);

    private sealed record MutationDecision(bool Succeeded, string Code)
    {
        internal static MutationDecision Success() => new(true, "succeeded");
        internal static MutationDecision Failure(string code) => new(false, code);
    }

    private sealed record SourceValidation(PurchaseOrderPersistenceOutcome Outcome, string Code)
    {
        internal bool Succeeded => Outcome == PurchaseOrderPersistenceOutcome.Succeeded;
        internal static SourceValidation Success() => new(PurchaseOrderPersistenceOutcome.Succeeded, "valid");
        internal static SourceValidation Denied(PurchaseOrderPersistenceOutcome outcome, string code) => new(outcome, code);
    }

    private enum ReplayOutcome
    {
        NotFound,
        Replay,
        Conflict
    }

    private sealed record ReplayLookup(ReplayOutcome Outcome, PurchaseOrderRecord? Record)
    {
        internal static readonly ReplayLookup NotFound = new(ReplayOutcome.NotFound, null);
        internal static readonly ReplayLookup Conflict = new(ReplayOutcome.Conflict, null);
        internal static ReplayLookup ForReplay(PurchaseOrderRecord record) => new(ReplayOutcome.Replay, record);
    }

    private sealed record ReplayResponseSnapshot(int SchemaVersion, PurchaseOrderRecord Response);
}

#pragma warning restore CS1591
