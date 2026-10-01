import { test, expect } from '@playwright/test';

const sessionWithoutContext = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 0,
};

const sessionWithContext = {
  ...sessionWithoutContext,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

const tenantEntry = {
  entryMode: 'TenantHost',
  canonicalHost: '127.0.0.1',
  candidateTenantId: 'tenant-a',
  candidateTenantDisplayName: 'Alpha Tenant',
  authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
  operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
  selectedOperationalContextId: 'operation-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
};

test.describe('Tenant-aware shell', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithoutContext }));
    await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({
      json: {
        contexts: [{
          contextId: 'context-a',
          kind: 'OrdinaryMembership',
          tenantId: 'tenant-a',
          displayName: 'Alpha workspace',
          eligibilityVersion: 3,
        }],
      },
    }));
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: tenantEntry }));
    await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({
      headers: { 'X-CSRF-TOKEN': 'playwright-token' },
      json: { status: 'issued' },
    }));
    await page.route('**/api/v1/auth/context-switch', (route) => route.fulfill({
      json: {
        ...sessionWithoutContext,
        selectedPath: 'OrdinaryMembership',
        selectedTenantId: 'tenant-a',
        selectedContextId: 'context-a',
        selectionVersion: 1,
      },
    }));
  });

  test('enters the Tenant Overview first and keeps the legacy context route available', async ({ page }) => {
    await page.goto('/app');
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha Tenant');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    await page.getByRole('button', { name: 'Language' }).click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');

    await page.goto('/app/workspaces');
    await expect(page.locator('.context-summary')).toContainText('Alpha Tenant');
    await expect(page.locator('#workspace-select')).toHaveCount(0);
    await expect(page.getByText('No Tenant context selected')).toHaveCount(0);
  });

  test('Tenant host shows a fixed Tenant and only Company/Branch choices', async ({ page }) => {
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
      ...tenantEntry,
      canonicalHost: 'wafra.localhost',
      operationalContexts: [
        { contextId: 'company-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 },
        { contextId: 'branch-a', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 1 },
      ],
      selectedOperationalContextId: 'company-a',
    } }));

    await page.goto('/app/workspaces');

    const summary = page.locator('.context-summary');
    await expect(summary).toContainText('Alpha Tenant');
    await expect(summary).toContainText('Alpha Company · Company');
    await expect(page.locator('#workspace-select')).toHaveCount(0);
    await expect(page.locator('app-context-switcher')).toHaveCount(0);
    await expect(page.locator('#operational-context-select option')).toHaveText([
      'Alpha Company - Company',
      'Alpha Branch - Branch',
    ]);
    await expect(page.getByText('No Tenant context selected')).toHaveCount(0);
  });

  test('common host shows a Tenant chooser only when there are multiple memberships', async ({ page }) => {
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
      ...tenantEntry,
      entryMode: 'CommonHost',
      canonicalHost: 'mesp.localhost',
      candidateTenantId: null,
      candidateTenantDisplayName: null,
      authorizedTenants: [
        ...tenantEntry.authorizedTenants,
        { tenantId: 'tenant-b', displayName: 'Beta Tenant', canonicalHost: 'beta.localhost' },
      ],
      operationalContexts: [],
      selectedOperationalContextId: null,
      branding: { ...tenantEntry.branding, displayName: 'MESP', tenantConfigured: false },
    } }));
    await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [
      { contextId: 'context-a', kind: 'OrdinaryMembership', tenantId: 'tenant-a', displayName: 'Alpha Tenant', eligibilityVersion: 1 },
      { contextId: 'context-b', kind: 'OrdinaryMembership', tenantId: 'tenant-b', displayName: 'Beta Tenant', eligibilityVersion: 1 },
    ] } }));

    await page.goto('/app/workspaces');

    await expect(page.locator('#workspace-select')).toBeVisible();
    await expect(page.locator('#workspace-select option:not([disabled])')).toHaveText([
      'Alpha Tenant · Tenant membership',
      'Beta Tenant · Tenant membership',
    ]);
    await expect(page.locator('#operational-context-select')).toHaveCount(0);
  });

  test('common host with one membership shows its fixed Tenant without a chooser', async ({ page }) => {
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: {
      ...tenantEntry,
      entryMode: 'CommonHost',
      canonicalHost: 'mesp.localhost',
    } }));

    await page.goto('/app/workspaces');

    await expect(page.locator('.context-summary')).toContainText('Alpha Tenant');
    await expect(page.locator('#workspace-select')).toHaveCount(0);
    await expect(page.getByText('No Tenant context selected')).toHaveCount(0);
  });

  test('keeps the authenticated shell and selected Tenant after an unconfirmed sign-out', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    let signOutAttempts = 0;
    await page.route('**/api/v1/auth/sign-out', (route) => {
      signOutAttempts += 1;
      return route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ code: 'audit_unavailable' }),
      });
    });

    await page.goto('/app');
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha Tenant');
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page.getByRole('alert')).toHaveText(
      'Sign-out could not be confirmed. Your session may still be active. Please try again.',
    );
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha Tenant');
    await expect(page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' })).toBeEnabled();
    expect(signOutAttempts).toBe(1);
    expect(await page.evaluate(() => ({
      localStorage: localStorage.length,
      sessionStorage: sessionStorage.length,
      cookie: document.cookie,
    }))).toEqual({ localStorage: 0, sessionStorage: 0, cookie: '' });
  });

  test('retries an unconfirmed sign-out and clears protected state after 204', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    let signOutAttempts = 0;
    await page.route('**/api/v1/auth/sign-out', (route) => {
      signOutAttempts += 1;
      if (signOutAttempts === 1) {
        return route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({ code: 'request_failed' }),
        });
      }
      return route.fulfill({ status: 204, body: '' });
    });

    await page.goto('/app');
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to MESP' })).toBeVisible();
    expect(signOutAttempts).toBe(2);
    await expect(page.locator('#tenant-overview-title')).toHaveCount(0);
  });

  test('treats a server-confirmed 401 as an expired session and returns to login', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    await page.route('**/api/v1/auth/sign-out', (route) => route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'authentication_failed' }),
    }));

    await page.goto('/app');
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to MESP' })).toBeVisible();
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(await page.evaluate(() => ({ localStorage: localStorage.length, sessionStorage: sessionStorage.length })))
      .toEqual({ localStorage: 0, sessionStorage: 0 });
  });
});
