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
    UnitOfMeasure = 13,
    PriceList = 14,
    ExchangeRate = 15
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
public sealed record MigrationCanonicalDomainContract(
    MigrationCanonicalRecordType RecordType,
    string ContractVersion,
    string SourceOwner,
    string TargetOwner,
    string SourceSet,
    DateTimeOffset ExtractedAt,
    string Scope,
    string Status,
    string CleansingNote);

public sealed record MigrationCanonicalPackage(
    string PackageVersion,
    string DefinitionId,
    string DefinitionVersion,
    string SourceProfileId,
    string SourceProfileVersion,
    string LogicalDataset,
    MigrationCanonicalSourceSnapshot SourceSnapshot,
    IReadOnlyList<MigrationCanonicalDomainContract>? DomainContracts,
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
    JsonElement Payload,
    string? CorrectionOwner = null);

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
    DateOnly? EffectiveDate = null,
    string? SourceCurrencyCode = null,
    string? TargetCurrencyCode = null) : MigrationCanonicalPayload;

public sealed record MigrationParsedCanonicalRow(
    int SourceSequence,
    string? SourceRecordId,
    MigrationCanonicalRecordType RecordType,
    MigrationCanonicalPayload Payload,
    string PayloadJson,
    bool HasForbiddenControlAccountId = false,
    bool HasForbiddenMonetaryInput = false,
    bool HasForbiddenTargetAuthority = false,
    string? CorrectionOwner = null);

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

    public static MigrationRequestFingerprint ForCorrectedValidation(
        MigrationRunRecord run,
        MigrationIntakeRecord intake,
        string correctionFingerprint) =>
        new(MigrationFingerprintEncoder.Compute(
            Version,
            "corrected-validation",
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            run.TenantId.Value.ToString("D", CultureInfo.InvariantCulture),
            intake.Source.ObjectId.ToString("D", CultureInfo.InvariantCulture),
            intake.Source.Sha256,
            intake.Source.Length.ToString(CultureInfo.InvariantCulture),
            intake.Source.ConcurrencyVersion.ToString(CultureInfo.InvariantCulture),
            correctionFingerprint));

    public static MigrationRequestFingerprint ForDryRun(MigrationRunRecord run, MigrationValidationSummary validation) =>
        new(MigrationFingerprintEncoder.Compute(
            Version,
            "dry-run",
            run.RunId.ToString("D", CultureInfo.InvariantCulture),
            validation.AttemptId.ToString("D", CultureInfo.InvariantCulture),
            validation.PackageHash,
            validation.SourceSnapshotHash));
}

public sealed record MigrationCanonicalDomainContractDefinition(
    MigrationCanonicalRecordType RecordType,
    string Version,
    string Schema);

