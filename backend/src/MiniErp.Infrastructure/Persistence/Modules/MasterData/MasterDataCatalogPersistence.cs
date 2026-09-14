#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-bound persistence adapter for M95-SL-02. The adapter owns the
/// Master Data context, mappings, transaction boundary, and append-before-
/// effect audit write; it exposes only the application persistence contract.
/// </summary>
public sealed class MasterDataCatalogPersistence : IMasterDataCatalogPersistence, IProductIdentityPersistence
{
    private readonly DbContextOptions options;

    internal MasterDataCatalogPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MasterDataCategoryRecord>> ListCategoriesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.Categories
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => ToCategoryRecord(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<MasterDataCategoryRecord?> FindCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        return entity is null ? null : ToCategoryRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> CreateCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CreateMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                if (await db.Categories.AnyAsync(item =>
                    item.Code == command.Code || item.NameKey == MasterDataCategoryUomValuePolicy.NameKey(command.Name),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "category_duplicate");
                }

                if (command.ParentCategoryId is { } parentId
                    && !await db.Categories.AnyAsync(item => item.Id == parentId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "parent_category_not_found");
                }

                var entity = new MasterDataCategoryEntity(
                    categoryId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name,
                    command.ParentCategoryId,
                    command.TrackingDefaultEnabled);
                db.Categories.Add(entity);
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> EditCategoryAsync(
        TenantContext tenantContext,
        EditMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var entity = await db.Categories.SingleOrDefaultAsync(
                    item => item.Id == command.CategoryId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "category_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.Categories.AnyAsync(item =>
                    item.Id != command.CategoryId
                    && (item.Code == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "category_duplicate");
                }

                if (command.ParentCategoryId is { } parentId
                    && !await db.Categories.AnyAsync(item => item.Id == parentId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "parent_category_not_found");
                }

                entity.Edit(
                    command.Code,
                    command.Name,
                    command.ParentCategoryId,
                    command.TrackingDefaultEnabled);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> SetCategoryLifecycleAsync(
        TenantContext tenantContext,
        Guid categoryId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "concurrency_conflict",
            async db =>
            {
                var entity = await db.Categories.SingleOrDefaultAsync(
                    item => item.Id == categoryId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "category_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<MasterDataUnitOfMeasureRecord>> ListUnitsOfMeasureAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => ToUnitRecord(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<MasterDataUnitOfMeasureRecord?> FindUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == unitOfMeasureId, cancellationToken);
        return entity is null ? null : ToUnitRecord(entity);
    }

    public async Task<MasterDataConversionRecord?> FindConversionAsync(
        TenantContext tenantContext,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Conversions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.FromUnitOfMeasureId == fromUnitOfMeasureId
                    && item.ToUnitOfMeasureId == toUnitOfMeasureId,
                cancellationToken);
        return entity is null ? null : ToConversionRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> CreateUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CreateMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                    item.Code == command.Code || item.NameKey == nameKey,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "uom_duplicate");
                }

                var entity = new MasterDataUnitOfMeasureEntity(
                    unitOfMeasureId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name);
                db.UnitsOfMeasure.Add(entity);
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> EditUnitOfMeasureAsync(
        TenantContext tenantContext,
        EditMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var entity = await db.UnitsOfMeasure.SingleOrDefaultAsync(
                    item => item.Id == command.UnitOfMeasureId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "uom_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                    item.Id != command.UnitOfMeasureId
                    && (item.Code == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "uom_duplicate");
                }

                entity.Edit(command.Code, command.Name);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> SetUnitOfMeasureLifecycleAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "concurrency_conflict",
            async db =>
            {
                var entity = await db.UnitsOfMeasure.SingleOrDefaultAsync(
                    item => item.Id == unitOfMeasureId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "uom_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (lifecycleState == MasterDataLifecycleState.Inactive
                    && await db.Conversions.AnyAsync(item =>
                        item.FromUnitOfMeasureId == unitOfMeasureId
                        || item.ToUnitOfMeasureId == unitOfMeasureId,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.InUse,
                        "uom_in_use");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataConversionRecord>> CreateConversionAsync(
        TenantContext tenantContext,
        Guid conversionId,
        CreateMasterDataConversionCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                        item.Id == command.FromUnitOfMeasureId
                        && item.LifecycleState != MasterDataLifecycleState.Active,
                    cancellationToken)
                    || await db.UnitsOfMeasure.AnyAsync(item =>
                        item.Id == command.ToUnitOfMeasureId
                        && item.LifecycleState != MasterDataLifecycleState.Active,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "conversion_reference_inactive");
                }

                if (!await db.UnitsOfMeasure.AnyAsync(item => item.Id == command.FromUnitOfMeasureId, cancellationToken)
                    || !await db.UnitsOfMeasure.AnyAsync(item => item.Id == command.ToUnitOfMeasureId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "conversion_reference_invalid");
                }

                if (await db.Conversions.AnyAsync(item =>
                    item.FromUnitOfMeasureId == command.FromUnitOfMeasureId
                    && item.ToUnitOfMeasureId == command.ToUnitOfMeasureId,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "conversion_duplicate");
                }

                var entity = new MasterDataConversionEntity(
                    conversionId,
                    tenantContext.TenantId,
                    command.FromUnitOfMeasureId,
                    command.ToUnitOfMeasureId,
                    command.Factor);
                db.Conversions.Add(entity);
                return MasterDataPersistenceResult<MasterDataConversionRecord>.Success(ToConversionRecord(entity));
            },
            cancellationToken);
    }

    public async Task<MasterDataQuantityConversionResult> ConvertQuantityAsync(
        TenantContext tenantContext,
        Guid conversionId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var conversion = await db.Conversions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversionId, cancellationToken);
        if (conversion is null)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_not_found", null);
        }

