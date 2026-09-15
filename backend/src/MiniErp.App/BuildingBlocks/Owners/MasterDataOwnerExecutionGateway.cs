#pragma warning disable CS1591

using Microsoft.AspNetCore.Http;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.BuildingBlocks.Owners;

internal sealed record TrustedOwnerImportRequest(
    Guid BatchId,
    MasterDataResourceKind ResourceKind,
    MasterDataImportSource Source,
    string IdempotencyKey,
    string Fingerprint,
    IReadOnlyList<MasterDataImportRowInput> Rows);

internal sealed class MasterDataOwnerExecutionGateway : IOwnerExecutionGateway
{
    private readonly MasterDataImportService imports;

    public MasterDataOwnerExecutionGateway(MasterDataImportService imports) =>
        this.imports = imports ?? throw new ArgumentNullException(nameof(imports));

    public async Task<OwnerOperationResult<OwnerBatchEvidence>> CreateBatchAsync(
        FoundationRequestContext trustedContext,
        OwnerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(trustedContext))
            return Denied<OwnerBatchEvidence>();
        var result = await imports.CreateTrustedOwnerBatchAsync(
            trustedContext,
            new TrustedOwnerImportRequest(
                request.BatchId,
                ToMasterDataKind(request.ResourceKind),
                new MasterDataImportSource("migration", request.Source.SourceFileReference, request.Source.BatchReference),
                request.IdempotencyKey,
                request.Fingerprint,
                request.Rows.Select(item => new MasterDataImportRowInput(item.RowNumber, item.Fields)).ToArray()),
            cancellationToken);
        return Map(result, ToBatchEvidence);
    }

    public async Task<OwnerOperationResult<OwnerBatchEvidence>> SimulateAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default) =>
        IsAuthorized(trustedContext)
            ? Map(await imports.SimulateTrustedOwnerBatchAsync(trustedContext, batchId, cancellationToken), ToBatchEvidence)
            : Denied<OwnerBatchEvidence>();

    public async Task<OwnerOperationResult<OwnerBatchEvidence>> ExecuteAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        IsAuthorized(trustedContext)
            ? Map(await imports.ExecuteTrustedOwnerBatchAsync(trustedContext, batchId, expectedVersion, cancellationToken), ToBatchEvidence)
            : Denied<OwnerBatchEvidence>();

    public async Task<OwnerEvidence?> ReadEvidenceAsync(
        FoundationRequestContext trustedContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized(trustedContext))
            return null;
        var evidence = await imports.ReadTrustedOwnerEvidenceAsync(trustedContext, batchId, cancellationToken);
        return evidence is null
            ? null
            : new OwnerEvidence(
                ToBatchEvidence(evidence.Batch),
                evidence.Rows.Select(item => new OwnerRowEvidence(
                    item.OriginalRowNumber,
                    item.Outcome switch
                    {
                        MasterDataImportRowOutcome.Rejected => OwnerRowOutcome.Rejected,
                        MasterDataImportRowOutcome.Quarantined => OwnerRowOutcome.Quarantined,
                        _ => OwnerRowOutcome.Accepted
                    },
                    item.MutationDisposition switch
                    {
                        MasterDataImportMutationDisposition.Committed => OwnerMutationDisposition.Committed,
                        MasterDataImportMutationDisposition.Updated => OwnerMutationDisposition.Updated,
                        MasterDataImportMutationDisposition.Failed => OwnerMutationDisposition.Failed,
                        _ => OwnerMutationDisposition.NotAttempted
                    },
                    item.Id,
                    item.ResultingResourceId,
                    item.ResultingResourceCode,
                    item.Diagnostics.Select(diagnostic => new OwnerDiagnosticEvidence(diagnostic.Code)).ToArray())).ToArray());
    }

    private static bool IsAuthorized(FoundationRequestContext? context) =>
        context is not null
        && context.SecurityProfile is (FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
        && context.TenantContext is { } tenant
        && context.PlatformGovernanceContext is null
        && context.ActorId is { } actorId
        && actorId != Guid.Empty
        && (tenant.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership && context.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
            || tenant.AuthorizationPath == TenantAuthorizationPath.SupportGrant && context.SecurityProfile == FoundationSecurityProfile.SupportGrant)
        && (tenant.ActorId is null || tenant.ActorId == actorId)
        && context.SessionId is { } sessionId
        && sessionId != Guid.Empty
        && string.Equals(context.Permission, "tenant.migration.execute", StringComparison.Ordinal);

    private static OwnerOperationResult<T> Denied<T>() =>
        new(false, "migration_execution_authority_required", default, StatusCodes.Status403Forbidden);

    private static OwnerBatchEvidence ToBatchEvidence(MasterDataImportBatchRecord batch) => new(
        batch.Id,
        batch.Status switch
        {
            MasterDataImportStatus.Draft or MasterDataImportStatus.Simulating => OwnerBatchStatus.Draft,
            MasterDataImportStatus.Validated => OwnerBatchStatus.Validated,
            MasterDataImportStatus.Completed => OwnerBatchStatus.Completed,
            MasterDataImportStatus.CompletedWithErrors => OwnerBatchStatus.CompletedWithErrors,
            _ => OwnerBatchStatus.Draft
        },
        batch.Version);

    private static MasterDataResourceKind ToMasterDataKind(OwnerResourceKind kind) => kind switch
    {
        OwnerResourceKind.Product => MasterDataResourceKind.Product,
        OwnerResourceKind.Supplier => MasterDataResourceKind.Supplier,
        OwnerResourceKind.Customer => MasterDataResourceKind.BusinessCustomer,
        OwnerResourceKind.Currency => MasterDataResourceKind.Currency,
        OwnerResourceKind.Tax => MasterDataResourceKind.Tax,
        OwnerResourceKind.PaymentTerm => MasterDataResourceKind.PaymentTerm,
        OwnerResourceKind.UnitOfMeasure => MasterDataResourceKind.UnitOfMeasure,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static OwnerOperationResult<TDestination> Map<TSource, TDestination>(
        MasterDataImportOperationResult<TSource> result,
        Func<TSource, TDestination> map) =>
        result.Succeeded && result.Value is not null
            ? new OwnerOperationResult<TDestination>(true, result.Code, map(result.Value), result.StatusCode)
            : new OwnerOperationResult<TDestination>(false, result.Code, default, result.StatusCode);
}

#pragma warning restore CS1591
