import { expect, test } from '@playwright/test';

const purchaseRequestId = '11111111-1111-1111-1111-111111111111';
const quotationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const companyId = '22222222-2222-2222-2222-222222222222';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

const approvedRequest = {
  id: purchaseRequestId,
  companyId,
  branchId: null,
  requesterId: 'actor-1',
  status: 'Approved',
  purpose: 'Office restocking',
  lineCount: 1,
  createdAt: '2026-08-13T10:00:00Z',
  updatedAt: '2026-08-13T10:00:00Z',
  version: 'PRVERSION',
};

const requestDetail = {
  ...approvedRequest,
  tenantId: 'tenant-a',
  lines: [{
    id: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget', unitOfMeasureId: 'uom-1',
    unitOfMeasureCode: 'PCS', quantity: 5, needByDate: '2026-09-01', purpose: 'Restocking', version: 'LINEVERSION',
  }],
  approval: null,
  submittedAt: '2026-08-13T11:00:00Z',
  approvedAt: '2026-08-13T12:00:00Z',
  cancelledAt: null,
  canEdit: false,
  canSubmit: false,
  canApprove: false,
  canReject: false,
  canReturnForChange: false,
  canCancel: false,
};

const quotationListItem = {
  id: quotationId,
  purchaseRequestId,
  supplier: { id: 'supplier-1', code: 'SUP-001', name: 'Supplier One' },
  status: 'Submitted',
  supplierQuotationReference: 'SUP-Q-2026-001',
  offerDate: '2026-08-14',
  validUntil: '2026-09-14',
  currency: { id: 'currency-sar', code: 'SAR', name: 'Saudi Riyal' },
  commercialTotal: 575,
  coveredLineCount: 1,
  requestedLineCount: 1,
  hasEvidence: true,
  version: 'QVERSION',
};

const comparison = {
  purchaseRequestId,
  hasMixedCurrencies: false,
  directCurrencyComparisonAvailable: true,
  comparisonBasis: 'Server-calculated commercial total within currency groups',
  currencyGroups: [{ currencyId: 'currency-sar', currencyCode: 'SAR', supplierQuotationIds: [quotationId], directlyComparableWithinGroup: true }],
  quotations: [{
    supplierQuotationId: quotationId,
    supplier: quotationListItem.supplier,
    status: 'Submitted',
    supplierQuotationReference: quotationListItem.supplierQuotationReference,
    offerDate: quotationListItem.offerDate,
    validUntil: quotationListItem.validUntil,
    currency: quotationListItem.currency,
    commercialTotal: 575,
    coveredLineCount: 1,
    requestedLineCount: 1,
    hasEvidence: true,
    isDirectlyComparableToAll: true,
    paymentTermCode: 'NET30',
    deliveryTerms: 'DAP',
    offeredDeliveryDate: '2026-09-01',
    offeredDeliveryLeadTime: null,
    lines: [{
      purchaseRequestLineId: 'line-1', productSku: 'SKU-001', productName: 'Widget', requestedQuantity: 5, quotedQuantity: 5,
      unitPrice: 100, discountAmount: null, discountPercentage: null, taxRatePercentage: 15, taxAmount: 75,
      requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', isCovered: true, qualificationIssue: null,
    }],
    qualificationIssues: [],
  }],
  currentSourceDecision: null,
};

const quotationDetail = {
  id: quotationId,
  tenantId: 'tenant-a',
  purchaseRequestId,
  companyId,
  branchId: null,
  createdByActorId: 'actor-1',
  supplier: quotationListItem.supplier,
  status: 'Draft',
  supplierQuotationReference: quotationListItem.supplierQuotationReference,
  offerDate: quotationListItem.offerDate,
  validUntil: quotationListItem.validUntil,
  currency: quotationListItem.currency,
  paymentTerm: { id: 'payment-term-1', code: 'NET30', name: 'Net 30 days', version: 1 },
  deliveryTerms: 'DAP',
  offeredDeliveryDate: '2026-09-01',
  offeredDeliveryLeadTime: null,
  notes: 'Routine office restocking quote.',
  lines: [{
    id: 'quotation-line-1', purchaseRequestLineId: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget',
    unitOfMeasureId: 'uom-1', unitOfMeasureCode: 'PCS', requestedQuantity: 5, quotedQuantity: 5, unitPrice: 100,
    discountAmount: null, discountPercentage: null, taxId: 'tax-1', taxCode: 'VAT', taxName: 'Configured VAT', taxRatePercentage: 15,
    taxAmount: 75, taxReference: null, requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', offeredDeliveryLeadTime: null,
    notes: null, version: 'LINEVERSION',
  }],
  evidence: [],
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  submittedAt: null,
  isSelected: false,
  version: 'QVERSION',
  canEdit: true,
  canSubmit: true,
  canWithdraw: false,
  canDisqualify: false,
};

