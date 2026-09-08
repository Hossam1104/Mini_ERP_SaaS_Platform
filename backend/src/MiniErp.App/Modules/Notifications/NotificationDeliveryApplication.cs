#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;

namespace MiniErp.App.Modules.Notifications;

public enum NotificationRequestOutcome
{
    Denied = 1,
    Requested = 2,
    Queued = 3,
    Delivered = 4,
    Failed = 5,
    Unavailable = 6,
    Suppressed = 7,
    Unknown = 8,

    /// <summary>
    /// The request failed structural/content validation (template, locale, or
    /// idempotency key shape) before authorization was even evaluated. Safe
    /// HTTP 400, distinct from the 403 <see cref="Denied"/> outcome used for
    /// scope/recipient authorization failures.
    /// </summary>
    ValidationFailed = 9
}

/// <summary>
/// Authorized Release-1 notification request; contact data is never accepted.
/// The idempotency key is never accepted from the request body -- the
/// canonical <c>Idempotency-Key</c> HTTP header is the sole authoritative key.
/// </summary>
public sealed record NotificationDispatchRequest(
    Guid RecipientUserId,
    string? Template,
    string? Locale);

/// <summary>Safe public outcome for the notification dispatch operation.</summary>
public sealed record NotificationDispatchResponse(
    NotificationRequestOutcome Outcome,
    Guid? IntentId,
    NotificationDeliveryState? DeliveryState,
    DurableWorkFailureCategory FailureCategory,
    string SafeCode,
    string EvidenceSource);

/// <summary>Safe result for one Tenant-bound notification request.</summary>
public sealed record NotificationDispatchResult(
    NotificationRequestOutcome Outcome,
    Guid? IntentId,
    NotificationDeliveryState? DeliveryState,
    DurableWorkFailureCategory FailureCategory,
    string SafeCode,
    string EvidenceSource,
    FoundationAuditEvidence? Evidence)
{
    public bool Succeeded => Outcome is NotificationRequestOutcome.Requested
        or NotificationRequestOutcome.Queued
        or NotificationRequestOutcome.Delivered;
}

/// <summary>
/// Coordinates recipient authorization, immutable intent creation, mandatory
/// audit evidence and the provider-neutral delivery adapter. The local adapter
/// is explicit test/development evidence and never implies a production send.
/// </summary>
public sealed class NotificationDeliveryApplication
{
    private const string OperationId = "notification.intent.dispatch";
    private readonly INotificationRecipientAuthorizer recipientAuthorizer;
    private readonly INotificationDeliveryAdapter deliveryAdapter;
    private readonly FoundationAuditCoordinator audit;
    private readonly TimeProvider timeProvider;

