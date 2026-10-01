import { expect, test, type Page, type Route } from '@playwright/test';

const tenantId = 'tenant-a';
const companyId = 'company-a';
const purchaseOrderId = 'po-gr-001';
const purchaseOrderLineId = 'po-line-001';
const goodsReceiptId = 'gr-001';
const goodsReceiptLineId = 'gr-line-001';
const invoiceHandoffId = 'pih-001';
const warehouseId = 'wh-001';

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
  entryMode: 'TenantHost',
  canonicalHost: '127.0.0.1',
  candidateTenantId: tenantId,
  candidateTenantDisplayName: 'Alpha Tenant',
  authorizedTenants: [{ tenantId, displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
  operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
  selectedOperationalContextId: 'operation-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
};

const eligibleReceiptSource = {
  purchaseOrderId,
  companyId,
  branchId: null,
  status: 'Confirmed',
  supplierId: 'supplier-001',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'SAR',
  lines: [
    {
      purchaseOrderLineId,
      productId: 'prod-001',
      productSku: 'SKU-GR-001',
      productName: 'Receivable Widget',
      unitOfMeasureId: 'uom-001',
      unitOfMeasureCode: 'PCS',
      unitPrice: 100,
      confirmedQuantity: 5,
      alreadyReceivedQuantity: 0,
      remainingReceivableQuantity: 5,
    },
  ],
};

const warehouseOptions = [
  {
    warehouseId,
    code: 'WH-MAIN',
    name: 'Main Warehouse',
    isActive: true,
  },
];

const eligibleHandoffSource = {
  purchaseOrderId,
  companyId,
  branchId: null,
  supplierId: 'supplier-001',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyId: 'currency-sar',
  currencyCode: 'SAR',
  currencyName: 'Saudi Riyal',
  lines: [
    {
      goodsReceiptId,
      goodsReceiptLineId,
      purchaseOrderLineId,
      productId: 'prod-001',
      productSku: 'SKU-GR-001',
      productName: 'Receivable Widget',
      unitOfMeasureId: 'uom-001',
      unitOfMeasureCode: 'PCS',
      receivedDate: '2026-08-19',
      acceptedQuantity: 4,
      alreadyHandedOffQuantity: 0,
      remainingHandoffQuantity: 4,
      unitPrice: 100,
      taxRatePercentage: 15,
      taxAmount: 60,
    },
  ],
};

function procurementHarness(receiptSource = eligibleReceiptSource, handoffSource = eligibleHandoffSource) {
  let grStatus = 'Recorded';
  let grVersionNumber = 1;
  let pihStatus = 'Recorded';
  let pihVersionNumber = 1;
  let submittedGoodsReceipt: { lines: { purchaseOrderLineId: string; receivedQuantity: number }[] } | null = null;
  let submittedInvoiceHandoff: { sources: { goodsReceiptLineId: string; quantity: number }[] } | null = null;

  const grVersion = () => `GR-V${grVersionNumber}`;
  const pihVersion = () => `PIH-V${pihVersionNumber}`;

  const goodsReceipt = () => ({
    id: goodsReceiptId,
    tenantId,
    companyId,
    branchId: null,
    purchaseOrderId,
    warehouseId,
    receivedByActorId: 'actor-1',
    status: grStatus,
    supplierId: 'supplier-001',
    supplierCode: 'SUP-001',
    supplierName: 'Supplier One',
    receivedDate: '2026-08-19',
    referenceNote: 'DN-2026-001',
    notes: 'Dock notes',
    createdAt: '2026-08-19T08:00:00Z',
    updatedAt: '2026-08-19T08:00:00Z',
    cancelledAt: grStatus === 'Cancelled' ? '2026-08-19T09:00:00Z' : null,
    cancellationReason: grStatus === 'Cancelled' ? 'Wrong delivery' : null,
    lines: [
      {
        id: goodsReceiptLineId,
        purchaseOrderLineId,
        productId: 'prod-001',
        productSku: 'SKU-GR-001',
        productName: 'Receivable Widget',
        unitOfMeasureCode: 'PCS',
        orderedQuantityAtReceipt: 5,
        receivedQuantity: 5,
        acceptedQuantity: 4,
        rejectedQuantity: 1,
        damagedQuantity: 1,
        damageNotes: '1 crushed item',
        remainingReceivableQuantityAfter: 1,
        notes: null,
      },
    ],
    version: grVersion(),
    canCancel: grStatus === 'Recorded',
  });

  const invoiceHandoff = () => ({
    id: invoiceHandoffId,
    tenantId,
    companyId,
    branchId: null,
    purchaseOrderId,
    createdByActorId: 'actor-1',
    status: pihStatus,
    supplierId: 'supplier-001',
    supplierCode: 'SUP-001',
    supplierName: 'Supplier One',
    currencyCode: 'SAR',
    supplierInvoiceReference: 'INV-2026-888',
    supplierInvoiceDate: '2026-08-19',
    notes: 'Matched with GR',
    createdAt: '2026-08-19T08:30:00Z',
    updatedAt: '2026-08-19T08:30:00Z',
    cancelledAt: pihStatus === 'Cancelled' ? '2026-08-19T09:30:00Z' : null,
    cancellationReason: pihStatus === 'Cancelled' ? 'Billing correction' : null,
    lines: [
      {
        id: 'pih-line-001',
        purchaseOrderLineId,
        productId: 'prod-001',
        productSku: 'SKU-GR-001',
        productName: 'Receivable Widget',
        unitOfMeasureCode: 'PCS',
        handoffQuantity: 4,
        unitPrice: 100,
        taxRatePercentage: 15,
        taxAmount: 60,
        lineAmount: 460,
      },
    ],
    sources: [
      {
        id: 'pih-src-001',
        goodsReceiptId,
        goodsReceiptLineId,
        purchaseOrderLineId,
        quantity: 4,
      },
    ],
    version: pihVersion(),
    canCancel: pihStatus === 'Recorded',
  });

  return {
    submittedGoodsReceipt: () => submittedGoodsReceipt,
    submittedInvoiceHandoff: () => submittedInvoiceHandoff,
    async route(route: Route): Promise<void> {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname;

      if (!path.includes('/api/v1/procurement/')) return route.fallback();

      // Goods Receipt endpoints
      if (request.method() === 'GET' && path.endsWith('/goods-receipt-sources')) return route.fulfill({ json: [receiptSource] });
      if (request.method() === 'GET' && path.endsWith('/warehouses')) return route.fulfill({ json: warehouseOptions });
      if (request.method() === 'GET' && path.endsWith('/goods-receipts')) return route.fulfill({ json: [goodsReceipt()] });
      if (request.method() === 'GET' && path.endsWith(`/goods-receipts/${goodsReceiptId}`)) return route.fulfill({ json: goodsReceipt() });
      if (request.method() === 'GET' && path.endsWith(`/goods-receipts/${goodsReceiptId}/history`)) return route.fulfill({ json: [] });
      if (request.method() === 'GET' && path.endsWith(`/goods-receipts/${goodsReceiptId}/audit`)) return route.fulfill({ json: [] });

      if (request.method() === 'POST' && path.endsWith('/goods-receipts')) {
        submittedGoodsReceipt = request.postDataJSON() as { lines: { purchaseOrderLineId: string; receivedQuantity: number }[] };
        grStatus = 'Recorded';
        grVersionNumber += 1;
        return route.fulfill({ status: 201, headers: { ETag: `"${grVersion()}"` }, json: goodsReceipt() });
      }
      if (request.method() === 'POST' && path.endsWith(`/goods-receipts/${goodsReceiptId}/cancel`)) {
        grStatus = 'Cancelled';
        grVersionNumber += 1;
        return route.fulfill({ json: goodsReceipt() });
      }

      // Purchase Invoice Handoff endpoints
      if (request.method() === 'GET' && path.endsWith('/purchase-invoice-handoff-sources')) return route.fulfill({ json: [handoffSource] });
      if (request.method() === 'GET' && path.endsWith('/purchase-invoice-handoffs')) return route.fulfill({ json: [invoiceHandoff()] });
      if (request.method() === 'GET' && path.endsWith(`/purchase-invoice-handoffs/${invoiceHandoffId}`)) return route.fulfill({ json: invoiceHandoff() });
      if (request.method() === 'GET' && path.endsWith(`/purchase-invoice-handoffs/${invoiceHandoffId}/history`)) return route.fulfill({ json: [] });
      if (request.method() === 'GET' && path.endsWith(`/purchase-invoice-handoffs/${invoiceHandoffId}/audit`)) return route.fulfill({ json: [] });

      if (request.method() === 'POST' && path.endsWith('/purchase-invoice-handoffs')) {
        submittedInvoiceHandoff = request.postDataJSON() as { sources: { goodsReceiptLineId: string; quantity: number }[] };
        pihStatus = 'Recorded';
        pihVersionNumber += 1;
        return route.fulfill({ status: 201, headers: { ETag: `"${pihVersion()}"` }, json: invoiceHandoff() });
      }
      if (request.method() === 'POST' && path.endsWith(`/purchase-invoice-handoffs/${invoiceHandoffId}/cancel`)) {
        pihStatus = 'Cancelled';
        pihVersionNumber += 1;
        return route.fulfill({ json: invoiceHandoff() });
      }

      return route.fulfill({ json: [] });
    },
  };
}

async function installAuth(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) =>
    route.fulfill({
      json: {
        contexts: [
          {
            contextId: 'context-a',
            kind: 'OrdinaryMembership',
            tenantId,
            displayName: 'Alpha workspace',
            eligibilityVersion: 3,
          },
        ],
      },
    }),
  );
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: authEntry }));
  await page.route('**/api/v1/auth/antiforgery', (route) =>
    route.fulfill({
      headers: { 'X-CSRF-TOKEN': 'playwright-token' },
      json: { status: 'issued' },
    }),
  );
}

