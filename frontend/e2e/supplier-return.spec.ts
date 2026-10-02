import { expect, test, type Page, type Route } from '@playwright/test';

const tenantId = 'tenant-a';
const goodsReceiptId = 'gr-sr-001';
const goodsReceiptLineId = 'gr-line-sr-001';
const supplierReturnId = 'sr-001';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: tenantId,
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

const authEntry = {
  entryMode: 'Tenant',
  selectedOperationalContextId: 'operation-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
};

const source = {
  goodsReceiptId,
  purchaseOrderId: 'po-sr-001',
  supplierConfirmationId: 'confirmation-sr-001',
  companyId: 'company-a',
  branchId: null,
  warehouseId: 'warehouse-a',
  supplierId: 'supplier-a',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'SAR',
  lines: [{
    goodsReceiptId,
    goodsReceiptLineId,
    purchaseOrderId: 'po-sr-001',
    purchaseOrderLineId: 'po-line-sr-001',
    warehouseId: 'warehouse-a',
    productId: 'product-sr-001',
    productSku: 'SKU-SR-001',
    productName: 'Returnable Widget',
    unitOfMeasureCode: 'PCS',
    acceptedQuantity: 10,
    alreadyReturnedQuantity: 6,
    eligibleReturnQuantity: 4,
    receivedDate: '2026-08-20',
  }],
};

function procurementHarness() {
  let status = 'Draft';
  let versionNumber = 1;
  let inventoryHandoffReference: string | null = null;
  let financeReference: string | null = null;

  const version = () => `SR-V${versionNumber}`;
  const detail = () => ({
    id: supplierReturnId,
    tenantId,
    companyId: 'company-a',
    branchId: null,
    goodsReceiptId,
    purchaseOrderId: 'po-sr-001',
    supplierConfirmationId: 'confirmation-sr-001',
    warehouseId: 'warehouse-a',
    supplierId: 'supplier-a',
    supplierCode: 'SUP-001',
    supplierName: 'Supplier One',
    currencyCode: 'SAR',
    status,
    reasonCode: 'Damaged',
    condition: 'Unusable',
    commercialOutcome: 'CreditExpected',
    reasonDetail: 'Broken seal',
    notes: 'Dock review',
    returnDate: '2026-08-21',
    createdAt: '2026-08-21T08:00:00Z',
    updatedAt: '2026-08-21T08:00:00Z',
    cancelledAt: null,
    reversedAt: null,
    correctionOfId: null,
    inventoryHandoffId: inventoryHandoffReference ? 'inventory-evidence-001' : null,
    inventoryHandoffReference,
    financeReference,
    financeCurrencyCode: financeReference ? 'SAR' : null,
    financeAmount: null,
    lines: [{
      id: 'sr-line-001',
      goodsReceiptLineId,
      purchaseOrderLineId: 'po-line-sr-001',
      productId: 'product-sr-001',
      productSku: 'SKU-SR-001',
      productName: 'Returnable Widget',
      unitOfMeasureCode: 'PCS',
      acceptedQuantityAtReturn: 4,
      returnQuantity: 4,
      eligibleQuantityAfter: 0,
      notes: null,
    }],
    evidence: [{
      id: 'evidence-sr-001',
      referenceId: 'private-object-001',
      fileName: 'dock-photo.png',
      contentType: 'image/png',
      description: 'Dock review photo',
      source: 'private-file-reference',
      recordedAt: '2026-08-21T08:00:00Z',
    }],
    version: version(),
    canSubmit: status === 'Draft',
    canApprove: status === 'Submitted',
    canCancel: status === 'Draft' || status === 'Submitted' || status === 'Approved' || status === 'AwaitingInventory',
    canReverse: status === 'InventoryHandoffRecorded' || status === 'AwaitingFinance',
    canCorrect: status === 'Draft' || status === 'Submitted' || status === 'Approved' || status === 'AwaitingInventory',
  });

  return async (route: Route): Promise<void> => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (request.method() === 'POST' && path.endsWith(`/api/v1/inventory/supplier-returns/${supplierReturnId}/post`)) {
      inventoryHandoffReference = 'inventory-evidence-001';
      status = 'AwaitingFinance';
      versionNumber += 1;
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: { supplierReturnId, movementIds: ['movement-sr-001'], handoffReference: inventoryHandoffReference } });
    }
    if (!path.includes('/api/v1/procurement/')) return route.fallback();

    if (request.method() === 'GET' && path.endsWith('/supplier-return-sources')) return route.fulfill({ json: [source] });
    if (request.method() === 'GET' && path.endsWith('/supplier-returns/report')) return route.fulfill({ json: { returnCount: 0, totalReturnQuantity: 0, openReturnCount: 0, openReturnQuantity: 0, pendingInventoryCount: 0, pendingFinanceCount: 0, returns: [] } });
    if (request.method() === 'GET' && path.endsWith(`/supplier-returns/${supplierReturnId}`)) return route.fulfill({ json: detail() });
    if (request.method() === 'GET' && path.endsWith(`/supplier-returns/${supplierReturnId}/history`)) return route.fulfill({ json: [] });
    if (request.method() === 'GET' && path.endsWith(`/supplier-returns/${supplierReturnId}/audit`)) return route.fulfill({ json: [] });

    if (request.method() === 'POST' && path.endsWith('/supplier-returns')) {
      status = 'Draft';
      versionNumber += 1;
      return route.fulfill({ status: 201, headers: { ETag: `"${version()}"` }, json: detail() });
    }
    if (request.method() === 'POST' && path.endsWith(`/supplier-returns/${supplierReturnId}/submit`)) {
      status = 'Submitted';
      versionNumber += 1;
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: detail() });
    }
    if (request.method() === 'POST' && path.endsWith(`/supplier-returns/${supplierReturnId}/approve`)) {
      status = 'AwaitingInventory';
      versionNumber += 1;
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: detail() });
    }
    if (request.method() === 'POST' && path.endsWith(`/supplier-returns/${supplierReturnId}/inventory-handoff`)) {
      inventoryHandoffReference = 'INV-HANDOFF-001';
      status = 'AwaitingFinance';
      versionNumber += 1;
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: detail() });
    }
    if (request.method() === 'POST' && path.endsWith(`/supplier-returns/${supplierReturnId}/finance-reference`)) {
      financeReference = 'FIN-CREDIT-001';
      status = 'Completed';
      versionNumber += 1;
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: detail() });
    }

    return route.fulfill({ json: [] });
  };
}

