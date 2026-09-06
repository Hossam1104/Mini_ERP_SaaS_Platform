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

async function setupReportingRoutes(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'reporting-playwright-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
    entryMode: 'TenantHost',
    canonicalHost: '127.0.0.1',
    candidateTenantId: 'tenant-a',
    candidateTenantDisplayName: 'Alpha Tenant',
    authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
    operationalContexts: [{ contextId: 'context-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
    selectedOperationalContextId: 'context-a',
    operationalSelectionVersion: 1,
    branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
    code: null,
  } }));
  await page.route('**/api/v1/reporting/catalogue', (route) => route.fulfill({ json: [{
    code: 'finance.trial-balance', name: 'Trial balance', arabicName: '\u0645\u064a\u0632\u0627\u0646 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629', domain: 'Finance', definitionVersion: '1.0',
    sourceOwnership: 'Finance owns posted truth.', reconciliationPath: 'Finance reconciliation', allowedFilters: ['company'], exportEnabled: true, schedulingEnabled: true, requiresCompany: true, pendingDecision: false,
  }] }));
  await page.route('**/api/v1/reporting/schedules', (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/v1\/reporting\/reports\/finance\.trial-balance(?:\?.*)?$/, (route) => route.fulfill({ json: {
    definition: { code: 'finance.trial-balance', name: 'Trial balance', arabicName: '\u0645\u064a\u0632\u0627\u0646 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629', domain: 'Finance', definitionVersion: '1.0', sourceOwnership: 'Finance owns posted truth.', reconciliationPath: 'Finance reconciliation', allowedFilters: ['company'], exportEnabled: true, schedulingEnabled: true, requiresCompany: true, pendingDecision: false },
    metadata: { resultId: 'result-a', tenantId: 'tenant-a', scope: 'Alpha Company', reportCode: 'finance.trial-balance', definitionVersion: '1.0', parameters: {}, sources: [{ sourceDomain: 'Finance', status: 'Fresh', dataAsOf: '2026-09-01T00:00:00Z' }], generatedAt: '2026-09-01T00:00:00Z', dataAsOf: '2026-09-01T00:00:00Z', state: 'Fresh', freshness: 'Current', reconciliationStatus: 'Reconciled', reconciliationOwner: 'Finance', correlationId: 'corr-reporting', explanation: 'Finance owns posted balances.', isProjected: false },
    columns: [{ key: 'account', label: 'Account', arabicLabel: '\u0627\u0644\u062d\u0633\u0627\u0628', dataType: 'text' }, { key: 'closing', label: 'Closing', arabicLabel: '\u0627\u0644\u0625\u0642\u0641\u0627\u0644', dataType: 'decimal' }],
    rows: [{ key: 'row-a', values: { account: '1000', closing: '90.00' }, lineage: [{ sourceDomain: 'Finance', sourceReference: 'account-a', sourceStatus: 'Posted', reconciliationReference: 'finance-reconciliation' }] }], totalRows: 1,
  } }));
}

test.describe('MESP-139 Reporting workspace', () => {
  test.beforeEach(async ({ page }) => setupReportingRoutes(page));

  test('renders the English catalogue, executes a source-linked report, and keeps scope server-selected', async ({ page }) => {
    await page.goto('/app/reporting');
    const reporting = page.locator('.reporting-page');
    await expect(reporting).toBeVisible();
    await expect(reporting.getByRole('heading', { level: 1 })).toContainText('A clear line back to the source.');
    await reporting.getByRole('button', { name: 'Run report' }).click();
    await expect(reporting).toContainText('1 source rows');
    await expect(reporting).toContainText('Finance');
    await expect(reporting).toContainText('Posted');
    await expect(reporting).toContainText('Server-selected; no raw identifier entry.');
  });

  test('switches the Reporting workspace to Arabic RTL', async ({ page }) => {
    await page.goto('/app/reporting');
    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('.reporting-page')).toContainText('خط واضح يعود إلى المصدر.');
  });
});
