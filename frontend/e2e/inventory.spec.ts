import { test, expect, type Page } from '@playwright/test';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 1,
};

async function setupValuationRoutes(page: Page, options: {
  summary?: Record<string, unknown>;
  processedSummary?: Record<string, unknown>;
  reconciliation?: Record<string, unknown>[];
  history?: Record<string, unknown>[];
  handoffs?: Record<string, unknown>[];
} = {}): Promise<{ processCount: () => number }> {
  let processed = false;
  let processRequests = 0;
  const initialSummary = {
    tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', functionalCurrencyCode: 'SAR',
    physicalOnHandQuantity: 15, valuedQuantity: 15, valuedAmount: 200, pendingMovementCount: 0, blockedMovementCount: 0,
    inTransitQuantity: 2, inTransitValue: 26.66666666, inTransitValueStatus: 'Ready', reconciliationStatus: 'Reconciled',
    latestLedgerSequence: 4, latestValuedLedgerSequence: 4, isComplete: true, isPartial: false,
    asOf: '2026-08-23T10:00:00Z', freshAsOf: '2026-08-23T10:00:00Z', ...options.summary,
  };
  const processedSummary = {
    ...initialSummary, valuedAmount: 216, latestLedgerSequence: 5, latestValuedLedgerSequence: 5,
    asOf: '2026-08-23T10:05:00Z', freshAsOf: '2026-08-23T10:05:00Z', ...options.processedSummary,
  };
  const reconciliation = options.reconciliation ?? [
    { warehouseId: 'warehouse-a', productId: 'product-a', unitOfMeasureId: 'uom-a', functionalCurrencyCode: 'SAR', status: 'Reconciled', physicalOnHandQuantity: 10, valuedQuantity: 10, quantityDifference: 0, valuedAmount: 133.33333333, averageUnitCost: 13.33333333, pendingMovementCount: 0, blockedMovementCount: 0, inTransitQuantity: 1, inTransitValue: 13.33333333, inTransitValueStatus: 'Ready', financeHandoffStatus: 'ReadyForFinance', lastAppliedLedgerSequence: 3 },
    { warehouseId: 'warehouse-a', productId: 'product-b', unitOfMeasureId: 'uom-a', functionalCurrencyCode: 'SAR', status: 'Reconciled', physicalOnHandQuantity: 5, valuedQuantity: 5, quantityDifference: 0, valuedAmount: 66.66666667, averageUnitCost: 13.33333333, pendingMovementCount: 0, blockedMovementCount: 0, inTransitQuantity: 1, inTransitValue: 13.33333333, inTransitValueStatus: 'Ready', financeHandoffStatus: 'ReadyForFinance', lastAppliedLedgerSequence: 4 },
  ];
  const history = options.history ?? [{ id: 'event-a', movementId: 'movement-a', sourceType: 'OpeningBalance', sourceDocumentId: 'opening-a', sourceReference: 'OPEN-001', ledgerSequence: 4, status: 'Applied', statusCode: 'applied', quantity: 15, direction: 'Inbound', baseUnitCost: 13.33333333, movementValue: 200, newValue: 200, effectiveOn: '2026-08-23', functionalCurrencyCode: 'SAR' }];
  const handoffs = options.handoffs ?? [
    { id: 'handoff-in', movementId: 'movement-in', sourceType: 'OpeningBalance', sourceDocumentId: 'opening-a', ledgerSequence: 3, quantity: 15, direction: 'Inbound', baseUnitCost: 13.33333333, baseAmount: 200, signedBaseAmount: 200, functionalCurrencyCode: 'SAR', status: 'ReadyForFinance', contractVersion: 'inventory-valuation-finance.v1' },
    { id: 'handoff-out', movementId: 'movement-out', sourceType: 'StockIssue', sourceDocumentId: 'issue-a', ledgerSequence: 4, quantity: 2, direction: 'Outbound', baseUnitCost: 13.33333333, baseAmount: 26.66666666, signedBaseAmount: -26.66666666, functionalCurrencyCode: 'SAR', status: 'ReadyForFinance', contractVersion: 'inventory-valuation-finance.v1' },
  ];

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: { entryMode: 'Tenant', selectedOperationalContextId: 'context-a', operationalSelectionVersion: 1, branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true }, currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null } }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ status: 204, headers: { 'X-CSRF-TOKEN': 'inventory-control-e2e-token' } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/inventory/valuation/policies**', (route) => route.fulfill({ json: [{ id: 'policy-a', functionalCurrencyCode: 'SAR', scopeMode: 'WarehouseProductUom', effectiveFrom: '2026-08-01', effectiveTo: null, versionNumber: 1, roundingMode: 'ToEven', goodsReceiptCostBasis: 'PurchaseOrderUnitPrice', positiveAdjustmentCostBasis: 'CurrentMovingAverage', supplierReturnCostBasis: 'CurrentMovingAverage', isActive: true, supersedesPolicyId: null }] }));
  await page.route('**/api/v1/inventory/valuation/summary**', (route) => route.fulfill({ json: processed ? processedSummary : initialSummary }));
  await page.route('**/api/v1/inventory/valuation/reconciliation**', (route) => route.fulfill({ json: reconciliation }));
  await page.route('**/api/v1/inventory/valuation/history**', (route) => route.fulfill({ json: history }));
  await page.route('**/api/v1/inventory/valuation/pending**', (route) => route.fulfill({ json: history.filter(event => event.status === 'Pending' || event.status === 'Blocked') }));
  await page.route('**/api/v1/inventory/valuation/finance-handoffs**', (route) => route.fulfill({ json: handoffs }));
  await page.route('**/api/v1/inventory/valuation/process', async (route) => {
    if (route.request().method() !== 'POST') return route.fulfill({ json: {} });
    processRequests += 1;
    processed = true;
    return route.fulfill({ json: { appliedCount: 1, pendingCount: 0, blockedCount: 0, latestLedgerSequence: 5, latestValuedLedgerSequence: 5 } });
  });

  return { processCount: () => processRequests };
}

