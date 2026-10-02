import { expect, test, type Page } from '@playwright/test';

const tenantId = 'tenant-a';
const matchId = '11111111-1111-1111-1111-111111111111';

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

const match = (result: string, appliedExchangeRate: Record<string, unknown> | null = null) => ({
  id: matchId,
  tenantId,
  companyId: 'company-a',
  branchId: null,
  purchaseInvoiceHandoffId: '22222222-2222-2222-2222-222222222222',
  purchaseOrderId: '33333333-3333-3333-3333-333333333333',
  lifecycle: 'Current',
  result,
  evaluatedAt: '2026-08-20T08:00:00Z',
  evaluatedByActorId: 'actor-1',
  resolvedByActorId: result === 'ResolvedException' ? 'actor-2' : null,
  resolvedAt: result === 'ResolvedException' ? '2026-08-20T09:00:00Z' : null,
  resolutionReason: result === 'ResolvedException' ? 'Supporting evidence reviewed' : null,
  sourceFingerprint: 'fingerprint-12345678',
  purchaseOrderVersion: 'UE8=',
  handoffVersion: 'SFY=',
  declaredEvidenceId: '44444444-4444-4444-4444-444444444444',
  declaredEvidenceVersion: 1,
  policy: {
    policyId: 'exact-safe-default', version: 1,
    quantityAbsoluteTolerance: 0, quantityPercentageTolerance: 0,
    priceAbsoluteTolerance: 0, pricePercentageTolerance: 0,
    amountAbsoluteTolerance: 0, amountPercentageTolerance: 0,
    taxAbsoluteTolerance: 0, taxPercentageTolerance: 0,
    effectiveFrom: '0001-01-01T00:00:00Z', effectiveTo: null,
  },
  resolutionPolicy: null,
  appliedExchangeRate,
  variances: result === 'ExceptionHold' ? [{ classification: 'PriceVariance', purchaseOrderLineId: null, goodsReceiptLineId: null, expectedValue: 100, actualValue: 105, variance: 5, allowedTolerance: 0, currencyCode: 'SAR', details: 'Supplier price differs.' }] : [],
  sourceSnapshot: null,
  version: 'V2',
  varianceCount: result === 'ExceptionHold' ? 1 : 0,
});

const crossCurrencyHandoff = {
  id: '22222222-2222-2222-2222-222222222222',
  tenantId,
  companyId: 'company-a',
  branchId: null,
  purchaseOrderId: '33333333-3333-3333-3333-333333333333',
  createdByActorId: 'actor-1',
  status: 'Recorded',
  supplierId: 'supplier-a',
  supplierCode: 'SUP-EUR',
  supplierName: 'Euro Supplier',
  currencyCode: 'EUR',
  supplierInvoiceReference: 'INV-EUR-001',
  supplierInvoiceDate: '2026-08-15',
  notes: null,
  createdAt: '2026-08-15T08:00:00Z',
  updatedAt: '2026-08-15T08:00:00Z',
  cancelledAt: null,
  cancellationReason: null,
  lines: [],
  sources: [],
  version: 'HANDOFF-V2',
  canCancel: false,
  declaredEvidence: {
    id: '55555555-5555-5555-5555-555555555555',
    versionNumber: 1,
    supplierInvoiceReference: 'INV-EUR-001',
    supplierInvoiceDate: '2026-08-15',
    currencyCode: 'EUR',
    subtotalAmount: 100,
    discountAmount: 0,
    taxAmount: 15,
    grossAmount: 115,
    recordedAt: '2026-08-15T08:00:00Z',
    recordedByActorId: 'actor-1',
    lines: [],
  },
};

const crossCurrencyPurchaseOrder = {
  id: '33333333-3333-3333-3333-333333333333',
  tenantId,
  companyId: 'company-a',
  branchId: null,
  createdByActorId: 'actor-1',
  status: 'Issued',
  source: {
    purchaseRequestId: 'pr-1',
    supplierQuotationId: 'quotation-1',
    sourceDecisionId: 'decision-1',
    purchaseRequestReference: 'PR-001',
    purchaseRequestPurpose: null,
    supplierQuotationReference: 'PO-EUR-001',
    supplier: { id: 'supplier-a', code: 'SUP-EUR', name: 'Euro Supplier' },
    currency: { id: 'currency-usd', code: 'USD', name: 'US Dollar' },
    paymentTerm: null,
    sourceDecisionRationale: 'Best value',
    selectedAt: '2026-08-10T08:00:00Z',
  },
  notes: null,
  createdAt: '2026-08-10T08:00:00Z',
  updatedAt: '2026-08-10T08:00:00Z',
  submittedAt: null,
  approvedAt: '2026-08-10T09:00:00Z',
  issuedAt: '2026-08-10T10:00:00Z',
  cancelledAt: null,
  latestConfirmationId: null,
  latestConfirmationStatus: null,
  approval: null,
  lines: [],
  pendingChanges: [],
  version: 'PO-V2',
  canEdit: false,
  canSubmit: false,
  canApprove: false,
  canReject: false,
  canReturnForChange: false,
  canIssue: false,
  canCancel: false,
  canCaptureConfirmation: false,
  canApproveSupplierChange: false,
  canRejectSupplierChange: false,
};

