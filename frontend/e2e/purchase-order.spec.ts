import { expect, test, type Page, type Route } from '@playwright/test';

const sourceDecisionId = 'decision-po-001';
const purchaseOrderId = 'po-001';
const purchaseOrderLineId = 'po-line-001';
const supplierQuotationLineId = 'quotation-line-001';
const tenantId = 'tenant-a';
const companyId = 'company-a';

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

const sourceOption = {
  source: {
    purchaseRequestId: 'request-001',
    supplierQuotationId: 'quotation-001',
    sourceDecisionId,
    purchaseRequestReference: 'PR-PO-001',
    purchaseRequestPurpose: 'Office restocking',
    supplierQuotationReference: 'SUP-Q-PO-001',
    supplier: { id: 'supplier-001', code: 'SUP-001', name: 'Supplier One' },
    currency: { id: 'currency-sar', code: 'SAR', name: 'Saudi Riyal' },
    paymentTerm: { id: 'term-001', code: 'NET30', name: 'Net 30 days', version: 1 },
    sourceDecisionRationale: 'Selected after server-side quotation comparison.',
    selectedAt: '2026-08-17T08:00:00Z',
  },
  companyId,
  branchId: null,
  purchaseRequestVersion: 'PR-V1',
  lines: [{
    supplierQuotationLineId,
    purchaseRequestLineId: 'request-line-001',
    productSku: 'SKU-PO-001',
    productName: 'Orderable Widget',
    unitOfMeasureCode: 'PCS',
    requestedQuantity: 5,
    selectedQuantity: 5,
    unitPrice: 100,
    discountAmount: null,
    discountPercentage: null,
    taxCode: null,
    taxName: null,
    taxRatePercentage: null,
    taxAmount: null,
    requestedNeedByDate: '2026-09-01',
    offeredDeliveryDate: '2026-09-05',
  }],
};

const authEntry = {
  entryMode: 'Tenant',
  selectedOperationalContextId: 'operation-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
};

const quotationRegressionRequest = {
  id: 'request-001', companyId, branchId: null, requesterId: 'actor-1', status: 'Approved',
  purpose: 'Office restocking', lineCount: 1, createdAt: '2026-08-17T08:00:00Z', updatedAt: '2026-08-17T08:00:00Z', version: 'PR-V1',
};

const quotationRegressionListItem = {
  id: 'quotation-001', purchaseRequestId: 'request-001', supplier: sourceOption.source.supplier, status: 'Submitted',
  supplierQuotationReference: sourceOption.source.supplierQuotationReference, offerDate: '2026-08-14', validUntil: '2026-09-14',
  currency: sourceOption.source.currency, commercialTotal: 500, coveredLineCount: 1, requestedLineCount: 1, hasEvidence: true, version: 'Q-V1',
};

