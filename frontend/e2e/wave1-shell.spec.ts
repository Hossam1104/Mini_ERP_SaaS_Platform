import { test, expect, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const screenshotDir = resolve(process.cwd(), '../.playwright-mcp/mesp-204');
mkdirSync(screenshotDir, { recursive: true });

const alphaLogo = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 80"%3E%3Crect width="240" height="80" rx="16" fill="%231f6c5a"/%3E%3Ctext x="120" y="51" fill="white" font-family="Arial" font-size="30" font-weight="700" text-anchor="middle"%3EALPHA ERP%3C/text%3E%3C/svg%3E';
const betaLogo = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 80"%3E%3Crect width="240" height="80" rx="16" fill="%235e4b8b"/%3E%3Ctext x="120" y="51" fill="white" font-family="Arial" font-size="30" font-weight="700" text-anchor="middle"%3EBETA ERP%3C/text%3E%3C/svg%3E';

type AccountKind = 'ordinary' | 'emergency';

const baseSession = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'company-a',
  selectionVersion: 1,
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
  isEmergencySuperAdministrator: false,
};

function sessionFor(kind: AccountKind, tenantId: string | null = null) {
  return {
    ...baseSession,
    selectedPath: tenantId ? 'OrdinaryMembership' : null,
    selectedTenantId: tenantId,
    selectedContextId: tenantId ? `company-${tenantId.slice(-1)}` : null,
    selectionVersion: tenantId ? 2 : 1,
    login: kind === 'emergency' ? 'admin@mesp.com' : 'amina@example.com',
    displayName: kind === 'emergency' ? 'Emergency Administrator' : 'Amina Hassan',
    isEmergencySuperAdministrator: kind === 'emergency',
  };
}

function tenantEntry(tenantId: string) {
  const beta = tenantId === 'tenant-b';
  return {
    entryMode: 'Tenant',
    operationalContexts: [{
      contextId: beta ? 'company-b' : 'company-a',
      kind: 'Company',
      displayName: beta ? 'Beta Company' : 'Alpha Company',
      eligibilityVersion: 1,
    }],
    selectedOperationalContextId: beta ? 'company-b' : 'company-a',
    operationalSelectionVersion: 1,
    branding: {
      displayName: beta ? 'Beta ERP' : 'Alpha ERP',
      arabicDisplayName: beta ? '\u0628\u064a\u062a\u0627 ERP' : '\u0623\u0644\u0641\u0627 ERP',
      logoLightUrl: beta ? betaLogo : alphaLogo,
      logoDarkUrl: null,
      logoAltText: beta ? 'Beta ERP' : 'Alpha ERP',
      tenantConfigured: true,
    },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
    code: null,
    isDevelopment: true,
    developmentAccountHint: 'admin@mesp.com / 123',
  };
}

const signInEntry = {
  entryMode: 'SignIn',
  operationalContexts: [],
  selectedOperationalContextId: null,
  operationalSelectionVersion: 0,
  branding: { displayName: 'MESP', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'MESP', tenantConfigured: false },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
  isDevelopment: true,
  developmentAccountHint: 'admin@mesp.com / 123',
};