test.describe('Supplier Quotation workspace', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
    await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-a', kind: 'OrdinaryMembership', tenantId: 'tenant-a', displayName: 'Alpha workspace', eligibilityVersion: 3 }] } }));
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
      entryMode: 'TenantHost', canonicalHost: '127.0.0.1', candidateTenantId: 'tenant-a', candidateTenantDisplayName: 'Alpha Tenant',
      authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
      operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Acme Trading Co.', eligibilityVersion: 1 }],
      selectedOperationalContextId: 'operation-a', operationalSelectionVersion: 1,
      branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
      currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null,
    } }));
    await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'playwright-token' }, json: { status: 'issued' } }));
  });

  async function mockProcurementAndReferences(page: import('@playwright/test').Page): Promise<void> {
    await page.route('**/api/v1/procurement/**', async (route) => {
      const url = new URL(route.request().url());
      if (url.pathname.endsWith('/organization-scopes')) return route.fulfill({ json: [{ companyId, branchId: null, companyDisplayName: 'Acme Trading Co.', branchDisplayName: null, displayName: 'Acme Trading Co.' }] });
      if (url.pathname.endsWith('/quotations')) return route.fulfill({ json: [quotationListItem] });
      if (url.pathname.endsWith('/quotation-comparison')) return route.fulfill({ json: comparison });
      if (url.pathname.endsWith('/source-decision/history')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/source-decision')) return route.fulfill({ json: null });
      if (url.pathname.endsWith('/purchase-requests/' + purchaseRequestId)) return route.fulfill({ json: requestDetail });
      if (url.pathname.endsWith('/quotations/' + quotationId + '/history')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/quotations/' + quotationId + '/audit')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/quotations/' + quotationId)) return route.fulfill({ json: quotationDetail });
      return route.fulfill({ json: [approvedRequest] });
    });
    await page.route('**/api/v1/master-data/*', (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/suppliers')) return route.fulfill({ json: [{ id: 'supplier-1', code: 'SUP-001', englishName: 'Supplier One', arabicName: 'المورد الأول', lifecycleState: 'Active' }] });
      if (path.endsWith('/currencies')) return route.fulfill({ json: [{ id: 'currency-sar', code: 'SAR', englishName: 'Saudi Riyal', arabicName: 'ريال سعودي', lifecycleState: 'Active' }] });
      if (path.endsWith('/payment-terms')) return route.fulfill({ json: [{ id: 'payment-term-1', code: 'NET30', englishName: 'Net 30 days', arabicName: '30 يوماً', lifecycleState: 'Active', currentVersionNumber: 1 }] });
      if (path.endsWith('/taxes')) return route.fulfill({ json: [{ id: 'tax-1', code: 'VAT', englishName: 'Configured VAT', arabicName: 'ضريبة مهيأة', lifecycleState: 'Active' }] });
      return route.fulfill({ json: [] });
    });
  }

  test('navigates from the sidebar into the real list and approved-request create context', async ({ page }) => {
    await mockProcurementAndReferences(page);
    await page.goto('/app');
    const procurementTile = page.locator('#app-sidebar').getByRole('button', { name: 'Procurement' });
    await procurementTile.click();
    await page.getByRole('link', { name: 'Supplier Quotations' }).click();

    await expect(page.getByTestId('supplier-quotation-list')).toBeVisible();
    await procurementTile.click();
    await expect(page.getByRole('link', { name: 'Supplier Quotations' })).toHaveAttribute('aria-current', 'page');
    await expect(page.getByText('Supplier One')).toBeVisible();
    await expect(page.getByText('Acme Trading Co.')).toBeVisible();
    await expect(page.getByText(companyId)).toHaveCount(0);

    await page.getByTestId('new-supplier-quotation').click();
    await expect(page).toHaveURL(/\/app\/procurement\/supplier-quotations\/new$/);
    await expect(page.getByTestId('quotation-request-select')).toBeVisible();
    await expect(page.getByTestId('quotation-request-select')).toContainText('PR-11111111');
    await page.getByTestId('quotation-request-select').selectOption(purchaseRequestId);
    await expect(page.getByText('SKU-001')).toBeVisible();
    await expect(page.getByText('Office restocking', { exact: true })).toBeVisible();
  });

  test('opens detail comparison and exposes source-decision rationale without a client winner', async ({ page }) => {
    await mockProcurementAndReferences(page);
    await page.goto('/app/procurement/supplier-quotations/' + quotationId);

    await expect(page.getByTestId('supplier-quotation-detail')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Submit for comparison' })).toBeVisible();
    await page.getByRole('tab', { name: 'Comparison' }).click();
    await expect(page.getByText('Comparable within this currency group')).toBeVisible();
    await expect(page.getByRole('radio', { name: /Supplier One/ })).toBeVisible();
    await expect(page.getByLabel(/Selection rationale/)).toBeVisible();
    await expect(page.locator('.winner, [data-winner]')).toHaveCount(0);
  });

  test('renders non-ISO configured currency safely in list and detail without console errors', async ({ page }) => {
    const pageErrors: string[] = [];
    page.on('pageerror', (err) => pageErrors.push(err.message));

    const s2kQuotationListItem = {
      ...quotationListItem,
      currency: { id: 'currency-s2k', code: 'S2K', name: 'Custom S2K' },
      commercialTotal: 1250,
      supplierQuotationReference: 'SUP-Q-S2K-001',
    };
    const s2kQuotationDetail = {
      ...quotationDetail,
      currency: s2kQuotationListItem.currency,
      supplierQuotationReference: 'SUP-Q-S2K-001',
    };
    const s2kComparison = {
      ...comparison,
      currencyGroups: [{ currencyId: 'currency-s2k', currencyCode: 'S2K', supplierQuotationIds: [quotationId], directlyComparableWithinGroup: true }],
      quotations: [{
        ...comparison.quotations[0],
        currency: s2kQuotationListItem.currency,
        commercialTotal: 1250,
      }],
    };

    await page.route('**/api/v1/procurement/**', async (route) => {
      const url = new URL(route.request().url());
      if (url.pathname.endsWith('/organization-scopes')) return route.fulfill({ json: [{ companyId, branchId: null, companyDisplayName: 'Acme Trading Co.', branchDisplayName: null, displayName: 'Acme Trading Co.' }] });
      if (url.pathname.endsWith('/quotations')) return route.fulfill({ json: [s2kQuotationListItem] });
      if (url.pathname.endsWith('/quotation-comparison')) return route.fulfill({ json: s2kComparison });
      if (url.pathname.endsWith('/source-decision/history')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/source-decision')) return route.fulfill({ json: null });
      if (url.pathname.endsWith('/purchase-requests/' + purchaseRequestId)) return route.fulfill({ json: requestDetail });
      if (url.pathname.endsWith('/quotations/' + quotationId + '/history')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/quotations/' + quotationId + '/audit')) return route.fulfill({ json: [] });
      if (url.pathname.endsWith('/quotations/' + quotationId)) return route.fulfill({ json: s2kQuotationDetail });
      return route.fulfill({ json: [approvedRequest] });
    });
    await page.route('**/api/v1/master-data/*', (route) => route.fulfill({ json: [] }));

    await page.goto('/app/procurement/supplier-quotations');
    await expect(page.getByTestId('supplier-quotation-list')).toBeVisible();
    await expect(page.getByText('SUP-Q-S2K-001')).toBeVisible();
    await expect(page.locator('app-data-grid app-currency-amount .currency-amount').first()).toHaveText(/1,250\.00\s*S2K/);

    await page.goto('/app/procurement/supplier-quotations/' + quotationId);
    await expect(page.getByTestId('supplier-quotation-detail')).toBeVisible();
    await expect(page.locator('app-currency-amount .currency-amount').first()).toHaveText(/1,250\.00\s*S2K/);

    expect(pageErrors.filter((e) => e.includes('RangeError'))).toHaveLength(0);
  });

  test('shows concurrency conflict error and reload action on source decision conflict without false success', async ({ page }) => {
    await mockProcurementAndReferences(page);
    await page.route('**/api/v1/procurement/purchase-requests/*/source-decision', (route) => {
      if (route.request().method() === 'POST') {
        return route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          json: { code: 'concurrency_conflict', message: 'Concurrency conflict.' },
        });
      }
      return route.fulfill({ json: null });
    });

    await page.goto('/app/procurement/supplier-quotations/' + quotationId);
    await page.getByRole('tab', { name: 'Comparison' }).click();
    await page.getByRole('radio', { name: /Supplier One/ }).click();
    await page.getByLabel(/Selection rationale/).fill('Conflicting decision attempt.');
    await page.getByRole('button', { name: /Record source decision/ }).click();

    await expect(page.locator('.inline-alert--error')).toBeVisible();
    await expect(page.getByText(/This purchase request changed since you opened it/)).toBeVisible();
    await expect(page.getByRole('button', { name: /Reload latest version/ })).toBeVisible();
    await expect(page.locator('.inline-alert--success')).toHaveCount(0);
  });
});