function orderHarness(initialStatus: string = 'Issued', options: { staleSubmit?: boolean; denyRead?: boolean } = {}) {
  let status = initialStatus;
  let versionNumber = 1;
  let confirmedQuantity = initialStatus === 'Confirmed' ? 5 : 0;
  let unitPrice = 100;
  let pendingChangeStatus: 'PendingApproval' | 'Approved' | 'Rejected' | null = null;
  const confirmations: unknown[] = [];
  const history: unknown[] = [];
  const audit: unknown[] = [];
  const requests: { method: string; path: string; body?: unknown }[] = [];

  const version = () => `PO-V${versionNumber}`;
  const order = () => ({
    id: purchaseOrderId,
    tenantId,
    companyId,
    branchId: null,
    createdByActorId: 'requester-1',
    status,
    source: sourceOption.source,
    notes: null,
    createdAt: '2026-08-17T08:00:00Z',
    updatedAt: '2026-08-17T08:00:00Z',
    submittedAt: status === 'Draft' ? null : '2026-08-17T08:05:00Z',
    approvedAt: ['Approved', 'Issued', 'Confirmed', 'PartiallyConfirmed'].includes(status) ? '2026-08-17T08:06:00Z' : null,
    issuedAt: ['Issued', 'Confirmed', 'PartiallyConfirmed', 'ChangedPendingApproval'].includes(status) ? '2026-08-17T08:07:00Z' : null,
    cancelledAt: null,
    latestConfirmationId: confirmations.length ? 'confirmation-001' : null,
    latestConfirmationStatus: confirmations.length ? (confirmations.at(-1) as { status: string }).status : null,
    approval: ['PendingApproval', 'Approved', 'ChangedPendingApproval'].includes(status) ? {
      policyId: 'po-policy', policyVersion: 1, stageIndex: 0, stageKey: 'manager', requiredApprovals: 1,
      recordedApprovals: status === 'PendingApproval' ? 0 : 1, allowDelegation: true,
      allowRequesterCancellationWhilePending: false, isReapproval: status === 'ChangedPendingApproval',
    } : null,
    lines: [{
      id: purchaseOrderLineId,
      sourceQuotationLineId: supplierQuotationLineId,
      purchaseRequestLineId: 'request-line-001',
      productSku: 'SKU-PO-001',
      productName: 'Orderable Widget',
      unitOfMeasureCode: 'PCS',
      orderedQuantity: 5,
      confirmedQuantity,
      remainingQuantity: Math.max(0, 5 - confirmedQuantity),
      unitPrice,
      discountAmount: null,
      discountPercentage: null,
      taxCode: null,
      taxName: null,
      taxRatePercentage: null,
      taxAmount: null,
      requestedNeedByDate: '2026-09-01',
      deliveryDate: '2026-09-05',
      notes: null,
      version: version(),
    }],
    pendingChanges: pendingChangeStatus ? [{
      id: 'change-001', confirmationId: 'confirmation-001', purchaseOrderLineId,
      previousOrderedQuantity: 5, proposedQuantity: 5, previousUnitPrice: 100, proposedUnitPrice: 115,
      previousDeliveryDate: '2026-09-05', proposedDeliveryDate: '2026-09-10', status: pendingChangeStatus,
      reason: 'Supplier requested a revised price and date.', actorId: 'actor-1',
      proposedAt: '2026-08-17T08:10:00Z', decidedAt: pendingChangeStatus === 'PendingApproval' ? null : '2026-08-17T08:11:00Z',
      decisionReason: null, version: 'CHANGE-V1',
    }] : [],
    version: version(),
    canEdit: status === 'Draft',
    canSubmit: status === 'Draft',
    canApprove: status === 'PendingApproval',
    canReject: status === 'PendingApproval',
    canReturnForChange: status === 'PendingApproval',
    canIssue: status === 'Approved',
    canCancel: ['Draft', 'Approved', 'Issued', 'PartiallyConfirmed'].includes(status),
    canCaptureConfirmation: ['Issued', 'Confirmed', 'PartiallyConfirmed', 'NoResponse'].includes(status),
    canApproveSupplierChange: status === 'ChangedPendingApproval',
    canRejectSupplierChange: status === 'ChangedPendingApproval',
  });

  const record = (route: Route, body?: unknown) => {
    requests.push({ method: route.request().method(), path: new URL(route.request().url()).pathname, body });
  };

  return {
    requests,
    async route(route: Route): Promise<void> {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname;
      if (!path.includes('/api/v1/procurement/')) return route.fallback();
      const body = request.method() === 'POST' ? request.postDataJSON() : undefined;
      record(route, body);

      if (options.denyRead && request.method() === 'GET' && path.endsWith(`/purchase-orders/${purchaseOrderId}`)) {
        return route.fulfill({ status: 403, contentType: 'application/problem+json', json: { code: 'access_denied', message: 'Access denied.' } });
      }
      if (request.method() === 'GET' && path.endsWith('/purchase-order-sources')) return route.fulfill({ json: [sourceOption] });
      if (request.method() === 'GET' && path.endsWith('/purchase-orders')) return route.fulfill({ json: [order()] });
      if (request.method() === 'GET' && path.endsWith(`/purchase-orders/${purchaseOrderId}`)) return route.fulfill({ json: order() });
      if (request.method() === 'GET' && path.endsWith(`/purchase-orders/${purchaseOrderId}/confirmations`)) return route.fulfill({ json: confirmations });
      if (request.method() === 'GET' && path.endsWith(`/purchase-orders/${purchaseOrderId}/history`)) return route.fulfill({ json: history });
      if (request.method() === 'GET' && path.endsWith(`/purchase-orders/${purchaseOrderId}/audit`)) return route.fulfill({ json: audit });

      if (request.method() !== 'POST') return route.fulfill({ json: [] });
      if (path.endsWith('/purchase-orders')) {
        status = 'Draft';
        versionNumber += 1;
        history.push({ evidenceId: `history-${versionNumber}`, purchaseOrderId, occurredAt: '2026-08-17T08:01:00Z', fromStatus: 'Draft', toStatus: 'Draft', action: 'Created', actorId: 'actor-1', reason: null, correlationId: 'corr-po', policyId: null, policyVersion: null, stageKey: null, delegatedFromActorId: null });
        return route.fulfill({ status: 201, headers: { ETag: `"${version()}"` }, json: order() });
      }
      if (options.staleSubmit && path.endsWith('/submit')) {
        return route.fulfill({ status: 409, contentType: 'application/problem+json', json: { code: 'concurrency_conflict', message: 'Concurrency conflict.' } });
      }
      if (path.endsWith('/submit')) status = 'PendingApproval';
      if (path.endsWith('/approve')) status = 'Approved';
      if (path.endsWith('/issue')) status = 'Issued';
      if (path.endsWith('/supplier-change/approve')) {
        status = 'Confirmed';
        unitPrice = 115;
        pendingChangeStatus = 'Approved';
      }
      if (path.endsWith('/supplier-change/reject')) {
        status = 'Issued';
        pendingChangeStatus = 'Rejected';
      }
      if (path.endsWith('/confirmations')) {
        const confirmation = body as { status: string; lines: { confirmedQuantity: number; proposedQuantity: number | null; proposedUnitPrice: number | null }[] };
        const hasChange = confirmation.lines.some((line) => line.proposedQuantity !== null || line.proposedUnitPrice !== null);
        confirmedQuantity = confirmation.status === 'Confirmed' ? 5 : confirmation.status === 'PartiallyConfirmed' ? 2 : 0;
        status = hasChange ? 'ChangedPendingApproval' : confirmation.status;
        pendingChangeStatus = hasChange ? 'PendingApproval' : null;
        const line = confirmation.lines[0];
        confirmations.push({
          id: 'confirmation-001', purchaseOrderId, status: confirmation.status, responseDate: '2026-08-17', supplierReference: null,
          supplierContact: null, reason: null, notes: null, recordedByActorId: 'actor-1', recordedAt: '2026-08-17T08:10:00Z',
          purchaseOrderVersion: version(), version: 'CONFIRM-V1',
          lines: [{ id: 'confirmation-line-001', purchaseOrderLineId, orderedQuantityAtResponse: 5, confirmedQuantity: line.confirmedQuantity, remainingQuantity: Math.max(0, 5 - line.confirmedQuantity), expectedDeliveryDate: '2026-09-05', proposedQuantity: line.proposedQuantity, proposedUnitPrice: line.proposedUnitPrice, proposedDeliveryDate: null, changeReason: hasChange ? 'Supplier requested a revised price and date.' : null, version: 'CONFIRM-LINE-V1' }],
          evidence: [], changes: hasChange ? [{ id: 'change-001', confirmationId: 'confirmation-001', purchaseOrderLineId, previousOrderedQuantity: 5, proposedQuantity: line.proposedQuantity, previousUnitPrice: 100, proposedUnitPrice: line.proposedUnitPrice, previousDeliveryDate: '2026-09-05', proposedDeliveryDate: '2026-09-10', status: 'PendingApproval', reason: 'Supplier requested a revised price and date.', actorId: 'actor-1', proposedAt: '2026-08-17T08:10:00Z', decidedAt: null, decisionReason: null, version: 'CHANGE-V1' }] : [],
        });
      }
      versionNumber += 1;
      history.push({ evidenceId: `history-${versionNumber}`, purchaseOrderId, occurredAt: '2026-08-17T08:12:00Z', fromStatus: 'Issued', toStatus: status, action: 'Lifecycle', actorId: 'actor-1', reason: null, correlationId: 'corr-po', policyId: 'po-policy', policyVersion: 1, stageKey: 'manager', delegatedFromActorId: null });
      audit.push({ evidenceId: `audit-${versionNumber}`, purchaseOrderId, occurredAt: '2026-08-17T08:12:00Z', operationId: path, correlationId: 'corr-po', tenantId, actorId: 'actor-1', sessionId: 'session-1', authorizationPath: 'OrdinaryMembership', decision: 'Allowed', reason: null, beforeStatus: null, afterStatus: status, companyId, branchId: null, beforeSummary: null, afterSummary: status, idempotencyKey: 'playwright-key' });
      return route.fulfill({ headers: { ETag: `"${version()}"` }, json: order() });
    },
  };
}