test('Inventory workspace renders server-provided scope and availability', async ({ page }) => {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({
    json: {
      entryMode: 'Tenant',
      selectedOperationalContextId: 'context-a',
      operationalSelectionVersion: 1,
      branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
      currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
      code: null,
    },
  }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/inventory/transfers**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/customer-returns/boundary', (route) => route.fulfill({ json: { available: false, code: 'unavailable', message: 'Sales handoff required', sourceType: 'CustomerReturn' } }));
  await page.route('**/api/v1/master-data/products', (route) => route.fulfill({ json: [{ id: 'product-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', sku: 'SKU-A', englishName: 'Product A', arabicName: null, description: null, categoryId: 'category-a', baseUnitOfMeasureId: 'unit-a', trackingDefaultEnabled: false, trackingEnabledOverride: false, trackingEnabled: false, isSellable: true, isPurchasable: true, isInventoryRelevant: true, barcodes: [] }] }));
  await page.route('**/api/v1/master-data/units-of-measure', (route) => route.fulfill({ json: [{ id: 'unit-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'EA', englishName: 'Each', arabicName: null }] }));
  await page.route('**/api/v1/inventory/ledger**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/opening-balances**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/reservations**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/availability**', (route) => route.fulfill({ json: { onHandQuantity: 12, reservedQuantity: 3, availableQuantity: 9, unitOfMeasureCode: 'EA' } }));

  await page.goto('/app/inventory');

  await expect(page.locator('[data-testid="inventory-workspace"]')).toBeVisible();
  await expect(page.locator('h1')).toHaveText('Stock');
  await expect(page.locator('select').first()).toContainText('WH-A · Main warehouse');
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('9');
});

test('Inventory opening posts to the ledger and reservation release restores availability', async ({ page }) => {
  let openingStatus = 'Draft';
  let openingVersion = 'AQ==';
  let openingRequestBody: { sourceReference?: string; rows?: Array<{ sourceLineReference?: string }> } | undefined;
  let posted = false;
  let reservationStatus = 'Active';
  let reservationVersion = 'BA==';
  let reserved = 0;

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: { entryMode: 'Tenant', selectedOperationalContextId: 'context-a', operationalSelectionVersion: 1, branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true }, currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null } }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ status: 204, headers: { 'X-CSRF-TOKEN': 'inventory-e2e-token' } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/inventory/transfers**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/customer-returns/boundary', (route) => route.fulfill({ json: { available: false, code: 'unavailable', message: 'Sales handoff required', sourceType: 'CustomerReturn' } }));
  await page.route('**/api/v1/master-data/products', (route) => route.fulfill({ json: [{ id: 'product-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', sku: 'SKU-A', englishName: 'Product A', arabicName: null, description: null, categoryId: 'category-a', baseUnitOfMeasureId: 'unit-a', trackingDefaultEnabled: false, trackingEnabledOverride: false, trackingEnabled: false, isSellable: true, isPurchasable: true, isInventoryRelevant: true, barcodes: [] }] }));
  await page.route('**/api/v1/master-data/units-of-measure', (route) => route.fulfill({ json: [{ id: 'unit-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'EA', englishName: 'Each', arabicName: null }] }));
  await page.route('**/api/v1/inventory/opening-balances**', async (route) => {
    if (route.request().method() === 'POST') {
      openingRequestBody = route.request().postDataJSON() as { sourceReference?: string; rows?: Array<{ sourceLineReference?: string }> };
      openingStatus = 'Draft';
      openingVersion = 'AQ==';
      return route.fulfill({ json: { id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: 5, rows: [] } });
    }
    return route.fulfill({ json: [{ id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: posted ? 5 : 0, rows: [] }] });
  });
  await page.route('**/api/v1/inventory/opening-balances/opening-a/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() === 'POST' && path.endsWith('/validate')) {
      openingStatus = 'Validated';
      openingVersion = 'Ag==';
    } else if (route.request().method() === 'POST' && path.endsWith('/post')) {
      openingStatus = 'Posted';
      openingVersion = 'Aw==';
      posted = true;
    }
    return route.fulfill({ json: { id: 'opening-a', status: openingStatus, version: openingVersion, warehouseCode: 'WH-A', sourceSystem: 'Opening import', asOfDate: '2026-08-21', validQuantityTotal: posted ? 5 : 5, rows: [] } });
  });
  await page.route('**/api/v1/inventory/reservations**', async (route) => {
    if (route.request().method() === 'POST') {
      reserved = 2;
      reservationStatus = 'Active';
      return route.fulfill({ json: { id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: reserved, unallocatedQuantity: 0 } });
    }
    return route.fulfill({ json: reserved > 0 || reservationStatus === 'Released' ? [{ id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: reserved, unallocatedQuantity: reservationStatus === 'Released' ? 2 : 0 }] : [] });
  });
  await page.route('**/api/v1/inventory/reservations/reservation-a/release', async (route) => {
    reservationStatus = 'Released';
    reservationVersion = 'BQ==';
    reserved = 0;
    return route.fulfill({ json: { id: 'reservation-a', status: reservationStatus, version: reservationVersion, sourceReference: 'DEMAND-1', productSku: 'SKU-A', reservedQuantity: 0, unallocatedQuantity: 2 } });
  });
  await page.route('**/api/v1/inventory/ledger**', (route) => route.fulfill({ json: posted ? [{ id: 'movement-a', productSku: 'SKU-A', productName: 'Product A', sourceType: 'OpeningBalance', direction: 'Inbound', quantity: 5, effectiveDate: '2026-08-21' }] : [] }));
  await page.route('**/api/v1/inventory/availability**', (route) => route.fulfill({ json: { onHandQuantity: posted ? 5 : 0, reservedQuantity: reserved, availableQuantity: (posted ? 5 : 0) - reserved, expectedQuantity: 0, damagedQuantity: 0, inTransitQuantity: 0, unitOfMeasureCode: 'EA' } }));

  await page.goto('/app/inventory');
  await expect(page.locator('[data-testid="inventory-workspace"]')).toBeVisible();

  const openingForm = page.locator('form').first();
  await openingForm.locator('input[name="quantity"]').fill('5');
  await openingForm.locator('input[name="sourceReference"]').fill('OPENING-1');
  await openingForm.getByRole('button').click();
  await expect.poll(() => openingRequestBody).toMatchObject({ sourceReference: 'OPENING-1', rows: [{ sourceLineReference: 'OPENING-1' }] });
  expect(JSON.stringify(openingRequestBody)).not.toContain('line-1');
  const openingGrid = page.locator('.workflow-card').first().locator('app-data-grid');
  await openingGrid.locator('.grid-row-menu-trigger').click();
  await openingGrid.getByRole('menuitem', { name: 'Validate', exact: true }).click();
  await openingGrid.locator('.grid-row-menu-trigger').click();
  await openingGrid.getByRole('menuitem', { name: 'Post movement', exact: true }).click();
  await expect(page.locator('.ledger-card app-data-grid tbody tr').first()).toContainText('SKU-A');
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('5');

  const reservationForm = page.locator('form').nth(1);
  await reservationForm.locator('input[name="requestedQuantity"]').fill('2');
  await reservationForm.locator('input[name="sourceReference"]').fill('DEMAND-1');
  await reservationForm.getByRole('button').click();
  await expect(page.getByText('DEMAND-1')).toBeVisible();
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('3');
  await page.getByRole('button', { name: 'Release' }).click();
  await expect(page.locator('.metric-grid strong').nth(2)).toHaveText('5');
});

test('MESP-130 stock control keeps counts blind and accepts physical observations', async ({ page }) => {
  let countStatus = 'Draft';
  let countVersion = 'AQ==';
  let countReason: string | undefined;
  let adjustmentStatus = 'Draft';
  const count = () => ({ id: 'count-1', tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', warehouseCode: 'WH-A', warehouseName: 'Main warehouse', countType: 'Cycle', assignedCounterId: 'actor-1', reviewerId: null, approverId: null, posterId: null, status: countStatus, currentRoundGeneration: 1, snapshotCutoff: '2026-08-22T10:00:00Z', approval: null, createdAt: '2026-08-22T10:00:00Z', updatedAt: '2026-08-22T10:00:00Z', submittedAt: countStatus === 'Draft' ? null : '2026-08-22T10:01:00Z', approvedAt: null, postedAt: null, version: countVersion, lines: [{ id: 'line-1', priorLineId: null, roundGeneration: 1, productId: 'product-a', productSku: 'SKU-A', productName: 'Product A', unitOfMeasureId: 'unit-a', unitOfMeasureCode: 'EA', trackingIdentity: '', expectedQuantity: countStatus === 'Draft' ? null : 5, countedQuantity: countStatus === 'Draft' ? null : 7, variance: countStatus === 'Draft' ? null : 2, varianceReasonCodeId: countReason ? 'reason-count' : null, varianceReasonCode: countReason ?? null, varianceReasonEnglishName: countReason ? 'Variance review' : null, varianceReasonArabicName: countReason ? 'مراجعة الفرق' : null, isCurrentRound: true, countedAt: countStatus === 'Draft' ? null : '2026-08-22T10:01:00Z', version: countVersion }] });

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: { entryMode: 'Tenant', selectedOperationalContextId: 'context-a', operationalSelectionVersion: 1, branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true }, currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null } }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ status: 204, headers: { 'X-CSRF-TOKEN': 'inventory-control-e2e-token' } }));
  await page.route('**/api/v1/inventory/warehouses**', (route) => route.fulfill({ json: [{ tenantId: 'tenant-a', companyId: 'company-a', branchId: null, warehouseId: 'warehouse-a', code: 'WH-A', name: 'Main warehouse', displayName: 'WH-A · Main warehouse', isActive: true }] }));
  await page.route('**/api/v1/inventory/transfers**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/customer-returns/boundary', (route) => route.fulfill({ json: { available: false, code: 'unavailable', message: 'Sales handoff required', sourceType: 'CustomerReturn' } }));
  await page.route('**/api/v1/master-data/products', (route) => route.fulfill({ json: [{ id: 'product-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', sku: 'SKU-A', englishName: 'Product A', arabicName: null, description: null, categoryId: 'category-a', baseUnitOfMeasureId: 'unit-a', trackingDefaultEnabled: false, trackingEnabledOverride: false, trackingEnabled: false, isSellable: true, isPurchasable: true, isInventoryRelevant: true, barcodes: [] }] }));
  await page.route('**/api/v1/master-data/units-of-measure', (route) => route.fulfill({ json: [{ id: 'unit-a', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'EA', englishName: 'Each', arabicName: null }] }));
  await page.route('**/api/v1/inventory/ledger**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/opening-balances**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/reservations**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/inventory/availability**', (route) => route.fulfill({ json: { onHandQuantity: 5, reservedQuantity: 0, availableQuantity: 5, unitOfMeasureCode: 'EA' } }));
  await page.route('**/api/v1/inventory/reason-codes**', (route) => {
    const category = new URL(route.request().url()).searchParams.get('category');
    const categories = category ? [category] : ['Adjustment', 'CountVariance', 'StockIssue'];
    return route.fulfill({ json: categories.map(value => ({ id: `reason-${value}`, tenantId: 'tenant-a', code: 'COUNT-DAMAGE', englishName: 'Variance review', arabicName: 'مراجعة الفرق', category: value, isActive: true, version: 'AQ==' })) });
  });
  await page.route('**/api/v1/inventory/adjustments**', async (route) => { if (route.request().method() === 'POST') { adjustmentStatus = 'Draft'; return route.fulfill({ json: { id: 'adjustment-1', status: adjustmentStatus, version: 'AQ==', lines: [{ productSku: 'SKU-A', direction: 'Increase', quantity: 1, reasonCode: 'COUNT-DAMAGE' }] } }); } return route.fulfill({ json: [{ id: 'adjustment-1', status: adjustmentStatus, version: 'AQ==', lines: [{ productSku: 'SKU-A', direction: 'Increase', quantity: 1, reasonCode: 'COUNT-DAMAGE' }] }] }); });
  await page.route('**/api/v1/inventory/adjustments/adjustment-1/submit', async (route) => { adjustmentStatus = 'PendingApproval'; return route.fulfill({ json: { id: 'adjustment-1', status: adjustmentStatus, version: 'Ag==', lines: [{ productSku: 'SKU-A', direction: 'Increase', quantity: 1, reasonCode: 'COUNT-DAMAGE' }] } }); });
  await page.route('**/api/v1/inventory/counts**', async (route) => { const path = new URL(route.request().url()).pathname; if (route.request().method() === 'POST' && path.endsWith('/submit')) { countStatus = 'PendingApproval'; countVersion = 'Ag=='; return route.fulfill({ json: count() }); } if (route.request().method() === 'POST' && path.endsWith('/variance-reason')) { countReason = 'COUNT-DAMAGE'; countVersion = 'Aw=='; return route.fulfill({ json: count() }); } return route.fulfill({ json: countStatus === 'Draft' ? [count()] : [count()] }); });
  await page.route('**/api/v1/inventory/stock-issues**', (route) => route.fulfill({ json: [] }));

  await page.goto('/app/inventory');
  const stockControl = page.locator('[data-testid="inventory-stock-control"]');
  await expect(stockControl).toBeVisible();
  await expect(stockControl).not.toContainText('Expected:');
  await expect(stockControl.getByRole('button', { name: 'Submit observations' })).toBeVisible();
  await expect(stockControl.getByRole('button', { name: 'Submit zero-variance' })).toHaveCount(0);
  await stockControl.getByRole('spinbutton', { name: 'SKU-A · EA' }).fill('7');
  await stockControl.getByRole('button', { name: 'Submit observations' }).click();
  await expect(stockControl).toContainText('Expected: 5');
  await stockControl.locator('.count-review select').selectOption('COUNT-DAMAGE');
  await stockControl.getByRole('button', { name: 'Record reason' }).click();
  await expect(stockControl).toContainText('COUNT-DAMAGE');

  const adjustmentForm = stockControl.locator('form.control-form').first();
  await adjustmentForm.locator('input[name="quantity"]').fill('1');
  await adjustmentForm.locator('select[name="reason"]').selectOption('COUNT-DAMAGE');
  await adjustmentForm.getByRole('button', { name: 'Create draft' }).click();
  await expect(stockControl.locator('.stock-control__lists section').first().getByText('Draft')).toBeVisible();
});

test('MESP-131 aggregate summary combines two Products without a warehouse average', async ({ page }) => {
  await setupValuationRoutes(page);
  await page.goto('/app/inventory/valuation');

  await expect(page.locator('[data-testid="inventory-valuation-workspace"]')).toBeVisible();
  await expect(page.locator('[data-testid="valuation-summary-metrics"] strong').nth(0)).toHaveText('15');
  await expect(page.locator('[data-testid="valuation-summary-metrics"] strong').nth(1)).toHaveText('15');
  await expect(page.locator('[data-testid="valuation-summary-metrics"] strong').nth(2)).toContainText('200');
  await expect(page.locator('[data-testid="inventory-valuation-workspace"]')).not.toContainText('Average unit cost');

  await page.getByRole('tab', { name: 'Reconciliation' }).click();
  await expect(page.locator('[data-testid="valuation-reconciliation"] tbody tr')).toHaveCount(2);
  await expect(page.locator('[data-testid="valuation-reconciliation"]')).toContainText('133.33333333');
  await expect(page.locator('[data-testid="valuation-reconciliation"]')).toContainText('66.66666667');
});

test('MESP-131 pending Product produces a visible partial summary', async ({ page }) => {
  await setupValuationRoutes(page, {
    summary: { valuedQuantity: 10, valuedAmount: 133.33333333, pendingMovementCount: 1, reconciliationStatus: 'PendingValuation', isComplete: false, isPartial: true },
    reconciliation: [{ warehouseId: 'warehouse-a', productId: 'product-b', unitOfMeasureId: 'uom-a', functionalCurrencyCode: 'SAR', status: 'Pending', physicalOnHandQuantity: 5, valuedQuantity: 0, quantityDifference: 5, valuedAmount: 0, averageUnitCost: null, pendingMovementCount: 1, blockedMovementCount: 0, inTransitQuantity: 0, inTransitValue: 0, inTransitValueStatus: 'Pending', financeHandoffStatus: 'Pending', lastAppliedLedgerSequence: 0 }],
    history: [{ id: 'event-pending', movementId: 'movement-pending', sourceType: 'StockIssue', sourceDocumentId: 'issue-pending', sourceReference: 'PENDING-001', ledgerSequence: 5, status: 'Pending', statusCode: 'valuation_policy_not_configured', pendingReason: 'valuation_policy_not_configured', quantity: 5, direction: 'Outbound', baseUnitCost: null, movementValue: null, newValue: 0, effectiveOn: '2026-08-23', functionalCurrencyCode: 'SAR' }],
  });
  await page.goto('/app/inventory/valuation');

  await expect(page.locator('[data-testid="valuation-summary-metrics"]')).toContainText('Partial');
  await expect(page.locator('[data-testid="valuation-summary-metrics"]')).toContainText('1');
  await expect(page.locator('[data-testid="inventory-valuation-workspace"]')).toContainText('PendingValuation');
  await expect(page.locator('[data-testid="inventory-valuation-workspace"]')).not.toContainText('Complete');
});

test('MESP-131 processing refreshes the aggregate safely', async ({ page }) => {
  const routes = await setupValuationRoutes(page, { processedSummary: { valuedAmount: 216 } });
  await page.goto('/app/inventory/valuation');
  await expect(page.locator('[data-testid="valuation-summary-metrics"] strong').nth(2)).toContainText('200');

  await page.getByTestId('valuation-process').click();
  await expect.poll(routes.processCount).toBe(1);
  await expect(page.locator('[data-testid="valuation-summary-metrics"] strong').nth(2)).toContainText('216');
  await expect(page.locator('[data-testid="valuation-summary-metrics"]')).toContainText('5');
});

test('MESP-131 Finance handoff exposes direction and signed amount', async ({ page }) => {
  await setupValuationRoutes(page);
  await page.goto('/app/inventory/valuation');
  await page.getByRole('tab', { name: 'Finance handoff' }).click();

  const handoff = page.locator('[data-testid="valuation-finance-handoff"]');
  await expect(handoff).toContainText('Inbound');
  await expect(handoff).toContainText('Outbound');
  await expect(handoff).toContainText('-26.66666666');
  await expect(handoff).toContainText('inventory-valuation-finance.v1');
});

test('MESP-131 valuation evidence keeps the Arabic route RTL', async ({ page }) => {
  await setupValuationRoutes(page);
  await page.goto('/app/inventory/valuation');

  await expect(page.locator('h1')).toHaveText('Moving Weighted Average');
  await page.getByRole('button', { name: 'Language' }).click();
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
  await expect(page.locator('h1')).toContainText('المتوسط المتحرك المرجح');
});