const appliedCrossCurrencyRate = {
  exchangeRateId: 'rate-good',
  exchangeRateVersionId: 'rate-good-v2',
  versionNumber: 2,
  sourceCurrencyCode: 'EUR',
  targetCurrencyCode: 'USD',
  rate: 1.1,
  scale: 1,
  provenance: 'Configured',
  source: 'Finance monthly close',
  effectiveOn: '2026-08-15',
  effectiveFrom: '2026-07-01',
  effectiveTo: null,
};

async function installAuth(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-a', kind: 'OrdinaryMembership', tenantId, displayName: 'Alpha workspace', eligibilityVersion: 3 }] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
    entryMode: 'Tenant',
    operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'operation-a', operationalSelectionVersion: 1,
    branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null,
  } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'playwright-token' }, json: { status: 'issued' } }));
}

test.describe('Three-way matching workspace', () => {
  test.beforeEach(async ({ page }) => installAuth(page));

  test('renders an exact decision from the canonical matching endpoint and switches to Arabic RTL', async ({ page }) => {
    await page.route('**/api/v1/procurement/purchase-invoice-matches', (route) => route.fulfill({ json: [match('ExactMatch')] }));
    await page.goto('/app/procurement/invoice-matching');
    await expect(page.getByTestId('invoice-matching-list')).toBeVisible();
    await expect(page.getByRole('table').getByText('Exact match')).toBeVisible();
    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  test('shows a price hold and sends an authorized resolution reason', async ({ page }) => {
    let resolved = false;
    await page.route('**/api/v1/procurement/purchase-invoice-matches/**', (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/history')) return route.fulfill({ json: [] });
      if (path.endsWith('/audit')) return route.fulfill({ json: [] });
      if (route.request().method() === 'POST' && path.endsWith('/resolve-exception')) {
        resolved = true;
        return route.fulfill({ json: match('ResolvedException') });
      }
      return route.fulfill({ json: match(resolved ? 'ResolvedException' : 'ExceptionHold') });
    });
    await page.goto(`/app/procurement/invoice-matching/${matchId}`);
    await expect(page.getByRole('heading', { name: 'Exception hold', exact: true })).toBeVisible();
    await expect(page.getByText('PriceVariance')).toBeVisible();
    await page.getByLabel('Resolution reason').fill('Reviewed supporting evidence');
    await page.getByRole('button', { name: 'Resolve exception' }).click();
    await expect(page.getByText('Resolved by authorized review')).toBeVisible();
    expect(resolved).toBe(true);
  });

  test('selects a compatible Exchange Rate identity and sends only its id', async ({ page }) => {
    let evaluateBody: unknown = null;
    await page.route('**/api/v1/procurement/purchase-invoice-matches/**', (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/history')) return route.fulfill({ json: [] });
      if (path.endsWith('/audit')) return route.fulfill({ json: [] });
      return route.fulfill({ json: match('NotMatchReady') });
    });
    await page.route('**/api/v1/procurement/purchase-invoice-handoffs/**', (route) => {
      const path = new URL(route.request().url()).pathname;
      if (route.request().method() === 'POST' && path.endsWith('/evaluate-match')) {
        evaluateBody = route.request().postDataJSON();
        return route.fulfill({ json: match('WithinTolerance', appliedCrossCurrencyRate) });
      }
      return route.fulfill({ json: crossCurrencyHandoff });
    });
    await page.route('**/api/v1/procurement/purchase-orders/**', (route) => route.fulfill({ json: crossCurrencyPurchaseOrder }));
    await page.route('**/api/v1/master-data/exchange-rates', (route) => route.fulfill({ json: [
      {
        id: 'rate-good', tenantId, lifecycleState: 'Active', version: 'RATE-V1',
        sourceCurrencyId: 'currency-eur', targetCurrencyId: 'currency-usd',
        sourceCurrencyCode: 'EUR', targetCurrencyCode: 'USD', currentVersionNumber: 2,
        versions: [{ id: 'rate-good-v2', versionNumber: 2, effectiveFrom: '2026-07-01', effectiveTo: null, rate: 1.1, rateScale: 1, provenance: 'Configured', sourceNotes: 'Finance monthly close', sourceCurrencyCode: 'EUR', targetCurrencyCode: 'USD' }],
      },
      {
        id: 'rate-wrong-pair', tenantId, lifecycleState: 'Active', version: 'RATE-V2',
        sourceCurrencyId: 'currency-sar', targetCurrencyId: 'currency-usd',
        sourceCurrencyCode: 'SAR', targetCurrencyCode: 'USD', currentVersionNumber: 1,
        versions: [{ id: 'rate-wrong-v1', versionNumber: 1, effectiveFrom: '2026-01-01', effectiveTo: null, rate: 0.27, rateScale: 1, provenance: 'Manual', sourceNotes: null, sourceCurrencyCode: 'SAR', targetCurrencyCode: 'USD' }],
      },
    ] }));

    await page.goto(`/app/procurement/invoice-matching/${matchId}`);
    await expect(page.getByTestId('matching-exchange-rate-selector')).toBeVisible();
    await expect(page.locator('[data-testid="matching-exchange-rate-selector"] select')).toContainText('EUR → USD');
    await expect(page.locator('[data-testid="matching-exchange-rate-selector"] input')).toHaveCount(0);
    await page.getByLabel('Choose an Exchange Rate identity').selectOption('rate-good');
    await page.getByRole('button', { name: 'Evaluate current sources' }).click();
    await expect(page.getByTestId('matching-applied-exchange-rate')).toContainText('EUR → USD');
    expect(evaluateBody).toEqual({ exchangeRateReference: { exchangeRateId: 'rate-good' } });
  });
});