        var source = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversion.FromUnitOfMeasureId, cancellationToken);
        var target = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversion.ToUnitOfMeasureId, cancellationToken);
        if (source is null || target is null)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_reference_invalid", null);
        }

        if (source.LifecycleState != MasterDataLifecycleState.Active
            || target.LifecycleState != MasterDataLifecycleState.Active)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_reference_inactive", null);
        }

        try
        {
            return new MasterDataQuantityConversionResult(
                true,
                "calculated",
                MasterDataCategoryUomValuePolicy.Calculate(quantity, conversion.Factor));
        }
        catch (ArgumentException)
        {
            return new MasterDataQuantityConversionResult(false, "precision_invalid", null);
        }
    }

    public async Task<bool> HasActiveConversionReferenceAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.Conversions.AnyAsync(item =>
            item.FromUnitOfMeasureId == unitOfMeasureId
            || item.ToUnitOfMeasureId == unitOfMeasureId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProductIdentityRecord>> ListProductsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.Products
            .AsNoTracking()
            .OrderBy(item => item.Sku)
            .ToListAsync(cancellationToken);
        var records = new List<ProductIdentityRecord>(entities.Count);
        foreach (var entity in entities)
        {
            records.Add(await ToProductRecordAsync(db, entity, cancellationToken));
        }

        return records;
    }

    public async Task<ProductIdentityRecord?> FindProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);
        return entity is null
            ? null
            : await ToProductRecordAsync(db, entity, cancellationToken);
    }

    public async Task<ProductReferenceValidation> ValidateReferencesAsync(
        TenantContext tenantContext,
        Guid categoryId,
        Guid baseUnitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await ReadProductReferencesAsync(
            db,
            categoryId,
            baseUnitOfMeasureId,
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> CreateProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CreateProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(references);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var currentReferences = await ReadProductReferencesAsync(
                    db,
                    command.CategoryId,
                    command.BaseUnitOfMeasureId,
                    cancellationToken);
                if (!currentReferences.IsValid)
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        currentReferences.Code);
                }

                var skuKey = ProductIdentityValuePolicy.ComparisonKey(command.Sku);
                if (await db.Products.AnyAsync(item => item.SkuKey == skuKey, cancellationToken))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "product_duplicate");
                }

                var normalizedBarcodes = (command.Barcodes ?? [])
                    .Select(ProductIdentityValuePolicy.NormalizeBarcode)
                    .ToArray();
                var barcodeKeys = normalizedBarcodes
                    .Select(ProductIdentityValuePolicy.ComparisonKey)
                    .ToArray();
                if (barcodeKeys.Distinct(StringComparer.Ordinal).Count() != barcodeKeys.Length
                    || await db.ProductBarcodes.AnyAsync(
                        item => barcodeKeys.Contains(item.ComparisonKey),
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "product_barcode_duplicate");
                }

                var entity = new MasterDataProductEntity(
                    productId,
                    tenantContext.TenantId,
                    command.Sku,
                    command.Name,
                    command.Description,
                    command.CategoryId,
                    command.BaseUnitOfMeasureId,
                    command.TrackingEnabledOverride,
                    command.IsSellable,
                    command.IsPurchasable,
                    command.IsInventoryRelevant);
                db.Products.Add(entity);
                var barcodeEntities = normalizedBarcodes
                    .Select(value => new MasterDataProductBarcodeEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        productId,
                        value))
                    .ToArray();
                db.ProductBarcodes.AddRange(barcodeEntities);
                return MasterDataPersistenceResult<ProductIdentityRecord>.Success(
                    await ToProductRecordAsync(db, entity, cancellationToken));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> EditProductAsync(
        TenantContext tenantContext,
        EditProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(references);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var entity = await db.Products.SingleOrDefaultAsync(
                    item => item.Id == command.ProductId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "product_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var currentReferences = await ReadProductReferencesAsync(
                    db,
                    command.CategoryId,
                    command.BaseUnitOfMeasureId,
                    cancellationToken);
                if (!currentReferences.IsValid)
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        currentReferences.Code);
                }

                var skuKey = ProductIdentityValuePolicy.ComparisonKey(command.Sku);
                if (await db.Products.AnyAsync(
                        item => item.Id != command.ProductId && item.SkuKey == skuKey,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "product_duplicate");
                }

                var normalizedBarcodes = (command.Barcodes ?? [])
                    .Select(ProductIdentityValuePolicy.NormalizeBarcode)
                    .ToArray();
                var barcodeKeys = normalizedBarcodes
                    .Select(ProductIdentityValuePolicy.ComparisonKey)
                    .ToArray();
                if (barcodeKeys.Distinct(StringComparer.Ordinal).Count() != barcodeKeys.Length
                    || await db.ProductBarcodes.AnyAsync(
                        item => item.ProductId != command.ProductId
                            && barcodeKeys.Contains(item.ComparisonKey),
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "product_barcode_duplicate");
                }

                entity.Edit(
                    command.Sku,
                    command.Name,
                    command.Description,
                    command.CategoryId,
                    command.BaseUnitOfMeasureId,
                    command.TrackingEnabledOverride,
                    command.IsSellable,
                    command.IsPurchasable,
                    command.IsInventoryRelevant);
                entity.TouchVersion();

                var existingBarcodes = await db.ProductBarcodes
                    .Where(item => item.ProductId == command.ProductId)
                    .ToListAsync(cancellationToken);
                db.ProductBarcodes.RemoveRange(existingBarcodes);
                db.ProductBarcodes.AddRange(normalizedBarcodes.Select(value =>
                    new MasterDataProductBarcodeEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        command.ProductId,
                        value)));

                return MasterDataPersistenceResult<ProductIdentityRecord>.Success(
                    await ToProductRecordAsync(db, entity, cancellationToken));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> SetProductLifecycleAsync(
        TenantContext tenantContext,
        Guid productId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "concurrency_conflict",
            async db =>
            {
                var entity = await db.Products.SingleOrDefaultAsync(
                    item => item.Id == productId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "product_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<ProductIdentityRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "product_lifecycle_no_change");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<ProductIdentityRecord>.Success(
                    await ToProductRecordAsync(db, entity, cancellationToken));
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid productId,
        CancellationToken cancellationToken = default) =>
        ReadAuditHistoryAsync(
            tenantContext,
            MasterDataResourceKind.Product,
            productId,
            cancellationToken);

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "audit_duplicate",
            db => Task.FromResult(
                MasterDataPersistenceResult<MasterDataAuditRecord>.Success(
                    ToAuditRecord(new MasterDataAuditEventEntity(evidence)))),
            cancellationToken,
            addEffect: false);
    }

    public async Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == resourceKind);
        if (resourceId is { } id)
        {
            query = query.Where(item => item.ResourceId == id);
        }

        var events = await query.ToListAsync(cancellationToken);
        return events
            .OrderByDescending(item => item.OccurredAt)
            .Select(ToAuditRecord)
            .ToArray();
    }

    private async Task<MasterDataPersistenceResult<T>> CommitAsync<T>(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        string databaseFailureCode,
        Func<MasterDataDbContext, Task<MasterDataPersistenceResult<T>>> operation,
        CancellationToken cancellationToken,
        bool addEffect = true)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(operation);

        if (evidence.Tenant.TenantId != tenantContext.TenantId.Value
            || evidence.ActorId == Guid.Empty
            || tenantContext.ActorId is { } actorId && actorId != evidence.ActorId)
        {
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.AuditFailure,
                "audit_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var auditEntity = new MasterDataAuditEventEntity(evidence);
            db.AuditEvents.Add(auditEntity);
            var result = await operation(db);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            if (!addEffect)
            {
                // The audit entity is the effect for AppendAuditAsync. The
                // parameter exists only so the transaction helper remains
                // explicit at its call site.
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result with { Value = RefreshVersion(result.Value, db) };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Duplicate,
                databaseFailureCode);
        }
    }

    private MasterDataDbContext CreateContext(TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return new MasterDataDbContext(options, tenantContext);
    }

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.AsSpan().SequenceEqual(expected);

    private static T? RefreshVersion<T>(T? value, MasterDataDbContext db)
    {
        if (value is MasterDataCategoryRecord category)
        {
            var entity = db.Categories.Local.SingleOrDefault(candidate => candidate.Id == category.Id);
            return entity is null ? value : (T)(object)ToCategoryRecord(entity);
        }

        if (value is MasterDataUnitOfMeasureRecord unit)
        {
            var entity = db.UnitsOfMeasure.Local.SingleOrDefault(candidate => candidate.Id == unit.Id);
            return entity is null ? value : (T)(object)ToUnitRecord(entity);
        }

        if (value is MasterDataConversionRecord conversion)
        {
            var entity = db.Conversions.Local.SingleOrDefault(candidate => candidate.Id == conversion.Id);
            return entity is null ? value : (T)(object)ToConversionRecord(entity);
        }

        if (value is ProductIdentityRecord product)
        {
            var entity = db.Products.Local.SingleOrDefault(candidate => candidate.Id == product.Id);
            if (entity is null)
            {
                return value;
            }

            var barcodeVersions = db.ProductBarcodes.Local
                .Where(candidate => candidate.ProductId == product.Id)
                .ToDictionary(candidate => candidate.Id, candidate => candidate.Version.ToArray());
            return (T)(object)(product with
            {
                Version = entity.Version.ToArray(),
                Barcodes = product.Barcodes
                    .Select(barcode => barcodeVersions.TryGetValue(barcode.Id, out var version)
                        ? barcode with { Version = version }
                        : barcode)
                    .ToArray()
            });
        }

        return value;
    }

    private static MasterDataCategoryRecord ToCategoryRecord(MasterDataCategoryEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.ParentCategoryId,
        entity.LifecycleState,
        entity.Version.ToArray(),
        entity.TrackingDefaultEnabled);

    private static async Task<ProductReferenceValidation> ReadProductReferencesAsync(
        MasterDataDbContext db,
        Guid categoryId,
        Guid baseUnitOfMeasureId,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        var unit = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == baseUnitOfMeasureId, cancellationToken);
        var categoryActive = category?.LifecycleState == MasterDataLifecycleState.Active;
        var unitActive = unit?.LifecycleState == MasterDataLifecycleState.Active;
        return new ProductReferenceValidation(
            Available: true,
            CategoryActive: categoryActive,
            BaseUnitOfMeasureActive: unitActive,
            TrackingDefaultEnabled: categoryActive && category is not null
                ? category.TrackingDefaultEnabled
                : false);
    }

    private static async Task<ProductIdentityRecord> ToProductRecordAsync(
        MasterDataDbContext db,
        MasterDataProductEntity entity,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == entity.CategoryId, cancellationToken);
        var trackedBarcodes = db.ChangeTracker
            .Entries<MasterDataProductBarcodeEntity>()
            .Where(entry => entry.Entity.ProductId == entity.Id)
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .OrderBy(item => item.Value)
            .ToArray();
        IReadOnlyList<MasterDataProductBarcodeEntity> barcodeEntities = trackedBarcodes.Length != 0
            || db.ChangeTracker.Entries<MasterDataProductBarcodeEntity>()
                .Any(entry => entry.Entity.ProductId == entity.Id)
            ? trackedBarcodes
            : await db.ProductBarcodes
                .AsNoTracking()
                .Where(item => item.ProductId == entity.Id)
                .OrderBy(item => item.Value)
                .ToListAsync(cancellationToken);
        var barcodes = barcodeEntities
            .Select(item => new ProductBarcodeRecord(
                item.Id,
                item.TenantId,
                item.ProductId,
                item.Value,
                item.Version.ToArray()))
            .ToArray();
        var trackingDefaultEnabled = category?.TrackingDefaultEnabled ?? false;
        return new ProductIdentityRecord(
            entity.Id,
            entity.TenantId,
            entity.Sku,
            entity.Name,
            entity.Description,
            entity.CategoryId,
            entity.BaseUnitOfMeasureId,
            trackingDefaultEnabled,
            entity.TrackingEnabledOverride,
            entity.TrackingEnabledOverride ?? trackingDefaultEnabled,
            entity.IsSellable,
            entity.IsPurchasable,
            entity.IsInventoryRelevant,
            entity.LifecycleState,
            entity.Version.ToArray(),
            barcodes);
    }

    private static MasterDataUnitOfMeasureRecord ToUnitRecord(MasterDataUnitOfMeasureEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.LifecycleState,
        entity.Version.ToArray());

    private static MasterDataConversionRecord ToConversionRecord(MasterDataConversionEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.FromUnitOfMeasureId,
        entity.ToUnitOfMeasureId,
        entity.Factor,
        entity.Version.ToArray());

    private static MasterDataAuditRecord ToAuditRecord(MasterDataAuditEventEntity entity)
    {
        var tenant = new TenantOwnership(entity.TenantId.Value);
        BusinessScope? scope = null;
        if (!string.IsNullOrWhiteSpace(entity.ScopePolicyId) && entity.ScopePolicyVersion > 0)
        {
            OrganizationReference? anchor = null;
            if (entity.ScopeAnchorKind is { } kind && entity.ScopeAnchorId is { } id)
            {
                anchor = new OrganizationReference(tenant, kind, id);
            }

            scope = new BusinessScope(
                tenant,
                anchor,
                new ScopePolicyReference(entity.ScopePolicyId, entity.ScopePolicyVersion));
        }

        return new MasterDataAuditRecord(
            entity.EvidenceId,
            entity.OccurredAt,
            entity.OperationId,
            entity.CorrelationId,
            entity.TenantId,
            entity.ActorId,
            entity.SessionId,
            entity.AuthorizationPath switch
            {
                FoundationAuditAuthorizationPath.OrdinaryMembership => TenantAuthorizationPath.OrdinaryMembership,
                FoundationAuditAuthorizationPath.SupportGrant => TenantAuthorizationPath.SupportGrant,
                _ => throw new InvalidOperationException("Unsupported Tenant audit path.")
            },
            entity.ResourceKind,
            entity.ResourceId,
            entity.BusinessCode,
            scope,
            entity.Operation,
            entity.PolicyOutcome,
            entity.Decision,
            entity.Reason,
            entity.BeforeSummary,
            entity.AfterSummary,
            entity.ApproverId);
    }
}

#pragma warning restore CS1591