test.describe('Goods Receipt and Purchase Invoice Handoff workspace', () => {
  test.beforeEach(async ({ page }) => installAuth(page));

  test('creates Goods Receipt from Confirmed Purchase Order, verifies detail tabs, and cancels', async ({ page }) => {
    const harness = procurementHarness();
    await page.route('**/api/v1/procurement/**', harness.route);

    await page.goto('/app/procurement/goods-receipts/new');
    await page.getByTestId('goods-receipt-source').selectOption(purchaseOrderId);
    await expect(page.getByText('SKU-GR-001')).toBeVisible();

    await page.getByTestId('goods-receipt-warehouse').selectOption(warehouseId);
    await page.getByTestId('goods-receipt-reference-note').fill('DN-2026-001');
    await page.getByTestId('submit-goods-receipt').click();

    await expect(page).toHaveURL(new RegExp(`/app/procurement/goods-receipts/${goodsReceiptId}$`));
    await expect(page.getByTestId('goods-receipt-detail')).toBeVisible();

    // Verify tabs
    await page.getByRole('tab', { name: 'Lines' }).click();
    await expect(page.getByText('SKU-GR-001')).toBeVisible();

    // Cancel receipt
    await page.getByTestId('cancel-goods-receipt').click();
    await page.locator('.action-dialog textarea').fill('Wrong delivery');
    await page.getByRole('button', { name: 'Confirm Cancellation' }).click();
    await expect(page.getByText('This Goods Receipt has been cancelled.')).toBeVisible();
  });

  test('shows and submits every editable source line in both unpaged editors', async ({ page }) => {
    const receiptLines = Array.from({ length: 9 }, (_, index) => {
      const suffix = String(index + 1).padStart(3, '0');
      return {
        ...eligibleReceiptSource.lines[0],
        purchaseOrderLineId: `po-line-${suffix}`,
        productId: `prod-${suffix}`,
        productSku: `SKU-GR-${suffix}`,
        productName: `Receivable Widget ${index + 1}`,
      };
    });
    const handoffLines = Array.from({ length: 9 }, (_, index) => {
      const suffix = String(index + 1).padStart(3, '0');
      return {
        ...eligibleHandoffSource.lines[0],
        goodsReceiptLineId: `gr-line-${suffix}`,
        purchaseOrderLineId: `po-line-${suffix}`,
        productId: `prod-${suffix}`,
        productSku: `SKU-PIH-${suffix}`,
        productName: `Handoff Widget ${index + 1}`,
      };
    });
    const harness = procurementHarness(
      { ...eligibleReceiptSource, lines: receiptLines },
      { ...eligibleHandoffSource, lines: handoffLines },
    );
    await page.route('**/api/v1/procurement/**', harness.route);

    await page.goto('/app/procurement/goods-receipts/new');
    await page.getByTestId('goods-receipt-source').selectOption(purchaseOrderId);
    const receiptRows = page.locator('app-data-grid tbody tr');
    await expect(receiptRows).toHaveCount(9);
    const receiptQuantities = receiptLines.map((_, index) => index % 5 + 1);
    for (let index = 0; index < receiptQuantities.length; index += 1) {
      const quantity = receiptRows.nth(index).locator('input[type="number"]').first();
      await expect(quantity).toBeVisible();
      await expect(quantity).toBeEnabled();
      await quantity.fill(String(receiptQuantities[index]));
    }
    await page.getByTestId('goods-receipt-warehouse').selectOption(warehouseId);
    await page.getByTestId('goods-receipt-reference-note').fill('DN-2026-009');
    await page.getByTestId('submit-goods-receipt').click();
    await expect(page).toHaveURL(new RegExp(`/app/procurement/goods-receipts/${goodsReceiptId}$`));
    expect(harness.submittedGoodsReceipt()?.lines.map(line => [line.purchaseOrderLineId, line.receivedQuantity])).toEqual(
      receiptLines.map((line, index) => [line.purchaseOrderLineId, receiptQuantities[index]]),
    );

    await page.goto('/app/procurement/invoice-handoffs/new');
    await page.getByTestId('invoice-handoff-source').selectOption(purchaseOrderId);
    const handoffRows = page.locator('app-data-grid tbody tr');
    await expect(handoffRows).toHaveCount(9);
    await page.getByTestId('invoice-handoff-ref').fill('INV-2026-009');
    const handoffQuantities = handoffLines.map((_, index) => index % 4 + 1);
    for (let index = 0; index < handoffQuantities.length; index += 1) {
      const quantity = handoffRows.nth(index).locator('input[type="number"]').first();
      await expect(quantity).toBeVisible();
      await expect(quantity).toBeEnabled();
      await quantity.fill(String(handoffQuantities[index]));
    }
    await expect(page.getByTestId('submit-invoice-handoff')).toBeEnabled();
    await page.getByTestId('submit-invoice-handoff').click();
    await expect(page).toHaveURL(new RegExp(`/app/procurement/invoice-handoffs/${invoiceHandoffId}$`));
    expect(harness.submittedInvoiceHandoff()?.sources.map(source => [source.goodsReceiptLineId, source.quantity])).toEqual(
      handoffLines.map((line, index) => [line.goodsReceiptLineId, handoffQuantities[index]]),
    );
  });

  test('creates Purchase Invoice Handoff from accepted Goods Receipt with pro-rata tax and cancels', async ({ page }) => {
    const harness = procurementHarness();
    await page.route('**/api/v1/procurement/**', harness.route);

    await page.goto('/app/procurement/invoice-handoffs/new');
    await page.getByTestId('invoice-handoff-source').selectOption(purchaseOrderId);
    await expect(page.getByText('SKU-GR-001')).toBeVisible();

    await page.getByTestId('invoice-handoff-ref').fill('INV-2026-888');
    await page.getByTestId('submit-invoice-handoff').click();

    await expect(page).toHaveURL(new RegExp(`/app/procurement/invoice-handoffs/${invoiceHandoffId}$`));
    await expect(page.getByTestId('invoice-handoff-detail')).toBeVisible();

    // Verify sources tab
    await page.getByRole('tab', { name: 'Sources' }).click();
    await expect(page.getByText(goodsReceiptId)).toBeVisible();

    // Cancel handoff
    await page.getByTestId('cancel-invoice-handoff').click();
    await page.locator('.action-dialog textarea').fill('Billing correction');
    await page.getByRole('button', { name: 'Confirm Cancellation' }).click();
    await expect(page.getByText('This Purchase Invoice Handoff has been cancelled.')).toBeVisible();
  });

  test('supports English and Arabic bilingual toggle on Goods Receipt workspace', async ({ page }) => {
    const harness = procurementHarness();
    await page.route('**/api/v1/procurement/**', harness.route);

    await page.goto('/app/procurement/goods-receipts');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.getByText('سندات استلام البضائع').first()).toBeVisible();
  });
});
