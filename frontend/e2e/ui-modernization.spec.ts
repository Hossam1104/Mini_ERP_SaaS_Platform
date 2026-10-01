import { expect, test, type Locator, type Page, type Route } from '@playwright/test';

const tenantId = 'tenant-ui';
const companyId = 'company-ui';
const operationalContexts = [
  { contextId: 'operation-ui', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 },
  { contextId: 'operation-branch-ui', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 1 },
  { contextId: 'operation-company-ui', kind: 'Company', displayName: 'Beta Company', eligibilityVersion: 1 },
];
const orderRows = [
  { id: 'po-1', status: 'Issued', supplierCode: 'SUP-01', supplierName: 'Aster Supplies', supplierQuotationReference: 'QT-001', currencyCode: 'SAR', total: 100, lineCount: 2, createdAt: '2026-08-01T08:00:00Z', updatedAt: '2026-08-10T08:00:00Z', version: 'PO-V1' },
  { id: 'po-2', status: 'Approved', supplierCode: 'SUP-02', supplierName: 'Birch Industrial', supplierQuotationReference: 'QT-002', currencyCode: 'USD', total: 250, lineCount: 3, createdAt: '2026-08-02T08:00:00Z', updatedAt: '2026-08-11T08:00:00Z', version: 'PO-V1' },
  { id: 'po-3', status: 'Draft', supplierCode: 'SUP-03', supplierName: 'Cedar Office', supplierQuotationReference: 'QT-003', currencyCode: 'SAR', total: 400, lineCount: 1, createdAt: '2026-08-03T08:00:00Z', updatedAt: '2026-08-12T08:00:00Z', version: 'PO-V1' },
  { id: 'po-4', status: 'Issued', supplierCode: 'SUP-04', supplierName: 'Dune Trading', supplierQuotationReference: 'QT-004', currencyCode: 'EUR', total: 550, lineCount: 4, createdAt: '2026-08-04T08:00:00Z', updatedAt: '2026-08-13T08:00:00Z', version: 'PO-V1' },
  { id: 'po-5', status: 'Approved', supplierCode: 'SUP-05', supplierName: 'Elm Services', supplierQuotationReference: 'QT-005', currencyCode: 'USD', total: 700, lineCount: 2, createdAt: '2026-08-05T08:00:00Z', updatedAt: '2026-08-14T08:00:00Z', version: 'PO-V1' },
  { id: 'po-6', status: 'Draft', supplierCode: 'SUP-06', supplierName: 'Fennel Logistics', supplierQuotationReference: 'QT-006', currencyCode: 'SAR', total: 850, lineCount: 3, createdAt: '2026-08-06T08:00:00Z', updatedAt: '2026-08-15T08:00:00Z', version: 'PO-V1' },
  { id: 'po-7', status: 'Issued', supplierCode: 'SUP-07', supplierName: 'Grove Manufacturing', supplierQuotationReference: 'QT-007', currencyCode: 'EUR', total: 1000, lineCount: 5, createdAt: '2026-08-07T08:00:00Z', updatedAt: '2026-08-16T08:00:00Z', version: 'PO-V1' },
  { id: 'po-8', status: 'Approved', supplierCode: 'SUP-08', supplierName: 'Harbor Goods', supplierQuotationReference: 'QT-008', currencyCode: 'USD', total: 1150, lineCount: 2, createdAt: '2026-08-08T08:00:00Z', updatedAt: '2026-08-17T08:00:00Z', version: 'PO-V1' },
];

