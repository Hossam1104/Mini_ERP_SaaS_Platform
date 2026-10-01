import { test, expect } from '@playwright/test';
import { resolve } from 'node:path';

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
  authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost', arabicDisplayName: 'وفرة' }],
  operationalContexts: [{ contextId: 'operation-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 }],
  selectedOperationalContextId: 'operation-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true, arabicDisplayName: 'وفرة' },
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
          arabicDisplayName: 'وفرة',
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
    await expect(page.locator('#context-switcher-title')).toBeVisible();
    await page.locator('#workspace-select').selectOption('context-a');
    await expect(page.locator('#context-switcher-title')).toHaveText('Alpha workspace');
  });

  test('shows configured Arabic Tenant names in the header, Overview, account menu, and workspaces page', async ({ page }) => {
    await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: sessionWithContext }));
    await page.goto('/app');
    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('#tenant-overview-title')).toHaveText('وفرة');
    await expect(page.locator('#context-trigger .context-chip__copy strong')).toHaveText('وفرة');

    await page.locator('#account-trigger').click();
    await expect(page.locator('.account-menu__identity')).toContainText('وفرة');
    await page.screenshot({
      path: resolve(process.cwd(), '../.playwright-mcp/mesp-202/arabic-overview-account.png'),
      fullPage: false,
    });

    await page.goto('/app/workspaces');
    await expect(page.locator('#context-switcher-title')).toHaveText('وفرة');
    await expect(page.locator('#workspace-select option[value="context-a"]')).toContainText('وفرة');
    await page.screenshot({
      path: resolve(process.cwd(), '../.playwright-mcp/mesp-202/arabic-workspaces.png'),
      fullPage: false,
    });
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
