#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.Contracts.Modules.Migration;

namespace MiniErp.App.Modules.Migration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationCanonicalRecordType
{
    Product = 1,
    Supplier = 2,
    Customer = 3,
    Organization = 4,
    InventoryOpening = 5,
    GlOpening = 6,
    ApOpening = 7,
    ArOpening = 8,
    CashBankOpening = 9,
    Currency = 10,
    Tax = 11,
    PaymentTerm = 12,
    UnitOfMeasure = 13
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationRecordDisposition
{
    Accepted = 1,
    Rejected = 2,
    Quarantined = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationFindingCategory
{
    FileTemplate = 1,
    MandatoryData = 2,
    Duplicate = 3,
    Reference = 4,
    Scope = 5,
    Currency = 6,
    Uom = 7,
    FinancialBalance = 8,
    Unsupported = 9
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationFindingSeverity
{
    Warning = 1,
    Error = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationPlannedAction
{
    Create = 1,
    MatchReference = 2,
    Skip = 3,
    Blocked = 4
}

public enum MigrationReferenceState
{
    NotApplicable = 1,
    Active = 2,
    Missing = 3,
    Inactive = 4,
    Ambiguous = 5,
    Unavailable = 6
}

public sealed record MigrationReferenceCheck(
    MigrationReferenceState State,
    MigrationFindingCategory Category,
    string Code,
    string Message,
    string? ReferenceId = null);

public enum MigrationBusinessIdentityState
{
    NotApplicable = 1,
    Valid = 2,
    Invalid = 3,
    Unavailable = 4
}

public sealed record MigrationBusinessIdentityResolution(
    MigrationBusinessIdentityState State,
    string? Key = null,
    string? Code = null,
    string? Message = null)
{
    public static MigrationBusinessIdentityResolution NotApplicable() => new(MigrationBusinessIdentityState.NotApplicable);

    public static MigrationBusinessIdentityResolution Valid(string key) => new(MigrationBusinessIdentityState.Valid, key);

    public static MigrationBusinessIdentityResolution Invalid(string message) =>
        new(MigrationBusinessIdentityState.Invalid, Code: "migration_business_identity_invalid", Message: message);

    public static MigrationBusinessIdentityResolution Unavailable() =>
        new(MigrationBusinessIdentityState.Unavailable, Code: "migration_reference_authority_unavailable", Message: "The owner-module identity authority is unavailable.");
}

/// <summary>
/// Internal normalized staging contract. It is not a customer transport
/// choice; future source adapters may normalize into this package later.
/// </summary>
public sealed record MigrationCanonicalPackage(
    string PackageVersion,
    string DefinitionId,
    string DefinitionVersion,
    string SourceProfileId,
    string SourceProfileVersion,
    string LogicalDataset,
    MigrationCanonicalSourceSnapshot SourceSnapshot,
    IReadOnlyList<MigrationCanonicalRow> Records);

public sealed record MigrationCanonicalSourceSnapshot(
    Guid ObjectId,
    string Sha256,
    long Length,
    long ConcurrencyVersion);

/// <summary>JSON envelope row. The discriminator is validated before typing.</summary>
public sealed record MigrationCanonicalRow(
    int SourceSequence,
    string? SourceRecordId,
    string RecordType,
    JsonElement Payload);

public abstract record MigrationCanonicalPayload;

public sealed record MigrationProductPayload(
    string? Sku = null,
    string? NameEnglish = null,
    string? NameArabic = null,
    Guid? ProductId = null,
    Guid? CategoryId = null,
    Guid? BaseUnitOfMeasureId = null) : MigrationCanonicalPayload;

public sealed record MigrationSupplierPayload(
    string? Code = null,
    string? NameEnglish = null,
    string? NameArabic = null,
    Guid? SupplierId = null) : MigrationCanonicalPayload;

public sealed record MigrationCustomerPayload(
    string? Code = null,
    string? NameEnglish = null,
    string? NameArabic = null,
    Guid? CustomerId = null) : MigrationCanonicalPayload;

public sealed record MigrationOrganizationPayload(
    Guid? CompanyId = null,
    Guid? BranchId = null,
    Guid? WarehouseId = null) : MigrationCanonicalPayload;

public sealed record MigrationInventoryOpeningPayload(
    Guid? CompanyId = null,
    Guid? BranchId = null,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? UnitOfMeasureId = null,
    decimal? Quantity = null,
    decimal? UnitCost = null,
    string? CurrencyCode = null,
    DateOnly? OpeningDate = null,
    Guid? ControlAccountId = null,
    string? TrackingIdentity = null,
    string? SourceLineReference = null) : MigrationCanonicalPayload;

public sealed record MigrationGlOpeningPayload(
    Guid? CompanyId = null,
    Guid? AccountId = null,
    decimal? Debit = null,
    decimal? Credit = null,
    string? CurrencyCode = null,
    DateOnly? OpeningDate = null,
    string? SourceLineReference = null) : MigrationCanonicalPayload;

public sealed record MigrationApOpeningPayload(
    Guid? CompanyId = null,
    Guid? SupplierId = null,
    Guid? ControlAccountId = null,
    decimal? Amount = null,
    string? CurrencyCode = null,
    DateOnly? OpeningDate = null,
    string? SourceReference = null,
    DateOnly? DocumentDate = null,
    DateOnly? DueDate = null,
    Guid? PaymentTermId = null) : MigrationCanonicalPayload;

public sealed record MigrationArOpeningPayload(
    Guid? CompanyId = null,
    Guid? CustomerId = null,
    string? SourceReference = null,
    DateOnly? DocumentDate = null,
    DateOnly? OpeningDate = null,
    decimal? Amount = null,
    string? CurrencyCode = null,
    DateOnly? DueDate = null,
    Guid? PaymentTermId = null) : MigrationCanonicalPayload;

public sealed record MigrationCashBankOpeningPayload(
    Guid? CompanyId = null,
    Guid? CashAccountId = null,
    string? SourceReference = null,
    decimal? Amount = null,
    string? CurrencyCode = null,
    DateOnly? OpeningDate = null) : MigrationCanonicalPayload
{
    // Legacy parser visibility only; Finance owns the linked posting account.
    public Guid? ControlAccountId { get; init; }
}

public sealed record MigrationReferencePayload(
    Guid? ReferenceId = null,
    string? Code = null,
    DateOnly? EffectiveDate = null) : MigrationCanonicalPayload;

public sealed record MigrationParsedCanonicalRow(
    int SourceSequence,
    string? SourceRecordId,
    MigrationCanonicalRecordType RecordType,
    MigrationCanonicalPayload Payload,
    string PayloadJson,
    bool HasForbiddenControlAccountId = false);

/// <summary>Length-prefixed SHA-256 encoding shared by Migration operations.</summary>
internal static class MigrationFingerprintEncoder
{
    public static string Compute(string version, params string?[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, version);
        foreach (var value in values)
            Append(hash, value);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        Span<byte> length = stackalloc byte[4];
        BitConverter.TryWriteBytes(length, bytes.Length);
        if (BitConverter.IsLittleEndian)
            length.Reverse();
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}

/// <summary>Canonical operation identities for validation and dry-run replay.</summary>
public static class MigrationValidationFingerprint
{
    public const string Version = "migration-validation-v1";

    public static MigrationRequestFingerprint ForValidation(MigrationRunRecord run, MigrationIntakeRecord intake) =>
        new(MigrationFingerprintEncoder.Compute(
            Version,
            "validation",
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            run.TenantId.Value.ToString("D", CultureInfo.InvariantCulture),
            run.Definition.DefinitionId,
            run.Definition.Version,
            run.SourceProfile.ProfileId,
            run.SourceProfile.ProfileVersion,
            intake.Source.ObjectId.ToString("D", CultureInfo.InvariantCulture),
            intake.Source.Sha256,
            intake.Source.Length.ToString(CultureInfo.InvariantCulture),
            intake.Source.ConcurrencyVersion.ToString(CultureInfo.InvariantCulture)));

    public static MigrationRequestFingerprint ForDryRun(MigrationRunRecord run, MigrationValidationSummary validation) =>
        new(MigrationFingerprintEncoder.Compute(
            Version,
            "dry-run",
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            validation.AttemptId.ToString("D", CultureInfo.InvariantCulture),
            validation.PackageHash,
            validation.SourceSnapshotHash));
}

public sealed record MigrationCanonicalPackageParseResult(
    MigrationCanonicalPackage? Package,
    IReadOnlyList<MigrationParsedCanonicalRow> Rows,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool Succeeded => Package is not null && ErrorCode is null;

    public static MigrationCanonicalPackageParseResult Failure(string code, string message) =>
        new(null, [], code, message);
}

public static class MigrationCanonicalPackageParser
{
    public const string Version = "migration-package-v1";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    public static MigrationCanonicalPackageParseResult Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return MigrationCanonicalPackageParseResult.Failure(
                "migration_package_empty",
                "The canonical package is empty.");
        }

        try
        {
            var package = JsonSerializer.Deserialize<MigrationCanonicalPackage>(bytes.Span, Options);
            if (package is null || package.SourceSnapshot is null || package.Records is null)
            {
                return MigrationCanonicalPackageParseResult.Failure(
                    "migration_package_metadata_missing",
                    "The canonical package metadata is incomplete.");
            }

            if (package.Records.Count > 100_000
                || package.Records.Any(item => item.SourceSequence < 1 || string.IsNullOrWhiteSpace(item.RecordType))
                || package.Records.Select(item => item.SourceSequence).Distinct().Count() != package.Records.Count)
            {
                return MigrationCanonicalPackageParseResult.Failure(
                    "migration_package_rows_invalid",
                    "The canonical package contains invalid or duplicate row sequence values.");
            }

            var rows = new List<MigrationParsedCanonicalRow>(package.Records.Count);
            foreach (var row in package.Records)
            {
                if (!Enum.TryParse<MigrationCanonicalRecordType>(row.RecordType, ignoreCase: true, out var type)
                    || !Enum.IsDefined(type))
                {
                    return MigrationCanonicalPackageParseResult.Failure(
                        "migration_package_record_type_unsupported",
                        "The canonical package contains an unsupported record type.");
                }

                var payload = DeserializePayload(type, row.Payload);
                if (payload is null)
                {
                    return MigrationCanonicalPackageParseResult.Failure(
                        "migration_package_payload_invalid",
                        "A canonical record payload is invalid.");
                }

                rows.Add(new(
                    row.SourceSequence,
                    row.SourceRecordId,
                    type,
                    payload,
                    JsonSerializer.Serialize(payload, payload.GetType(), Options),
                    type is (MigrationCanonicalRecordType.ArOpening or MigrationCanonicalRecordType.ApOpening or MigrationCanonicalRecordType.CashBankOpening)
                        && row.Payload.EnumerateObject().Any(item => string.Equals(item.Name, "controlAccountId", StringComparison.OrdinalIgnoreCase) && item.Value.ValueKind != JsonValueKind.Null)));
            }

            return new(package, rows, null, null);
        }
        catch (JsonException)
        {
            return MigrationCanonicalPackageParseResult.Failure(
                "migration_package_invalid_json",
                "The canonical package is not valid JSON.");
        }
        catch (NotSupportedException)
        {
            return MigrationCanonicalPackageParseResult.Failure(
                "migration_package_payload_invalid",
                "The canonical package contains an unsupported value.");
        }
        catch (OverflowException)
        {
            return MigrationCanonicalPackageParseResult.Failure(
                "migration_package_payload_invalid",
                "The canonical package contains an out-of-range value.");
        }
    }

    public static string Hash(MigrationCanonicalPackage package, IReadOnlyList<MigrationParsedCanonicalRow> rows)
    {
        var normalized = JsonSerializer.Serialize(
            new
            {
                package.PackageVersion,
                package.DefinitionId,
                package.DefinitionVersion,
                package.SourceProfileId,
                package.SourceProfileVersion,
                package.LogicalDataset,
                package.SourceSnapshot,
                Records = rows.Select(item => new
                {
                    item.SourceSequence,
                    item.SourceRecordId,
                    RecordType = item.RecordType.ToString(),
                    Payload = ParseElement(item.PayloadJson)
                }).ToArray()
            },
            Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static MigrationCanonicalPayload? DeserializePayload(
        MigrationCanonicalRecordType type,
        JsonElement payload) => type switch
        {
            MigrationCanonicalRecordType.Product => payload.Deserialize<MigrationProductPayload>(Options),
            MigrationCanonicalRecordType.Supplier => payload.Deserialize<MigrationSupplierPayload>(Options),
            MigrationCanonicalRecordType.Customer => payload.Deserialize<MigrationCustomerPayload>(Options),
            MigrationCanonicalRecordType.Organization => payload.Deserialize<MigrationOrganizationPayload>(Options),
            MigrationCanonicalRecordType.InventoryOpening => payload.Deserialize<MigrationInventoryOpeningPayload>(Options),
            MigrationCanonicalRecordType.GlOpening => payload.Deserialize<MigrationGlOpeningPayload>(Options),
            MigrationCanonicalRecordType.ApOpening => payload.Deserialize<MigrationApOpeningPayload>(Options),
            MigrationCanonicalRecordType.ArOpening => payload.Deserialize<MigrationArOpeningPayload>(Options),
            MigrationCanonicalRecordType.CashBankOpening => payload.Deserialize<MigrationCashBankOpeningPayload>(Options),
            MigrationCanonicalRecordType.Currency or
            MigrationCanonicalRecordType.Tax or
            MigrationCanonicalRecordType.PaymentTerm or
            MigrationCanonicalRecordType.UnitOfMeasure => payload.Deserialize<MigrationReferencePayload>(Options),
            _ => null
        };
}

public sealed record MigrationStagedRecord(
    Guid StagedRecordId,
    TenantId TenantId,
    Guid RunId,
    int SourceSequence,
    string? SourceRecordId,
    MigrationCanonicalRecordType RecordType,
    string CanonicalPayload,
    string PayloadHash,
    string PackageHash,
    string PackageVersion,
    Guid SourceObjectId,
    string SourceSnapshotHash,
    DateTimeOffset CapturedAt);

public sealed record MigrationValidationFinding(
    Guid FindingId,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    Guid? StagedRecordId,
    MigrationFindingCategory Category,
    MigrationFindingSeverity Severity,
    bool IsBlocking,
    string Code,
    string Message,
    string? ReferenceId,
    DateTimeOffset CreatedAt);

public sealed record MigrationValidationRecordResult(
    Guid StagedRecordId,
    int SourceSequence,
    MigrationCanonicalRecordType RecordType,
    MigrationRecordDisposition Disposition,
    IReadOnlyList<string> FindingCodes);

public sealed record MigrationValidationSummary(
    Guid ValidationResultId,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    string PackageHash,
    string SourceSnapshotHash,
    int TotalStagedRecords,
    int AcceptedCount,
    int RejectedCount,
    int QuarantinedCount,
    IReadOnlyDictionary<string, int> FindingCounts,
    IReadOnlyList<MigrationValidationRecordResult> Records,
    DateTimeOffset CompletedAt)
{
    public bool IsValid => RejectedCount == 0 && QuarantinedCount == 0;
}

public sealed record MigrationPreviewRow(
    Guid StagedRecordId,
    int SourceSequence,
    MigrationCanonicalRecordType RecordType,
    MigrationRecordDisposition Disposition,
    MigrationPlannedAction PlannedAction,
    string? Projection);

public sealed record MigrationDryRunPreview(
    Guid PreviewId,
    TenantId TenantId,
    Guid RunId,
    Guid AttemptId,
    Guid ValidationAttemptId,
    string PackageHash,
    string SourceSnapshotHash,
    int TotalStagedRecords,
    int AcceptedCount,
    int RejectedCount,
    int QuarantinedCount,
    IReadOnlyDictionary<string, int> FindingCounts,
    IReadOnlyDictionary<string, decimal> ControlTotals,
    int UnresolvedDependencyCount,
    int ExceptionCount,
    IReadOnlyList<MigrationPreviewRow> Rows,
    DateTimeOffset CompletedAt);

public sealed record StageMigrationPackageCommand(
    Guid RunId,
    Guid SourceObjectId,
    string SourceSnapshotHash,
    string PackageHash,
    string PackageVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MigrationStagedRecord> Records);

public sealed record MigrationStagingResult(
    string PackageHash,
    IReadOnlyList<MigrationStagedRecord> Records);

public sealed record SaveMigrationValidationCommand(
    MigrationValidationSummary Summary,
    IReadOnlyList<MigrationValidationFinding> Findings);

public sealed record SaveMigrationDryRunCommand(MigrationDryRunPreview Preview);

public interface IMigrationValidationPersistence
{
    Task<MigrationIntakeRecord?> FindIntakeAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationStagingResult>> StagePackageAsync(
        TenantContext tenantContext,
        StageMigrationPackageCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationValidationSummary>> SaveValidationAsync(
        TenantContext tenantContext,
        SaveMigrationValidationCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationValidationSummary?> FindValidationAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<MigrationValidationSummary?> FindLatestValidationAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationValidationFinding>> ListFindingsAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid? attemptId = null,
        int offset = 0,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationStagedRecord>> ListStagedRecordsAsync(
        TenantContext tenantContext,
        Guid runId,
        int offset = 0,
        int pageSize = 100,
        CancellationToken cancellationToken = default);

    Task<MigrationPersistenceResult<MigrationDryRunPreview>> SaveDryRunAsync(
        TenantContext tenantContext,
        SaveMigrationDryRunCommand command,
        CancellationToken cancellationToken = default);

    Task<MigrationDryRunPreview?> FindDryRunAsync(
        TenantContext tenantContext,
        Guid runId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<MigrationDryRunPreview?> FindLatestDryRunAsync(
        TenantContext tenantContext,
        Guid runId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Migration asks owners for reference truth through this narrow read port.
/// It never opens or queries a sibling module DbContext itself.
/// </summary>
public interface IMigrationReferenceAuthority
{
    Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(
        FoundationRequestContext requestContext,
        MigrationParsedCanonicalRow row,
        CancellationToken cancellationToken = default);

    MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) =>
        MigrationBusinessIdentityResolution.NotApplicable();
}

public sealed class UnavailableMigrationReferenceAuthority : IMigrationReferenceAuthority
{
    public Task<IReadOnlyList<MigrationReferenceCheck>> ValidateAsync(
        FoundationRequestContext requestContext,
        MigrationParsedCanonicalRow row,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MigrationReferenceCheck>>(
            [new(MigrationReferenceState.Unavailable, MigrationFindingCategory.Reference, "migration_reference_authority_unavailable", "The owner-module reference authority is unavailable.")]);

    public MigrationBusinessIdentityResolution ResolveBusinessIdentity(MigrationParsedCanonicalRow row) =>
        row.Payload is MigrationProductPayload or MigrationSupplierPayload or MigrationCustomerPayload
            ? MigrationBusinessIdentityResolution.Unavailable()
            : MigrationBusinessIdentityResolution.NotApplicable();
}

public static class MigrationValidationRules
{
    public static IReadOnlyList<(MigrationFindingCategory Category, string Code, string Message)> Validate(
        MigrationParsedCanonicalRow row)
    {
        var findings = new List<(MigrationFindingCategory, string, string)>();
        static void Required(List<(MigrationFindingCategory, string, string)> target, bool missing, string field) {
            if (missing) target.Add((MigrationFindingCategory.MandatoryData, "migration_required_field_missing", $"A required field is missing: {field}."));
        }

        switch (row.Payload)
        {
            case MigrationProductPayload product:
                Required(findings, string.IsNullOrWhiteSpace(product.Sku), "sku");
                Required(findings, string.IsNullOrWhiteSpace(product.NameEnglish) && string.IsNullOrWhiteSpace(product.NameArabic), "name");
                break;
            case MigrationSupplierPayload supplier:
                Required(findings, string.IsNullOrWhiteSpace(supplier.Code), "code");
                Required(findings, string.IsNullOrWhiteSpace(supplier.NameEnglish) && string.IsNullOrWhiteSpace(supplier.NameArabic), "name");
                break;
            case MigrationCustomerPayload customer:
                Required(findings, string.IsNullOrWhiteSpace(customer.Code), "code");
                Required(findings, string.IsNullOrWhiteSpace(customer.NameEnglish) && string.IsNullOrWhiteSpace(customer.NameArabic), "name");
                break;
            case MigrationOrganizationPayload organization:
                Required(findings, organization.CompanyId is null && organization.BranchId is null && organization.WarehouseId is null, "organization");
                Required(findings, organization.BranchId is not null && organization.CompanyId is null, "companyId");
                Required(findings, organization.WarehouseId is not null && organization.BranchId is null, "branchId");
                break;
            case MigrationInventoryOpeningPayload inventory:
                Required(findings, inventory.CompanyId is null, "companyId");
                Required(findings, inventory.WarehouseId is not null && inventory.BranchId is null, "branchId");
                Required(findings, inventory.WarehouseId is null, "warehouseId");
                Required(findings, inventory.ProductId is null, "productId");
                Required(findings, inventory.UnitOfMeasureId is null, "unitOfMeasureId");
                Required(findings, inventory.Quantity is null, "quantity");
                Required(findings, inventory.UnitCost is null, "unitCost");
                Required(findings, string.IsNullOrWhiteSpace(inventory.CurrencyCode), "currencyCode");
                Required(findings, inventory.OpeningDate is null, "openingDate");
                Required(findings, inventory.ControlAccountId is not null, "controlAccountId");
                Required(findings, string.IsNullOrWhiteSpace(inventory.SourceLineReference), "sourceLineReference");
                if (inventory.Quantity < 0m) findings.Add((MigrationFindingCategory.MandatoryData, "migration_quantity_invalid", "Opening quantity cannot be negative."));
                if (inventory.UnitCost < 0m) findings.Add((MigrationFindingCategory.MandatoryData, "migration_amount_invalid", "Opening unit cost cannot be negative."));
                break;
            case MigrationGlOpeningPayload gl:
                Required(findings, gl.CompanyId is null, "companyId");
                Required(findings, gl.AccountId is null, "accountId");
                Required(findings, gl.Debit is null, "debit");
                Required(findings, gl.Credit is null, "credit");
                Required(findings, string.IsNullOrWhiteSpace(gl.CurrencyCode), "currencyCode");
                Required(findings, gl.OpeningDate is null, "openingDate");
                Required(findings, string.IsNullOrWhiteSpace(gl.SourceLineReference), "sourceLineReference");
                if (gl.Debit < 0m || gl.Credit < 0m || gl.Debit > 0m && gl.Credit > 0m)
                    findings.Add((MigrationFindingCategory.FinancialBalance, "migration_debit_credit_invalid", "A GL opening line must contain one non-negative side."));
                if (gl.Debit == 0m && gl.Credit == 0m)
                    findings.Add((MigrationFindingCategory.FinancialBalance, "migration_gl_opening_zero_amount", "A GL opening line must contain a non-zero amount."));
                break;
            case MigrationApOpeningPayload ap:
                Required(findings, ap.CompanyId is null, "companyId");
                Required(findings, ap.SupplierId is null, "supplierId");
                Required(findings, string.IsNullOrWhiteSpace(ap.SourceReference), "sourceReference");
                Required(findings, ap.DocumentDate is null, "documentDate");
                Required(findings, ap.Amount is null, "amount");
                Required(findings, string.IsNullOrWhiteSpace(ap.CurrencyCode), "currencyCode");
                Required(findings, ap.OpeningDate is null, "openingDate");
                Required(findings, ap.DueDate is null && ap.PaymentTermId is null, "dueDate or paymentTermId");
                if (ap.Amount <= 0m) findings.Add((MigrationFindingCategory.FinancialBalance, "migration_amount_invalid", "Opening amount must be greater than zero."));
                if (row.HasForbiddenControlAccountId)
                    findings.Add((MigrationFindingCategory.FinancialBalance, "migration_ap_control_account_not_allowed", "AP opening control-account selection is owned by Finance and is not accepted in the migration payload."));
                break;
            case MigrationArOpeningPayload ar:
                Required(findings, ar.CompanyId is null, "companyId");
                Required(findings, ar.CustomerId is null, "customerId");
                Required(findings, string.IsNullOrWhiteSpace(ar.SourceReference), "sourceReference");
                Required(findings, ar.DocumentDate is null, "documentDate");
                Required(findings, ar.OpeningDate is null, "openingDate");
                Required(findings, ar.Amount is null, "amount");
                Required(findings, string.IsNullOrWhiteSpace(ar.CurrencyCode), "currencyCode");
                Required(findings, ar.DueDate is null && ar.PaymentTermId is null, "dueDate or paymentTermId");
                if (ar.Amount <= 0m) findings.Add((MigrationFindingCategory.FinancialBalance, "migration_amount_invalid", "Opening amount must be greater than zero."));
                if (row.HasForbiddenControlAccountId)
                    findings.Add((MigrationFindingCategory.FinancialBalance, "migration_ar_control_account_not_allowed", "AR opening control-account selection is owned by Finance and is not accepted in the migration payload."));
                break;
            case MigrationCashBankOpeningPayload cash:
                Required(findings, cash.CompanyId is null, "companyId");
                Required(findings, cash.CashAccountId is null, "cashAccountId");
                Required(findings, string.IsNullOrWhiteSpace(cash.SourceReference), "sourceReference");
                Required(findings, cash.Amount is null, "amount");
                Required(findings, string.IsNullOrWhiteSpace(cash.CurrencyCode), "currencyCode");
                Required(findings, cash.OpeningDate is null, "openingDate");
                if (cash.Amount < 0m) findings.Add((MigrationFindingCategory.FinancialBalance, "migration_amount_invalid", "Opening amount cannot be negative."));
                if (cash.Amount == 0m) findings.Add((MigrationFindingCategory.FinancialBalance, "migration_cash_bank_opening_zero_amount", "Opening amount must be positive; zero does not create an economic effect."));
                if (row.HasForbiddenControlAccountId || cash.ControlAccountId is not null)
                    findings.Add((MigrationFindingCategory.FinancialBalance, "migration_cash_bank_control_account_not_allowed", "Cash/Bank opening control-account selection is owned by Finance and is not accepted in the migration payload."));
                break;
            case MigrationReferencePayload reference:
                Required(findings, reference.ReferenceId is null && string.IsNullOrWhiteSpace(reference.Code), "reference");
                break;
        }

        return findings;
    }

}

#pragma warning restore CS1591