    public NotificationDeliveryApplication(
        INotificationRecipientAuthorizer recipientAuthorizer,
        INotificationDeliveryAdapter deliveryAdapter,
        FoundationAuditCoordinator audit,
        TimeProvider? timeProvider = null)
    {
        this.recipientAuthorizer = recipientAuthorizer ?? throw new ArgumentNullException(nameof(recipientAuthorizer));
        this.deliveryAdapter = deliveryAdapter ?? throw new ArgumentNullException(nameof(deliveryAdapter));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<NotificationDispatchResult> DispatchAsync(
        FoundationRequestContext requestContext,
        TenantWorkScope scope,
        NotificationRecipientReference recipient,
        string template,
        string locale,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(recipient);
        var tenantContext = requestContext.TenantContext;
        if (tenantContext is null
            || requestContext.ActorId is not { } actorId
            || requestContext.SessionId is null
            || tenantContext.ActorId is not { } tenantActorId
            || tenantActorId != actorId)
        {
            return new(NotificationRequestOutcome.Denied, null, null, DurableWorkFailureCategory.AuthorizationDenied, "tenant_context_required", "none", null);
        }

        if (scope.TenantId != tenantContext.TenantId)
        {
            return await DeniedAsync(requestContext, recipient, "scope_denied", FoundationAuditReason.CrossTenantTargetDenied, cancellationToken);
        }

        var authorization = await recipientAuthorizer.AuthorizeAsync(tenantContext, recipient, cancellationToken);
        if (!authorization.Allowed || authorization.Recipient is null)
        {
            return await DeniedAsync(requestContext, recipient, "recipient_denied", FoundationAuditReason.PermissionDenied, cancellationToken);
        }

        TenantNotificationIntent intent;
        try
        {
            intent = TenantNotificationIntent.Create(
                tenantContext,
                scope,
                authorization.Recipient,
                template,
                locale,
                idempotencyKey,
                timeProvider: timeProvider);
        }
        catch (ArgumentException)
        {
            return await DeniedAsync(
                requestContext,
                recipient,
                "validation_failed",
                FoundationAuditReason.ValidationFailed,
                cancellationToken,
                outcome: NotificationRequestOutcome.ValidationFailed,
                failureCategory: DurableWorkFailureCategory.ValidationFailed);
        }

        var requestedEvidence = await audit.RecordAsync(
            requestContext,
            OperationId,
            Correlation(requestContext),
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            idempotencyKey: intent.IdempotencyKey,
            source: "notification-control",
            targetType: "notification-intent",
            targetReference: intent.IntentId.ToString("N"),
            changeSummary: "notification request accepted",
            cancellationToken: cancellationToken);
        if (!requestedEvidence.Succeeded)
        {
            return new(NotificationRequestOutcome.Unavailable, null, null, DurableWorkFailureCategory.Unknown, "audit_evidence_unavailable", "none", null);
        }

        NotificationDeliveryResult delivery;
        try
        {
            delivery = await deliveryAdapter.DeliverAsync(tenantContext, intent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            await audit.RecordAsync(
                requestContext,
                OperationId,
                Correlation(requestContext),
                FoundationAuditDecision.EffectFailed,
                FoundationAuditReason.InternalFailure,
                idempotencyKey: intent.IdempotencyKey,
                source: "notification-control",
                targetType: "notification-intent",
                targetReference: intent.IntentId.ToString("N"),
                changeSummary: "delivery adapter unavailable",
                retryOfEvidenceId: requestedEvidence.Evidence?.EvidenceId,
                attempt: 2,
                cancellationToken: cancellationToken);
            return new(NotificationRequestOutcome.Unknown, intent.IntentId, intent.DeliveryState, DurableWorkFailureCategory.Unknown, "delivery_outcome_unknown", "adapter", requestedEvidence.Evidence);
        }

        var outcome = MapOutcome(delivery);
        var decision = outcome switch
        {
            NotificationRequestOutcome.Delivered => FoundationAuditDecision.Allowed,
            NotificationRequestOutcome.Queued => FoundationAuditDecision.Retry,
            NotificationRequestOutcome.Failed => FoundationAuditDecision.EffectFailed,
            NotificationRequestOutcome.Suppressed => FoundationAuditDecision.Denied,
            _ => FoundationAuditDecision.EffectFailed
        };
        var reason = outcome switch
        {
            NotificationRequestOutcome.Delivered => FoundationAuditReason.Allowed,
            NotificationRequestOutcome.Queued => FoundationAuditReason.RetryAccepted,
            NotificationRequestOutcome.Failed => FoundationAuditReason.EffectFailed,
            NotificationRequestOutcome.Suppressed => FoundationAuditReason.AuthorizationDenied,
            _ => FoundationAuditReason.InternalFailure
        };
        var finalEvidence = await audit.RecordAsync(
            requestContext,
            OperationId,
            Correlation(requestContext),
            decision,
            reason,
            idempotencyKey: intent.IdempotencyKey,
            source: "notification-control",
            targetType: "notification-intent",
            targetReference: intent.IntentId.ToString("N"),
            changeSummary: delivery.SafeOutcome,
            retryOfEvidenceId: decision is FoundationAuditDecision.Retry or FoundationAuditDecision.EffectFailed
                ? requestedEvidence.Evidence?.EvidenceId
                : null,
            attempt: decision is FoundationAuditDecision.Retry or FoundationAuditDecision.EffectFailed ? 2 : 1,
            cancellationToken: cancellationToken);

        return new(
            finalEvidence.Succeeded ? outcome : NotificationRequestOutcome.Unavailable,
            intent.IntentId,
            delivery.State,
            delivery.FailureCategory,
            finalEvidence.Succeeded ? delivery.SafeOutcome : "audit_evidence_unavailable",
            delivery.EvidenceSource,
            finalEvidence.Evidence ?? requestedEvidence.Evidence);
    }

    private async Task<NotificationDispatchResult> DeniedAsync(
        FoundationRequestContext context,
        NotificationRecipientReference recipient,
        string code,
        FoundationAuditReason reason,
        CancellationToken cancellationToken,
        NotificationRequestOutcome outcome = NotificationRequestOutcome.Denied,
        DurableWorkFailureCategory failureCategory = DurableWorkFailureCategory.AuthorizationDenied)
    {
        var evidence = await audit.RecordAsync(
            context,
            OperationId,
            Correlation(context),
            FoundationAuditDecision.Denied,
            reason,
            source: "notification-control",
            targetType: "notification-recipient",
            targetReference: recipient.UserId.ToString("N"),
            changeSummary: code,
            cancellationToken: cancellationToken);
        return new(
            evidence.Succeeded ? outcome : NotificationRequestOutcome.Unavailable,
            null,
            null,
            failureCategory,
            evidence.Succeeded ? code : "audit_evidence_unavailable",
            "none",
            evidence.Evidence);
    }

    private static NotificationRequestOutcome MapOutcome(NotificationDeliveryResult result) =>
        result.EvidenceSource == "no-provider"
            ? NotificationRequestOutcome.Unavailable
            : result.State switch
        {
            NotificationDeliveryState.Delivered or NotificationDeliveryState.Duplicate => NotificationRequestOutcome.Delivered,
            NotificationDeliveryState.RetryScheduled => NotificationRequestOutcome.Queued,
            NotificationDeliveryState.DeadLetter => NotificationRequestOutcome.Failed,
            _ when result.FailureCategory == DurableWorkFailureCategory.TenantMismatch => NotificationRequestOutcome.Suppressed,
            _ => NotificationRequestOutcome.Unknown
        };

    private static string Correlation(FoundationRequestContext context) =>
        context.TenantContext?.CorrelationId?.Value ?? Guid.NewGuid().ToString("N");
}
#pragma warning restore CS1591