const orderDetail = (id: string) => {
  const row = orderRows.find((candidate) => candidate.id === id) ?? orderRows[0];
  return {
    ...row,
    tenantId,
    companyId,
    branchId: null,
    createdByActorId: 'actor-ui',
    source: {
      purchaseRequestId: 'pr-ui', purchaseRequestReference: 'PR-UI-001', purchaseRequestPurpose: 'Office supplies',
      supplierQuotationId: 'quotation-ui', supplierQuotationReference: row.supplierQuotationReference,
      supplier: { id: `supplier-${id}`, code: row.supplierCode, name: row.supplierName },
      currency: { id: 'currency-ui', code: row.currencyCode, name: row.currencyCode }, paymentTerm: null,
      sourceDecisionId: 'decision-ui', sourceDecisionRationale: 'Selected by the buyer.', selectedAt: row.createdAt,
    },
    notes: null, submittedAt: row.createdAt, approvedAt: row.createdAt, issuedAt: row.createdAt, cancelledAt: null,
    latestConfirmationId: null, latestConfirmationStatus: null, approval: null,
    lines: [{ id: `line-${id}`, sourceQuotationLineId: 'quotation-line-ui', purchaseRequestLineId: 'pr-line-ui', productSku: 'SKU-UI', productName: 'Office chair', unitOfMeasureCode: 'EA', orderedQuantity: 2, confirmedQuantity: 0, remainingQuantity: 2, unitPrice: row.total / 2, discountAmount: null, discountPercentage: null, taxCode: null, taxName: null, taxRatePercentage: null, taxAmount: null, requestedNeedByDate: '2026-09-01', deliveryDate: '2026-09-05', notes: null, version: 'LINE-V1' }],
    pendingChanges: [], canEdit: false, canSubmit: false, canApprove: false, canReject: false,
    canReturnForChange: false, canIssue: false, canCancel: row.status === 'Issued', canCaptureConfirmation: false,
    canApproveSupplierChange: false, canRejectSupplierChange: false,
  };
};

const entryResponse = (defaultTheme: string | null = null, symbolAssetUrl: string | null = null) => ({
  entryMode: 'TenantHost',
  canonicalHost: '127.0.0.1',
  candidateTenantId: tenantId,
  candidateTenantDisplayName: 'Alpha Tenant',
  authorizedTenants: [{ tenantId, displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
  operationalContexts,
  selectedOperationalContextId: 'operation-ui',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true, defaultTheme },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl, symbolTextFallback: 'SAR' },
  code: null,
});

async function installMocks(page: Page): Promise<void> {
  await page.addInitScript(() => {
    if (!sessionStorage.getItem('mesp.ui-test-clean')) {
      localStorage.clear();
      sessionStorage.setItem('mesp.ui-test-clean', '1');
    }
  });
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: { authenticated: true, actorId: 'actor-ui', sessionId: 'session-ui', lifecycleState: 'Active', absoluteExpiresAt: null, selectedPath: 'OrdinaryMembership', selectedTenantId: tenantId, selectedContextId: 'context-ui', selectionVersion: 1 } }));
  await page.route('**/api/v1/auth/contexts', (route) => route.fulfill({ json: { contexts: [{ contextId: 'context-ui', kind: 'OrdinaryMembership', tenantId, displayName: 'Alpha Company', eligibilityVersion: 1 }] } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: entryResponse() }));
  await page.route('**/api/v1/auth/antiforgery', (route) => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'test-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/procurement/**', async (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/purchase-orders')) return route.fulfill({ json: orderRows });
    if (path.endsWith('/confirmations') || path.endsWith('/history') || path.endsWith('/audit')) return route.fulfill({ json: [] });
    const match = path.match(/\/purchase-orders\/(po-\d+)$/);
    if (match) return route.fulfill({ json: orderDetail(match[1]) });
    return route.fulfill({ json: [] });
  });
}