async function setupLoginFlow(page: Page) {
  let kind: AccountKind = 'ordinary';
  let authenticated = false;
  let selectedTenant: string | null = null;
  let listRequests = 0;
  let signOutAttempts = 0;

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/module-registration', (route) => route.fulfill({ json: {
    module: 'platform-administration', name: 'Platform Administration', boundary: 'platform', registered: true,
    masterData: { module: 'master-data-catalog', name: 'Master Data Catalog', boundary: 'master-data', registered: true },
    businessParties: { module: 'business-parties', name: 'Business Parties', boundary: 'business-parties', registered: true },
  } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: authenticated ? sessionFor(kind, selectedTenant) : { authenticated: false } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: !authenticated
    ? signInEntry
    : kind === 'emergency' && selectedTenant === null
      ? { ...signInEntry, entryMode: 'EmergencySuperAdministrator' }
      : tenantEntry(selectedTenant ?? 'tenant-a') }));
  await page.route('**/api/v1/auth/sign-in', async (route) => {
    const body = route.request().postDataJSON() as { login?: string };
    kind = body.login === 'admin@mesp.com' ? 'emergency' : 'ordinary';
    selectedTenant = kind === 'ordinary' ? 'tenant-a' : null;
    authenticated = true;
    return route.fulfill({ json: sessionFor(kind, selectedTenant) });
  });
  await page.route('**/api/v1/auth/emergency-tenants', (route) => {
    listRequests += 1;
    return route.fulfill({ json: { tenants: [
      { tenantId: 'tenant-a', displayName: 'Alpha ERP', arabicDisplayName: '\u0623\u0644\u0641\u0627 ERP' },
      { tenantId: 'tenant-b', displayName: 'Beta ERP', arabicDisplayName: '\u0628\u064a\u062a\u0627 ERP' },
    ] } });
  });
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({
    headers: { 'X-CSRF-TOKEN': 'playwright-token' },
    json: { status: 'issued' },
  }));
  await page.route('**/api/v1/auth/emergency-tenant-switch', async (route) => {
    const body = route.request().postDataJSON() as { tenantId?: string };
    selectedTenant = body.tenantId ?? null;
    return route.fulfill({ json: sessionFor('emergency', selectedTenant) });
  });
  await page.route('**/api/v1/auth/sign-out', async (route) => {
    signOutAttempts += 1;
    return route.fulfill({ status: 204, body: '' });
  });

  return {
    listRequests: () => listRequests,
    signOutAttempts: () => signOutAttempts,
    setSignOutHandler: async (handler: (route: import('@playwright/test').Route) => Promise<unknown>) => {
      await page.unroute('**/api/v1/auth/sign-out');
      await page.route('**/api/v1/auth/sign-out', (route) => {
        signOutAttempts += 1;
        return handler(route);
      });
    },
  };
}