async function installAuth(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-a', kind: 'OrdinaryMembership', tenantId, displayName: 'Alpha workspace', eligibilityVersion: 3 }] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: authEntry }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'playwright-token' }, json: { status: 'issued' } }));
}

test.describe('Supplier Return workspace', () => {
  test.beforeEach(async ({ page }) => installAuth(page));

  test('creates, approves, and records downstream evidence without claiming stock or accounting posting', async ({ page }) => {
    await page.route('**/api/v1/**', procurementHarness());

    await page.goto('/app/procurement/supplier-returns/new');
    await page.getByTestId('supplier-return-source').selectOption(goodsReceiptId);
    await expect(page.getByRole('cell', { name: 'Returnable Widget SKU-SR-001' })).toBeVisible();
    await expect(page.getByText('4', { exact: true }).first()).toBeVisible();
    await page.locator('input.quantity-input').fill('4');
    await page.getByPlaceholder('Private object ID or approved evidence reference').fill('private-object-001');
    await page.getByRole('button', { name: 'Save return draft' }).click();

    await expect(page).toHaveURL(new RegExp(`/app/procurement/supplier-returns/${supplierReturnId}$`));
    await expect(page.getByText('No authoritative movement claimed yet.')).toBeVisible();
    await page.getByRole('button', { name: 'Submit' }).click();
    await expect(page.getByRole('button', { name: 'Approve' })).toBeVisible();
    await page.getByRole('button', { name: 'Approve' }).click();

    await expect(page.getByRole('button', { name: 'Post physical return' })).toBeVisible();
    await expect(page.getByText('No accounting posting claimed here.')).toBeVisible();
    await page.getByRole('button', { name: 'Post physical return' }).click();
    await expect(page.getByText('Reference recorded; downstream module remains authoritative.')).toBeVisible();

    await page.getByPlaceholder('Finance-owned credit or correction reference').fill('FIN-CREDIT-001');
    await page.getByRole('button', { name: 'Record reference' }).click();
    await expect(page.getByText('FIN-CREDIT-001')).toBeVisible();
  });

  test('switches the return workspace into Arabic RTL', async ({ page }) => {
    await page.route('**/api/v1/procurement/**', procurementHarness());
    await page.goto('/app/procurement/supplier-returns/new');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });
});