test.describe('MESP-153 Slice A UI', () => {
  test.beforeEach(async ({ page }) => installMocks(page));

  test('keeps English and Sapphire defaults, keyboard themes, independent dark mode, and persisted choices', async ({ page }) => {
    await page.goto('/app');
    await expect(page.locator('#tenant-overview-title')).toHaveText('Alpha Tenant');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'sapphire');
    await expect(page.locator('.module-card').first().locator('.module-card__tag')).toHaveText('Master data');
    await expect(page.locator('.module-card').first().locator('strong')).toHaveText('Master Data');

    const sidebar = page.locator('#app-sidebar');
    const overviewTile = sidebar.getByRole('button', { name: 'Overview' });
    await expect(overviewTile).toHaveClass(/is-active/);
    await expect(overviewTile).toHaveAttribute('aria-current', 'page');
    await page.getByRole('button', { name: 'Expand navigation' }).click();
    await expect(sidebar).toHaveClass(/sidebar--expanded/);
    await expect(sidebar.locator('.nav-group__title').first()).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Purchase Orders' })).toBeVisible();
    await page.getByRole('button', { name: 'Collapse navigation' }).click();
    await expect(sidebar).not.toHaveClass(/sidebar--expanded/);

    const overviewSearch = page.getByRole('searchbox', { name: 'Search modules and destinations' });
    await overviewSearch.fill('Purchase Orders');
    await expect(page.locator('.module-card')).toHaveCount(1);
    await expect(page.locator('.module-card strong')).toHaveText('Purchase Orders');
    await overviewSearch.fill('');

    const trigger = page.getByRole('button', { name: 'Themes' });
    await expect(trigger).not.toHaveAttribute('aria-controls');
    await trigger.click();
    const menu = page.getByRole('menu', { name: 'Choose a theme' });
    await expect(menu.getByRole('menuitemradio')).toHaveCount(9);
    await expect(trigger).toHaveAttribute('aria-controls', 'theme-menu');
    await expect(menu.getByRole('menuitemradio', { name: 'Sapphire' })).toHaveAttribute('aria-checked', 'true');
    await page.keyboard.press('End');
    await expect(menu.getByRole('menuitemradio', { name: 'Noir' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(trigger).toBeFocused();

    await trigger.click();
    await menu.getByRole('menuitemradio', { name: 'Teal' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'teal');
    await page.getByRole('button', { name: 'Switch to dark mode' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-color-scheme', 'dark');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'teal');
    await expect(page.locator('.topbar__brand')).not.toHaveClass(/light-backplate/);
    await expect(page.locator('.topbar__mesp-logo img')).toHaveAttribute('src', /Logo_16_9_BG_Removed_Dark\.png/);
    await expect(page.locator('.module-card--procurement .module-card__banner').first()).toHaveCSS('background-color', 'rgb(26, 34, 48)');

    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'teal');
    await expect(page.locator('html')).toHaveAttribute('data-color-scheme', 'dark');
    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });

  test('loads every Overview module card image', async ({ page }) => {
    await page.goto('/app');
    const images = page.locator('.module-card img');
    await expect(images).toHaveCount(15);
    for (const image of await images.all()) {
      await image.scrollIntoViewIfNeeded();
      await expect.poll(() => image.evaluate((element) => (element as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
    }
  });

  test('keeps the Overview link visible and clickable below the header in expanded navigation', async ({ page }) => {
    await page.goto('/app/procurement/purchase-orders');
    const sidebar = page.locator('#app-sidebar');
    await page.getByRole('button', { name: 'Expand navigation' }).click();

    const overviewLink = sidebar.getByRole('link', { name: 'Overview' });
    await expect(overviewLink).toBeVisible();
    const [headerBox, overviewBox] = await Promise.all([
      page.locator('.topbar').boundingBox(),
      overviewLink.boundingBox(),
    ]);
    expect(headerBox).not.toBeNull();
    expect(overviewBox).not.toBeNull();
    expect(overviewBox!.y).toBeGreaterThanOrEqual(headerBox!.y + headerBox!.height);

    await overviewLink.click();
    await expect(page).toHaveURL(/\/app$/);
    await expect(page.locator('#tenant-overview-title')).toBeVisible();
  });

  test('uses the Tenant branding theme when there is no saved user choice', async ({ page }) => {
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: entryResponse('forest') }));
    await page.goto('/app');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'forest');
    await page.getByRole('button', { name: 'Themes' }).click();
    await expect(page.getByRole('menu', { name: 'Choose a theme' }).getByRole('menuitemradio', { name: 'Forest' })).toHaveAttribute('aria-checked', 'true');
  });

  test('renders the SAR asset accessibly in grid and detail views across RTL and dark mode', async ({ page }) => {
    await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: entryResponse('forest', '/assets/Saudi_Riyal.svg') }));
    await page.goto('/app/procurement/purchase-orders');

    const assertMaskedSymbol = async (symbol: Locator): Promise<void> => {
      await expect(symbol).toHaveAccessibleName('SAR');
      await expect(symbol).toBeVisible();
      const style = await symbol.evaluate((element) => {
        const computed = getComputedStyle(element);
        return { mask: computed.maskImage, background: computed.backgroundColor, color: computed.color };
      });
      expect(style.mask).toContain('Saudi_Riyal.svg');
      expect(style.background).toBe(style.color);
    };

    const sarGridSymbol = page.locator('tr[data-row-id=\"po-1\"] .data-grid-money [role=\"img\"]');
    await assertMaskedSymbol(sarGridSymbol);
    const usdGridCell = page.locator('tr[data-row-id=\"po-2\"] .data-grid-money');
    await expect(usdGridCell).toContainText('USD');
    await expect(usdGridCell.getByRole('img')).toHaveCount(0);

    await page.getByRole('button', { name: 'Switch to dark mode' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-color-scheme', 'dark');
    await assertMaskedSymbol(sarGridSymbol);
    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await assertMaskedSymbol(sarGridSymbol);

    await page.getByRole('link', { name: 'QT-001' }).click();
    await expect(page).toHaveURL(/purchase-orders\/po-1$/);
    await page.getByRole('tab').nth(1).click();
    const sarDetailSymbol = page.locator('app-data-grid [role=\"img\"]');
    await assertMaskedSymbol(sarDetailSymbol);
    await page.locator('.scheme-toggle').click();
    await expect(page.locator('html')).toHaveAttribute('data-color-scheme', 'light');
    await assertMaskedSymbol(sarDetailSymbol);
  });

  test('keeps header controls reachable without horizontal overflow at 360px with several contexts', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 800 });
    await page.goto('/app');

    const contextSelect = page.locator('.operational-switcher__select');
    await expect(contextSelect).toBeVisible();
    await expect(contextSelect.locator('option')).toHaveCount(3);
    const controls = [
      contextSelect,
      page.getByRole('button', { name: 'Themes' }),
      page.getByRole('button', { name: 'Switch to dark mode' }),
      page.locator('.language-button'),
      page.locator('.user-pill .sign-out'),
    ];
    for (const control of controls) await expect(control).toBeVisible();

    const bounds = await page.evaluate(() => ({
      documentWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth,
      controls: [...document.querySelectorAll<HTMLElement>('.operational-switcher__select, #theme-trigger, .scheme-toggle, .language-button, .user-pill .sign-out')]
        .map((element) => { const rect = element.getBoundingClientRect(); return { left: rect.left, right: rect.right }; }),
    }));
    expect(bounds.documentWidth).toBeLessThanOrEqual(bounds.viewportWidth);
    expect(bounds.controls).toHaveLength(5);
    for (const control of bounds.controls) {
      expect(control.left).toBeGreaterThanOrEqual(0);
      expect(control.right).toBeLessThanOrEqual(bounds.viewportWidth);
    }

    await page.getByRole('button', { name: 'Themes' }).click();
    await expect(page.getByRole('menu', { name: 'Choose a theme' })).toBeVisible();
    await page.keyboard.press('Escape');
  });

  test('keeps the full-height sticky rail in LTR and RTL while scrolling', async ({ page }) => {
    await page.setViewportSize({ width: 1600, height: 600 });
    await page.goto('/app/procurement/purchase-orders');
    await page.locator('#main-content').evaluate((element) => { (element as HTMLElement).style.minHeight = '1400px'; });
    const sidebar = page.locator('#app-sidebar');
    const measure = () => sidebar.evaluate((element) => {
      const rect = element.getBoundingClientRect();
      return { top: rect.top, bottom: rect.bottom, height: rect.height, position: getComputedStyle(element).position };
    });
    const scrollRange = await page.evaluate(() => document.documentElement.scrollHeight - window.innerHeight);
    expect(scrollRange).toBeGreaterThan(0);
    await page.evaluate(() => window.scrollTo(0, 100));
    await expect.poll(async () => measure()).toEqual({ top: 0, bottom: 600, height: 600, position: 'sticky' });

    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await page.evaluate(() => window.scrollTo(0, 200));
    await expect.poll(async () => measure()).toEqual({ top: 0, bottom: 600, height: 600, position: 'sticky' });
  });

  test('keeps the Arabic hero orbit inside its card', async ({ page }) => {
    await page.setViewportSize({ width: 1600, height: 1000 });
    await page.goto('/app');
    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    const bounds = await page.locator('.overview-hero').evaluate((hero) => {
      const card = hero.getBoundingClientRect();
      const orbit = [...hero.querySelectorAll<HTMLElement>('.hero-orbit')].map((element) => {
        const rect = element.getBoundingClientRect();
        return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom };
      });
      return { card: { left: card.left, right: card.right, top: card.top, bottom: card.bottom }, orbit };
    });
    for (const orbit of bounds.orbit) {
      expect(orbit.left).toBeGreaterThanOrEqual(bounds.card.left);
      expect(orbit.right).toBeLessThanOrEqual(bounds.card.right);
      expect(orbit.top).toBeGreaterThanOrEqual(bounds.card.top);
      expect(orbit.bottom).toBeLessThanOrEqual(bounds.card.bottom);
    }
  });

  test('aligns numeric grid headers in both directions and reduces glass motion', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/app');
    await expect(page.locator('.module-card').first()).toHaveCSS('animation-name', 'none');
    await page.goto('/app/procurement/purchase-orders');

    const totalHeader = page.locator('app-data-grid thead th.numeric');
    await expect(totalHeader).toHaveCSS('text-align', 'end');
    await expect(totalHeader.locator('.grid-heading')).toHaveCSS('justify-content', 'flex-end');
    await expect(page.locator('.filter-field select')).toHaveCSS('appearance', 'none');
    await expect(page.locator('.filter-select__chevron')).toBeVisible();

    await page.locator('.language-button').click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(totalHeader).toHaveCSS('text-align', 'end');
    await expect(page.locator('app-data-grid tbody tr[data-row-id="po-3"] td').nth(1)).toContainText(`${new Intl.NumberFormat('ar').format(1)} بند`);
  });

  test('sorts, filters, resizes, selects, pages, opens row actions, and fits the grid on mobile', async ({ page }) => {
    await page.goto('/app/procurement/purchase-orders');
    const grid = page.locator('app-data-grid');
    await expect(page.locator('.filter-search input')).toHaveCount(1);
    await expect(page.locator('.filter-search input')).toHaveAttribute('aria-label', 'Search supplier or quotation reference');
    await expect(page.locator('.filter-search input')).toHaveCSS('height', '44px');
    await expect(page.locator('.filter-search input')).toHaveCSS('border-top-width', '1px');
    await expect(grid.locator('.pager-summary')).toHaveText('1–7 / 8');
    await expect(grid.locator('.data-grid-pager')).toBeVisible();
    await expect(grid.locator('.data-grid-badge--issued').first()).toHaveText('Issued');
    await expect(grid.locator('.data-grid-money').first()).toContainText('SAR');
    await expect(grid.locator('tbody tr[data-row-id="po-3"] td').nth(1)).toContainText('1 line');
    await expect(grid.locator('tbody tr')).toHaveCount(7);
    await expect(grid.locator('[aria-sort="ascending"]')).toHaveCount(0);
    await expect(grid.locator('tbody tr').first().getByRole('link', { name: 'QT-001' })).toHaveAttribute('href', '/app/procurement/purchase-orders/po-1');
    await page.getByRole('button', { name: /Sort by Supplier/ }).click();
    await expect(grid.locator('thead th').nth(2)).toHaveAttribute('aria-sort', 'ascending');
    await expect(grid.locator('tbody tr').first()).toHaveAttribute('data-row-id', 'po-1');

    const supplierFilter = page.getByRole('button', { name: 'Filter Supplier' });
    await supplierFilter.click();
    const filterDialog = page.getByRole('dialog', { name: 'Filter Supplier' });
    await expect(supplierFilter).toHaveAttribute('aria-controls', 'grid-filter-supplierName');
    await filterDialog.getByRole('searchbox').fill('Cedar');
    await expect(grid.locator('tbody tr')).toHaveCount(1);
    await expect(filterDialog.getByRole('searchbox')).toHaveValue('Cedar');
    await expect(page.locator('.filter-toolbar')).toBeVisible();
    await expect(supplierFilter).toHaveClass(/is-filtered/);
    await expect(grid.locator('tbody tr').first()).toHaveAttribute('data-row-id', 'po-3');
    const filteredLayout = await page.evaluate(() => {
      const toolbar = document.querySelector('.filter-toolbar')!.getBoundingClientRect();
      const card = document.querySelector('app-data-grid .data-grid-card')!.getBoundingClientRect();
      const pager = document.querySelector('app-data-grid .data-grid-pager')!.getBoundingClientRect();
      const popover = document.querySelector('app-data-grid .grid-filter-popover')!.getBoundingClientRect();
      return {
        toolbarTop: toolbar.top,
        cardHeight: card.height,
        pagerGap: card.bottom - pager.bottom,
        popoverInViewport: popover.left >= 0 && popover.right <= window.innerWidth && popover.top >= 0 && popover.bottom <= window.innerHeight,
        tableScrollTop: document.querySelector('app-data-grid .data-grid-scroll')!.scrollTop,
      };
    });
    expect(filteredLayout.toolbarTop).toBeLessThan(500);
    expect(filteredLayout.cardHeight).toBeLessThan(350);
    expect(filteredLayout.pagerGap).toBeLessThanOrEqual(2);
    expect(filteredLayout.popoverInViewport).toBe(true);
    expect(filteredLayout.tableScrollTop).toBe(0);
    await filterDialog.getByRole('button', { name: 'Clear filter' }).click();

    const resize = page.getByRole('separator', { name: 'Resize Supplier' });
    const initialWidth = Number(await resize.getAttribute('aria-valuenow'));
    await resize.press('ArrowRight');
    await expect(resize).toHaveAttribute('aria-valuenow', String(initialWidth + 8));
    await grid.locator('tbody tr').first().getByRole('checkbox').check();
    await expect(grid.locator('tbody tr').first()).toHaveClass(/is-selected/);
    await page.getByRole('button', { name: 'Next page' }).click();
    await expect(grid.locator('tbody tr')).toHaveCount(1);

    const rowActions = grid.locator('tbody tr').last().getByRole('button', { name: /Purchase Order actions/ });
    await expect(rowActions).toHaveAccessibleName('Purchase Order actions: QT-008');
    await rowActions.click();
    await expect(page.getByRole('menu', { name: 'Purchase Order actions' })).toBeVisible();
    await expect(page.getByRole('menuitem', { name: 'View Purchase Order' })).toBeFocused();

    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(0);
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await expect(page.locator('#app-sidebar')).toHaveClass(/sidebar--mobile-open/);
    await page.locator('.mobile-toggle').click();
  });

  test('keeps the modal keyboard accessible on a server-loaded Purchase Order', async ({ page }) => {
    await page.goto('/app/procurement/purchase-orders/po-1');
    await expect(page.getByRole('heading', { name: 'QT-001' })).toBeVisible();
    await page.getByRole('button', { name: 'Cancel Purchase Order' }).click();
    const dialog = page.getByRole('dialog', { name: 'Cancel Purchase Order' });
    await expect(dialog).toBeVisible();
    await expect.poll(() => dialog.evaluate((element) => getComputedStyle(element, '::before').backgroundImage)).toContain('linear-gradient');
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
  });
});
