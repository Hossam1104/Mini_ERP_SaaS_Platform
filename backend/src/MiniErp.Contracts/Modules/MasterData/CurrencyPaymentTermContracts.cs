#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

public sealed record CurrencyWriteRequest(
    string? Code,
    string? EnglishName,
    string? ArabicName);

public enum PaymentTermBaseDateRule
{
    DocumentDate = 1,
    InvoiceDate = 2,
    DeliveryDate = 3,
    ReceiptDate = 4
}

public enum PaymentTermScheduleMode
{
    SingleDueDate = 1,
    Installments = 2
}

public sealed record PaymentTermOffsetRequest(
    int Days = 0,
    int Months = 0);

public sealed record PaymentTermInstallmentRequest(
    int Sequence,
    decimal Percentage,
    int Days = 0,
    int Months = 0);

public sealed record EarlySettlementDiscountRequest(
    bool Enabled,
    decimal? Percentage = null,
    int Days = 0,
    int Months = 0);

public sealed record PaymentTermWriteRequest(
    string? Code,
    string? EnglishName,
    string? ArabicName,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    PaymentTermOffsetRequest? DueOffset,
    IReadOnlyList<PaymentTermInstallmentRequest>? Installments,
    EarlySettlementDiscountRequest? EarlySettlementDiscount);

public sealed record CurrencyResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    string LifecycleState,
    int Revision,
    byte[] Version);

public sealed record CurrencyReferenceResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    string LifecycleState,
    int Revision,
    string ReferenceValue,
    string ReferenceEffectiveOn,
    byte[] Version);

public sealed record PaymentTermInstallmentResponse(
    int Sequence,
    decimal Percentage,
    int Days,
    int Months);

public sealed record PaymentTermVersionResponse(
    Guid Id,
    int VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    int DueOffsetDays,
    int DueOffsetMonths,
    IReadOnlyList<PaymentTermInstallmentResponse> Installments,
    bool EarlySettlementDiscountEnabled,
    decimal? EarlySettlementDiscountPercentage,
    int EarlySettlementDiscountDays,
    int EarlySettlementDiscountMonths,
    string Code,
    string? EnglishName,
    string? ArabicName);

public sealed record PaymentTermResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    string LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<PaymentTermVersionResponse> Versions,
    byte[] Version);

public sealed record PaymentTermReferenceResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    string LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    int DueOffsetDays,
    int DueOffsetMonths,
    IReadOnlyList<PaymentTermInstallmentResponse> Installments,
    bool EarlySettlementDiscountEnabled,
    decimal? EarlySettlementDiscountPercentage,
    int EarlySettlementDiscountDays,
    int EarlySettlementDiscountMonths,
    string ReferenceValue,
    byte[] Version);

public sealed record PaymentTermDueDateResponse(
    int Sequence,
    decimal Percentage,
    DateOnly DueDate);

public sealed record PaymentTermDueDatePreviewResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly BaseDate,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    IReadOnlyList<PaymentTermDueDateResponse> DueDates,
    DateOnly? EarlySettlementDiscountDate,
    decimal? EarlySettlementDiscountPercentage,
    string ReferenceValue);

public static class PaymentTermDueDateCalculator
{
    public static IReadOnlyList<PaymentTermDueDateResponse> CalculateDueDates(
        DateOnly baseDate,
        PaymentTermScheduleMode scheduleMode,
        int dueOffsetDays,
        int dueOffsetMonths,
        IReadOnlyList<PaymentTermInstallmentResponse> installments) =>
        scheduleMode == PaymentTermScheduleMode.SingleDueDate
            ? [new PaymentTermDueDateResponse(1, 100m, AddOffset(baseDate, dueOffsetDays, dueOffsetMonths))]
            : installments
                .OrderBy(item => item.Sequence)
                .Select(item => new PaymentTermDueDateResponse(
                    item.Sequence,
                    item.Percentage,
                    AddOffset(baseDate, item.Days, item.Months)))
                .ToArray();

    public static DateOnly CalculateFinalDueDate(
        DateOnly baseDate,
        PaymentTermScheduleMode scheduleMode,
        int dueOffsetDays,
        int dueOffsetMonths,
        IReadOnlyList<PaymentTermInstallmentResponse> installments) =>
        CalculateDueDates(baseDate, scheduleMode, dueOffsetDays, dueOffsetMonths, installments)
            .LastOrDefault()?.DueDate ?? baseDate;

    public static DateOnly AddOffset(DateOnly baseDate, int days, int months) =>
        baseDate.AddMonths(months).AddDays(days);
}

#pragma warning restore CS1591