/// <summary>
/// Normalized package schemas. Every row also requires a unique sourceSequence and stable sourceRecordId.
/// Schema text lists required and optional payload fields; owner references and lifecycle remain authoritative.
/// </summary>
public static class MigrationCanonicalDomainContractCatalog
{
    private static readonly IReadOnlyDictionary<MigrationCanonicalRecordType, MigrationCanonicalDomainContractDefinition> Definitions =
        new Dictionary<MigrationCanonicalRecordType, MigrationCanonicalDomainContractDefinition>
        {
            [MigrationCanonicalRecordType.Product] = new(MigrationCanonicalRecordType.Product, "migration-product-v1", "Required: sku and at least one of nameEnglish/nameArabic. Optional: productId (existing active Product reference), categoryId (existing active Category reference), baseUnitOfMeasureId (existing active UOM reference). SKU is the source identity; target identity and lifecycle are owner-assigned."),
            [MigrationCanonicalRecordType.Supplier] = new(MigrationCanonicalRecordType.Supplier, "migration-supplier-v1", "Required: code and at least one of nameEnglish/nameArabic. Optional: supplierId may reference only an existing active Business Parties record. Code is the source identity; target identity and lifecycle are owner-assigned."),
            [MigrationCanonicalRecordType.Customer] = new(MigrationCanonicalRecordType.Customer, "migration-customer-v1", "Required: code and at least one of nameEnglish/nameArabic. Optional: customerId may reference only an existing active Business Parties record. Code is the source identity; target identity and lifecycle are owner-assigned."),
            [MigrationCanonicalRecordType.Organization] = new(MigrationCanonicalRecordType.Organization, "migration-organization-v1", "Fields: companyId, branchId, warehouseId. At least one is required; branchId requires companyId and warehouseId requires branchId. Values reference existing organization scope; this record validates scope and creates no organization."),
            [MigrationCanonicalRecordType.InventoryOpening] = new(MigrationCanonicalRecordType.InventoryOpening, "migration-inventory-opening-v1", "Required: companyId, branchId, warehouseId, productId, unitOfMeasureId, quantity, unitCost, currencyCode, openingDate and sourceLineReference. Optional: trackingIdentity when required by the Product owner. Organization, Product and UOM values are owner-validated references; controlAccountId is forbidden; quantity conversion is never guessed."),
            [MigrationCanonicalRecordType.GlOpening] = new(MigrationCanonicalRecordType.GlOpening, "migration-gl-opening-v1", "Required: companyId, accountId, debit, credit, currencyCode, openingDate and sourceLineReference. Company and account are Finance references; debit/credit are non-negative one-sided values. Finance owns journals, posting and approval state."),
            [MigrationCanonicalRecordType.ApOpening] = new(MigrationCanonicalRecordType.ApOpening, "migration-ap-opening-v1", "Required: companyId, supplierId, sourceReference, documentDate, openingDate, amount and currencyCode; at least one of dueDate/paymentTermId. Optional: dueDate and paymentTermId when the other is supplied. Company, Supplier and Payment Term are owner-validated references; controlAccountId is forbidden; Finance owns journal and approval state."),
            [MigrationCanonicalRecordType.ArOpening] = new(MigrationCanonicalRecordType.ArOpening, "migration-ar-opening-v1", "Required: companyId, customerId, sourceReference, documentDate, openingDate, amount and currencyCode; at least one of dueDate/paymentTermId. Optional: dueDate and paymentTermId when the other is supplied. Company, Customer and Payment Term are owner-validated references; controlAccountId is forbidden; Finance owns journal and approval state."),
            [MigrationCanonicalRecordType.CashBankOpening] = new(MigrationCanonicalRecordType.CashBankOpening, "migration-cash-bank-opening-v1", "Required: companyId, cashAccountId, sourceReference, amount, currencyCode and openingDate. Company and Cash Account are Finance references; controlAccountId is forbidden; Finance owns journal, posting and approval state."),
            [MigrationCanonicalRecordType.Currency] = new(MigrationCanonicalRecordType.Currency, "migration-currency-v1", "Required: one of code/referenceId identifying an existing active Master Data Currency. Optional: effectiveDate records the source reference date. Rate, precision and target lifecycle remain owner-controlled."),
            [MigrationCanonicalRecordType.Tax] = new(MigrationCanonicalRecordType.Tax, "migration-tax-v1", "Required: one of code/referenceId for an existing active Master Data Tax and effectiveDate selecting exactly one owner rate version. Tax rules and rates remain owner-controlled."),
            [MigrationCanonicalRecordType.PaymentTerm] = new(MigrationCanonicalRecordType.PaymentTerm, "migration-payment-term-v1", "Required: one of code/referenceId for an existing active Master Data Payment Term. Term, schedule and due-date behavior remain owner-controlled."),
            [MigrationCanonicalRecordType.UnitOfMeasure] = new(MigrationCanonicalRecordType.UnitOfMeasure, "migration-unit-of-measure-v1", "Required: one of code/referenceId for an existing active Master Data UOM. Quantity conversion is owner-controlled and is never guessed."),
            [MigrationCanonicalRecordType.PriceList] = new(MigrationCanonicalRecordType.PriceList, "migration-price-list-v1", "Required: one of code/referenceId for an existing active Master Data Price List. Currency, customer scope, prices and lifecycle remain owner-controlled."),
            [MigrationCanonicalRecordType.ExchangeRate] = new(MigrationCanonicalRecordType.ExchangeRate, "migration-exchange-rate-v1", "Required: sourceCurrencyCode, targetCurrencyCode and effectiveDate identifying exactly one active owner rate version. No rate amount, rate version or target lifecycle is accepted from the source.")
        };

    public static IReadOnlyList<MigrationCanonicalDomainContractDefinition> All => Definitions.Values.OrderBy(item => item.RecordType).ToArray();

    public static MigrationCanonicalDomainContractDefinition For(MigrationCanonicalRecordType recordType) => Definitions[recordType];

    public static bool IsCompatible(MigrationCanonicalDomainContract contract) =>
        Definitions.TryGetValue(contract.RecordType, out var definition)
        && string.Equals(contract.ContractVersion, definition.Version, StringComparison.Ordinal);
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
    public const string Version = "migration-package-v2";