async function signIn(page: Page, login = 'amina@example.com', password = 'Example-Password-1!'): Promise<void> {
  await page.locator('#username').fill(login);
  await page.locator('#password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
}

test.describe('account-resolved entry and shell', () => {
  test('normal account enters its branded Tenant Overview with no Tenant dropdown', async ({ page }) => {
    const state = await setupLoginFlow(page);
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: 'Sign in to MESP' })).toBeVisible();
    await expect(page.locator('.auth-brand__name')).toHaveText('MESP');
    await page.screenshot({ path: resolve(screenshotDir, 'sign-in-en.png'), animations: 'disabled' });
    expect(state.listRequests()).toBe(0);

    await signIn(page);
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    await expect(page.locator('.overview-hero__tenant-logo img')).toHaveAttribute('alt', 'Alpha ERP');
    await expect(page.locator('.overview-hero__tenant-logo img')).toHaveAttribute('src', alphaLogo);
    await expect(page.locator('#tenant-context')).toHaveCount(0);
    await expect(page.locator('#emergency-tenant-select')).toHaveCount(0);
    await expect(page.locator('#context-trigger')).toHaveCount(0);
    await expect(page.locator('.context-chip--static')).toContainText('Alpha ERP');
    await expect(page.locator('.context-chip--static')).toContainText('Alpha Company');
    await page.screenshot({ path: resolve(screenshotDir, 'ordinary-overview-en.png'), animations: 'disabled' });
    await page.screenshot({ path: resolve(screenshotDir, 'ordinary-header-en.png'), animations: 'disabled' });

    await page.locator('button.language-button.header-control').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('#tenant-overview-title')).toHaveText('\u0623\u0644\u0641\u0627 ERP');
    await expect(page.locator('#context-trigger')).toHaveCount(0);
    await page.screenshot({ path: resolve(screenshotDir, 'ordinary-header-ar.png'), animations: 'disabled' });
  });

  test('emergency admin chooses on /login, enters branded Overview, and switches Tenant in the header', async ({ page }) => {
    const state = await setupLoginFlow(page);
    await page.goto('/login');
    await signIn(page, 'admin@mesp.com', '123');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Choose a Tenant' })).toBeVisible();
    const tenantSelect = page.locator('#tenant-context');
    await expect(tenantSelect.locator('option')).toHaveText(['Select a Tenant', 'Alpha ERP', 'Beta ERP']);
    expect(state.listRequests()).toBe(1);
    await page.screenshot({ path: resolve(screenshotDir, 'super-admin-login-step2-en.png'), animations: 'disabled' });

    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(tenantSelect.locator('option')).toHaveText(['\u0627\u062e\u062a\u0631 \u0639\u0645\u064a\u0644\u0627\u064b', '\u0623\u0644\u0641\u0627 ERP', '\u0628\u064a\u062a\u0627 ERP']);
    await page.screenshot({ path: resolve(screenshotDir, 'super-admin-login-step2-ar.png'), animations: 'disabled' });
    await page.locator('.language-button').click();

    await tenantSelect.selectOption('tenant-a');
    await page.getByRole('button', { name: 'Continue' }).click();
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    await expect(page.locator('.topbar__tenant-logo')).toHaveAttribute('alt', 'Alpha ERP');
    await expect(page.locator('#context-trigger')).toBeVisible();
    await expect(page.locator('#emergency-tenant-select')).toHaveCount(0);
    await page.screenshot({ path: resolve(screenshotDir, 'super-admin-overview-en.png'), animations: 'disabled' });
    await page.locator('#context-trigger').click();
    const headerTenantSelect = page.locator('#emergency-tenant-select');
    await expect(headerTenantSelect.locator('option')).toHaveText(['Alpha ERP', 'Beta ERP']);
    await page.screenshot({ path: resolve(screenshotDir, 'super-admin-header-en.png'), animations: 'disabled' });
    await page.keyboard.press('Escape');

    await page.locator('button.language-button.header-control').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('#tenant-overview-title')).toHaveText('\u0623\u0644\u0641\u0627 ERP');
    await page.locator('#context-trigger').click();
    await expect(headerTenantSelect.locator('option')).toHaveText(['\u0623\u0644\u0641\u0627 ERP', '\u0628\u064a\u062a\u0627 ERP']);
    await page.screenshot({ path: resolve(screenshotDir, 'super-admin-header-ar.png'), animations: 'disabled' });

    await headerTenantSelect.selectOption('tenant-b');
    await expect(page.locator('#tenant-overview-title')).toHaveText('\u0628\u064a\u062a\u0627 ERP');
    await expect(page.locator('.topbar__tenant-logo')).toHaveAttribute('alt', 'Beta ERP');
    await expect(page).toHaveURL(/\/app$/);
    expect(state.listRequests()).toBe(1);
  });

  test('the removed Manage access contexts URL does not expose a page or menu item', async ({ page }) => {
    await setupLoginFlow(page);
    await page.goto('/login');
    await signIn(page);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    await expect(page.locator('a[href="/app/workspaces"]')).toHaveCount(0);
    await page.goto('/app/workspaces');
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    await page.goto('/access-contexts');
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
  });

  test('keeps the authenticated Tenant after an unconfirmed sign-out', async ({ page }) => {
    const state = await setupLoginFlow(page);
    await state.setSignOutHandler((route) => route.fulfill({
      status: 503,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'audit_unavailable' }),
    }));
    await page.goto('/login');
    await signIn(page);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page.getByRole('alert')).toContainText('Sign-out could not be confirmed.');
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha ERP');
    expect(state.signOutAttempts()).toBe(1);
  });

  test('retries an unconfirmed sign-out and clears protected state after 204', async ({ page }) => {
    const state = await setupLoginFlow(page);
    await state.setSignOutHandler(async (route) => state.signOutAttempts() === 1
      ? route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ code: 'request_failed' }) })
      : route.fulfill({ status: 204, body: '' }));
    await page.goto('/login');
    await signIn(page);
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();
    await expect(page.getByRole('alert')).toBeVisible();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to MESP' })).toBeVisible();
    expect(state.signOutAttempts()).toBe(2);
  });

  test('returns to login when sign-out confirms the session is expired', async ({ page }) => {
    const state = await setupLoginFlow(page);
    await state.setSignOutHandler((route) => route.fulfill({
      status: 401,
      contentType: 'application/json',
      body: JSON.stringify({ code: 'authentication_failed' }),
    }));
    await page.goto('/login');
    await signIn(page);
    await page.getByRole('button', { name: 'Account' }).click();
    await page.getByRole('menu', { name: 'Account menu' }).getByRole('menuitem', { name: 'Sign out' }).click();

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: 'Sign in to MESP' })).toBeVisible();
    expect(state.signOutAttempts()).toBe(1);
  });
});