async function installAuth(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-a', kind: 'OrdinaryMembership', tenantId, displayName: 'Alpha workspace', eligibilityVersion: 3 }] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: authEntry }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'playwright-token' }, json: { status: 'issued' } }));
}

test.describe('Purchase Order and Supplier Confirmation workspace', () => {
  test.beforeEach(async ({ page }) => installAuth(page));

  test('creates from an eligible source decision, then approves and issues without downstream effects', async ({ page }) => {
    const harness = orderHarness('Draft');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto('/app/procurement/purchase-orders/new');
    await page.getByTestId('purchase-order-source').selectOption(sourceDecisionId);
    await expect(page.getByText('SKU-PO-001')).toBeVisible();
    await page.getByTestId('create-purchase-order').click();
    await expect(page).toHaveURL(new RegExp(`/app/procurement/purchase-orders/${purchaseOrderId}$`));
    await page.getByRole('button', { name: 'Submit for approval' }).click();
    await expect(page.locator('.status-badge--pending')).toBeVisible();
    await page.getByRole('button', { name: 'Approve', exact: true }).click();
    await expect(page.locator('.status-badge--approved')).toBeVisible();
    await page.getByRole('button', { name: 'Issue to supplier' }).click();
    await expect(page.locator('.status-badge--issued')).toBeVisible();
    expect(harness.requests.filter((request) => request.method === 'POST').map((request) => request.path)).toEqual([
      '/api/v1/procurement/purchase-orders',
      `/api/v1/procurement/purchase-orders/${purchaseOrderId}/submit`,
      `/api/v1/procurement/purchase-orders/${purchaseOrderId}/approve`,
      `/api/v1/procurement/purchase-orders/${purchaseOrderId}/issue`,
    ]);
  });

  test('records full and partial supplier confirmation with exact remainder semantics', async ({ page }) => {
    const harness = orderHarness('Issued');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await page.getByRole('button', { name: 'Record supplier response' }).click();
    await page.locator('[data-testid="purchase-order-detail"] input[type="number"]').first().fill('5');
    await page.getByTestId('capture-supplier-confirmation').click();
    await expect(page.locator('.status-badge--confirmed')).toBeVisible();

    const partialHarness = orderHarness('Issued');
    await page.unroute('**/api/v1/procurement/**');
    await page.route('**/api/v1/procurement/**', partialHarness.route);
    await page.reload();
    await page.getByRole('button', { name: 'Record supplier response' }).click();
    await page.locator('[data-testid="purchase-order-detail"] select').first().selectOption('PartiallyConfirmed');
    await page.locator('[data-testid="purchase-order-detail"] input[type="number"]').first().fill('2');
    await page.getByTestId('capture-supplier-confirmation').click();
    await expect(page.locator('.status-badge--partial')).toBeVisible();
    await page.getByRole('tab', { name: 'Lines' }).click();
    await expect(page.getByText('3', { exact: true })).toBeVisible();
  });

  test('records explicit supplier rejection and preserves an auditable terminal state', async ({ page }) => {
    const harness = orderHarness('Issued');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await page.getByRole('button', { name: 'Record supplier response' }).click();
    await page.locator('[data-testid="purchase-order-detail"] select').first().selectOption('Rejected');
    await page.locator('[data-testid="purchase-order-detail"] textarea').last().fill('Supplier declined the order.');
    await page.getByTestId('capture-supplier-confirmation').click();
    await expect(page.locator('.status-badge--rejected')).toBeVisible();
    await page.getByRole('tab', { name: 'History' }).click();
    await expect(page.locator('.timeline').getByText(/Issued.*Rejected/)).toBeVisible();
  });

  test('explains new-source-decision recovery for a terminal rejected order', async ({ page }) => {
    const harness = orderHarness('Rejected');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await expect(page.getByTestId('purchase-order-terminal-recovery')).toContainText('consumed its source decision');
    await expect(page.getByTestId('purchase-order-terminal-recovery')).toContainText('Controlled reopening is not available');
  });

  test('keeps original values visible while supplier changes await reapproval, then applies the approved proposal', async ({ page }) => {
    const harness = orderHarness('Issued');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await page.getByRole('button', { name: 'Record supplier response' }).click();
    const numbers = page.locator('[data-testid="purchase-order-detail"] input[type="number"]');
    await numbers.first().fill('5');
    await numbers.nth(1).fill('5');
    await numbers.nth(2).fill('115');
    await page.locator('[data-testid="purchase-order-detail"] input[type="text"]').first().fill('Supplier requested a revised price and date.');
    await page.getByTestId('capture-supplier-confirmation').click();
    await expect(page.locator('.status-badge--changed')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Approve supplier change' })).toBeVisible();
    await page.getByRole('button', { name: 'Approve supplier change' }).click();
    await page.getByRole('tab', { name: 'Lines' }).click();
    await expect(page.getByText(/115\.00/)).toBeVisible();
  });

  test('shows a stale concurrency conflict without false lifecycle success', async ({ page }) => {
    const harness = orderHarness('Draft', { staleSubmit: true });
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await page.getByRole('button', { name: 'Submit for approval' }).click();
    await expect(page.locator('.inline-error')).toBeVisible();
    await expect(page.getByText(/changed elsewhere/i)).toBeVisible();
    await expect(page.locator('.status-badge--draft')).toBeVisible();
  });

  test('renders Tenant or operational-scope denial as a safe no-access state', async ({ page }) => {
    const harness = orderHarness('Issued', { denyRead: true });
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await expect(page.getByRole('alert')).toContainText(/current access/i);
    await expect(page.getByTestId('purchase-order-detail')).toHaveCount(0);
  });

  test('keeps English/Arabic direction switching and the existing MESP-123 quotation surface working', async ({ page }) => {
    const harness = orderHarness('Issued');
    await page.route('**/api/v1/procurement/**', harness.route);
    await page.route('**/api/v1/master-data/*', (route) => route.fulfill({ json: [] }));
    await page.goto(`/app/procurement/purchase-orders/${purchaseOrderId}`);
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    await page.unroute('**/api/v1/procurement/**');
    await page.route('**/api/v1/procurement/**', async (route) => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/organization-scopes')) return route.fulfill({ json: [{ companyId, branchId: null, companyDisplayName: 'Alpha Company', branchDisplayName: null, displayName: 'Alpha Company' }] });
      if (path.endsWith('/quotation-comparison')) return route.fulfill({ json: { currentSourceDecision: null, quotations: [], currencyGroups: [], hasMixedCurrencies: false, directCurrencyComparisonAvailable: true, comparisonBasis: 'Server-calculated commercial total' } });
      if (path.endsWith('/purchase-requests')) return route.fulfill({ json: [quotationRegressionRequest] });
      if (path.endsWith('/quotations')) return route.fulfill({ json: [quotationRegressionListItem] });
      return route.fulfill({ json: [] });
    });
    await page.goto('/app/procurement/supplier-quotations');
    await expect(page.getByTestId('supplier-quotation-list')).toBeVisible();
    await expect(page.getByText('SUP-Q-PO-001')).toBeVisible();
  });
});