    private static readonly HashSet<string> ForbiddenSourceAuthorityFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "recordId", "targetId", "targetRecordId", "targetResourceId", "ownerId", "ownerRecordId", "resultingResourceId",
        "tenantId", "targetTenantId", "postingStatus", "posted", "postedAt", "journalId", "journalStatus",
        "approval", "approvals", "approvalId", "approvalStatus", "approvedAt", "approvedBy", "reconciliationStatus"
    };

    private static readonly HashSet<string> ForbiddenSourceMonetaryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "controlAccountId", "exchangeRate", "exchangeRateId", "rateVersion", "exchangeRateVersion",
        "exchangeRateVersionId", "exchangeRateVersionNumber", "transactionToFunctionalRate", "appliedRate",
        "functionalAmount", "functionalCurrencyCode", "functionalCarryingAmount", "historicalFunctionalAmount",
        "historicalCarryingAmount", "historicalCarryingValue", "reportingCurrencyCode", "reportingAmount",
        "reportingExchangeRateId", "reportingExchangeRateVersionId", "reportingExchangeRateVersionNumber",
        "reportingAppliedRate"
    };

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
            if (package is null || package.SourceSnapshot is null || package.DomainContracts is null || package.Records is null)
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
                if (!IsValidCorrectionOwner(row.CorrectionOwner))
                {
                    return MigrationCanonicalPackageParseResult.Failure(
                        "migration_correction_owner_invalid",
                        "A correction owner must be at most 128 characters and contain no control characters.");
                }

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

                var isOpening = type is (MigrationCanonicalRecordType.InventoryOpening or MigrationCanonicalRecordType.ArOpening or MigrationCanonicalRecordType.ApOpening or MigrationCanonicalRecordType.CashBankOpening or MigrationCanonicalRecordType.GlOpening);
                var hasForbiddenControlAccountId = isOpening && row.Payload.EnumerateObject().Any(item =>
                    string.Equals(item.Name, "controlAccountId", StringComparison.OrdinalIgnoreCase) && item.Value.ValueKind != JsonValueKind.Null);
                var hasForbiddenMonetaryInput = isOpening && row.Payload.EnumerateObject().Any(item =>
                    ForbiddenSourceMonetaryFields.Contains(item.Name) && item.Value.ValueKind != JsonValueKind.Null);
                var hasForbiddenTargetAuthority = row.Payload.EnumerateObject().Any(item =>
                    ForbiddenSourceAuthorityFields.Contains(item.Name) && item.Value.ValueKind != JsonValueKind.Null);
                if (HasUnknownPayloadField(type, row.Payload) && !hasForbiddenTargetAuthority && !hasForbiddenMonetaryInput)
                    return MigrationCanonicalPackageParseResult.Failure(
                        "migration_package_payload_invalid",
                        "A canonical record contains a field that is not in its domain contract.");
                rows.Add(new(
                    row.SourceSequence,
                    row.SourceRecordId,
                    type,
                    payload,
                    JsonSerializer.Serialize(payload, payload.GetType(), Options),
                    HasForbiddenControlAccountId: hasForbiddenControlAccountId,
                    HasForbiddenMonetaryInput: hasForbiddenMonetaryInput,
                    HasForbiddenTargetAuthority: hasForbiddenTargetAuthority,
                    CorrectionOwner: BoundedCorrectionOwner(row.CorrectionOwner)));
            }

            if (!ValidateDomainContracts(package.DomainContracts, rows.Select(item => item.RecordType).Distinct().ToArray(), out var contractFailure))
                return MigrationCanonicalPackageParseResult.Failure(contractFailure.Code, contractFailure.Message);

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
                DomainContracts = package.DomainContracts!.OrderBy(item => item.RecordType).ToArray(),
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

    public static bool TryParsePayload(
        int sourceSequence,
        string? sourceRecordId,
        MigrationCanonicalRecordType type,
        string payloadJson,
        string? correctionOwner,
        out MigrationParsedCanonicalRow? row)
    {
        row = null;
        if (sourceSequence < 1
            || string.IsNullOrWhiteSpace(payloadJson)
            || payloadJson.Length > 2_000_000
            || (correctionOwner is not null
                && (correctionOwner.Trim().Length is 0 or > 128
                    || correctionOwner.Any(char.IsControl))))
            return false;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || DeserializePayload(type, document.RootElement) is not { } payload)
                return false;

            var isOpening = type is MigrationCanonicalRecordType.InventoryOpening
                or MigrationCanonicalRecordType.ArOpening
                or MigrationCanonicalRecordType.ApOpening
                or MigrationCanonicalRecordType.CashBankOpening
                or MigrationCanonicalRecordType.GlOpening;
            var hasForbiddenControlAccountId = isOpening && document.RootElement.EnumerateObject().Any(item =>
                string.Equals(item.Name, "controlAccountId", StringComparison.OrdinalIgnoreCase)
                && item.Value.ValueKind != JsonValueKind.Null);
            var hasForbiddenMonetaryInput = isOpening && document.RootElement.EnumerateObject().Any(item =>
                ForbiddenSourceMonetaryFields.Contains(item.Name)
                && item.Value.ValueKind != JsonValueKind.Null);
            var hasForbiddenTargetAuthority = document.RootElement.EnumerateObject().Any(item =>
                ForbiddenSourceAuthorityFields.Contains(item.Name)
                && item.Value.ValueKind != JsonValueKind.Null);
            if (HasUnknownPayloadField(type, document.RootElement)
                && !hasForbiddenTargetAuthority
                && !hasForbiddenMonetaryInput)
                return false;

            row = new MigrationParsedCanonicalRow(
                sourceSequence,
                sourceRecordId,
                type,
                payload,
                JsonSerializer.Serialize(payload, payload.GetType(), Options),
                hasForbiddenControlAccountId,
                hasForbiddenMonetaryInput,
                hasForbiddenTargetAuthority,
                correctionOwner?.Trim());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool ValidateDomainContracts(
        IReadOnlyList<MigrationCanonicalDomainContract> contracts,
        IReadOnlyList<MigrationCanonicalRecordType> recordTypes,
        out (string Code, string Message) failure)
    {
        if (contracts.Count is 0 or > 32
            || contracts.Any(item => item is null || !Enum.IsDefined(item.RecordType)))
        {
            failure = ("migration_package_domain_contract_required", "A versioned domain source contract is required for each batch.");
            return false;
        }
        if (contracts.Select(item => item.RecordType).Distinct().Count() != contracts.Count
            || !contracts.Select(item => item.RecordType).OrderBy(item => item).SequenceEqual(recordTypes.OrderBy(item => item)))
        {
            failure = ("migration_package_domain_contract_mismatch", "The domain source contracts do not match the records in the package.");
            return false;
        }
        if (contracts.Any(item => !MigrationCanonicalDomainContractCatalog.IsCompatible(item)))
        {
            failure = ("migration_package_domain_contract_incompatible", "A domain source contract version is unknown or incompatible.");
            return false;
        }
        if (contracts.Any(item =>
            !ValidLineageText(item.SourceOwner, 256)
            || !ValidLineageText(item.TargetOwner, 256)
            || !ValidLineageText(item.SourceSet, 512)
            || !ValidLineageText(item.Scope, 512)
            || !ValidLineageText(item.Status, 128)
            || item.CleansingNote is null
            || item.CleansingNote.Length > 2048
            || item.CleansingNote.Any(char.IsControl)
            || item.ExtractedAt == default))
        {
            failure = ("migration_package_domain_lineage_invalid", "A domain source contract is missing bounded lineage metadata.");
            return false;
        }
        failure = default;
        return true;
    }

    private static bool ValidLineageText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximumLength
        && !value.Any(char.IsControl);

    private static bool HasUnknownPayloadField(MigrationCanonicalRecordType type, JsonElement payload)
    {
        string[]? referenceFields = type switch
        {
            MigrationCanonicalRecordType.Currency or MigrationCanonicalRecordType.Tax => ["referenceId", "code", "effectiveDate"],
            MigrationCanonicalRecordType.PaymentTerm or MigrationCanonicalRecordType.UnitOfMeasure or MigrationCanonicalRecordType.PriceList => ["referenceId", "code"],
            MigrationCanonicalRecordType.ExchangeRate => ["sourceCurrencyCode", "targetCurrencyCode", "effectiveDate"],
            _ => null
        };
        if (referenceFields is not null)
            return payload.EnumerateObject().Any(item => !referenceFields.Contains(item.Name, StringComparer.OrdinalIgnoreCase));

        var payloadType = type switch
        {
            MigrationCanonicalRecordType.Product => typeof(MigrationProductPayload),
            MigrationCanonicalRecordType.Supplier => typeof(MigrationSupplierPayload),
            MigrationCanonicalRecordType.Customer => typeof(MigrationCustomerPayload),
            MigrationCanonicalRecordType.Organization => typeof(MigrationOrganizationPayload),
            MigrationCanonicalRecordType.InventoryOpening => typeof(MigrationInventoryOpeningPayload),
            MigrationCanonicalRecordType.GlOpening => typeof(MigrationGlOpeningPayload),
            MigrationCanonicalRecordType.ApOpening => typeof(MigrationApOpeningPayload),
            MigrationCanonicalRecordType.ArOpening => typeof(MigrationArOpeningPayload),
            MigrationCanonicalRecordType.CashBankOpening => typeof(MigrationCashBankOpeningPayload),
            _ => typeof(MigrationReferencePayload)
        };
        var names = payloadType.GetProperties()
            .Select(item => JsonNamingPolicy.CamelCase.ConvertName(item.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return payload.EnumerateObject().Any(item => !names.Contains(item.Name));
    }

    private static string? BoundedCorrectionOwner(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static bool IsValidCorrectionOwner(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || value.Trim().Length <= 128 && !value.Any(char.IsControl);

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
            MigrationCanonicalRecordType.UnitOfMeasure or
            MigrationCanonicalRecordType.PriceList or
            MigrationCanonicalRecordType.ExchangeRate => payload.Deserialize<MigrationReferencePayload>(Options),
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
    IReadOnlyList<string> FindingCodes,
    string? SourceRecordId = null,
    string? CanonicalPayload = null,
    string? CorrectionOwner = null,
    string? ErrorClass = null,
    string? ActionableMessage = null);

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

    public string Outcome => "validation-only";

    public byte[]? RunVersion { get; init; }

    public Guid? OwnerActorId { get; init; }

    public MigrationRunStatus? StageStatus { get; init; }

    public MigrationAttemptOutcome? AttemptOutcome { get; init; }

    public string? Failure { get; init; }

    public string? NextAction { get; init; }
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
    DateTimeOffset CompletedAt)
{
    public string Outcome => "dry-run";
}

public sealed record MigrationNonAuthoritativePreview(
    Guid RunId,
    TenantId TenantId,
    string Outcome,
    int ExpectedAdditions,
    int DuplicateOutcomes,
    int UnresolvedDependencies,
    IReadOnlyDictionary<string, decimal> ControlTotals,
    int Exceptions,
    bool AuthoritativeImport,
    bool ApprovalCreated,
    bool ReadinessCreated,
    bool RunStateChanged,
    IReadOnlyList<MigrationPreviewRow> Rows);

public sealed record MigrationCorrectionSubmission(
    Guid StagedRecordId,
    string CorrectedPayload,
    string CorrectionOwner);

public sealed record StageMigrationPackageCommand(
    Guid RunId,
    Guid SourceObjectId,
    string SourceSnapshotHash,
    string PackageHash,
    string PackageVersion,
    DateTimeOffset CapturedAt,
    IReadOnlyList<MigrationStagedRecord> Records,
    string? DomainContractsJson = null);

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

        if (string.IsNullOrWhiteSpace(row.SourceRecordId)
            || row.SourceRecordId.Length > 256
            || row.SourceRecordId.Any(char.IsControl))
            Required(findings, true, "sourceRecordId");

        if (row.HasForbiddenTargetAuthority)
            findings.Add((MigrationFindingCategory.Reference, "migration_source_target_authority_not_allowed", "The source cannot assign target identifiers, posting state, approvals, or resulting resources."));

        if (row.HasForbiddenMonetaryInput && row.Payload is MigrationInventoryOpeningPayload or MigrationArOpeningPayload or MigrationApOpeningPayload or MigrationCashBankOpeningPayload or MigrationGlOpeningPayload)
            findings.Add((MigrationFindingCategory.FinancialBalance, "migration_opening_source_monetary_fields_not_allowed", "Opening FX identity, rate, or functional carrying values are resolved and owned by Finance; source-supplied monetary evidence is not accepted."));

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
            case MigrationReferencePayload reference when row.RecordType == MigrationCanonicalRecordType.ExchangeRate:
                Required(findings, string.IsNullOrWhiteSpace(reference.SourceCurrencyCode), "sourceCurrencyCode");
                Required(findings, string.IsNullOrWhiteSpace(reference.TargetCurrencyCode), "targetCurrencyCode");
                Required(findings, reference.EffectiveDate is null, "effectiveDate");
                break;
            case MigrationReferencePayload reference when row.RecordType == MigrationCanonicalRecordType.Tax:
                Required(findings, reference.ReferenceId is null && string.IsNullOrWhiteSpace(reference.Code), "reference");
                Required(findings, reference.EffectiveDate is null, "effectiveDate");
                break;
            case MigrationReferencePayload reference:
                Required(findings, reference.ReferenceId is null && string.IsNullOrWhiteSpace(reference.Code), "reference");
                break;
        }

        return findings;
    }

}

#pragma warning restore CS1591
