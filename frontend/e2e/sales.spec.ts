import { expect, test, type Page } from '@playwright/test';

const session = {
  authenticated: true,
  actorId: 'actor-sales-1',
  sessionId: 'session-sales-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-sales-a',
  selectedContextId: 'context-sales-a',
  selectionVersion: 1,
};

async function setupSalesRoutes(page: Page): Promise<void> {
  await page.route('**/api/v1/auth/development-bypass', route => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/antiforgery', route => route.fulfill({ headers: { 'X-CSRF-TOKEN': 'sales-playwright-token' }, json: { status: 'issued' } }));
  await page.route('**/api/v1/auth/session', route => route.fulfill({ json: session }));
  await page.route('**/api/v1/auth/contexts', route => route.fulfill({ json: { contexts: [] } }));
  await page.route('**/api/v1/auth/entry', route => route.fulfill({ json: {
    entryMode: 'Tenant',
    selectedOperationalContextId: 'context-sales-a', operationalSelectionVersion: 1,
    branding: { displayName: 'Sales Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Sales Tenant', tenantConfigured: true },
    currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' }, code: null,
  } }));
  await page.route('**/api/v1/sales/quotations', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/sales/orders', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/customers', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/currencies', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/products', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/units', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/master-data/price-lists', route => route.fulfill({ json: [] }));
  await page.route('**/api/v1/procurement/organization-scopes', route => route.fulfill({ json: [] }));
}

test.describe('MESP-136 Sales workspace', () => {
  test.beforeEach(async ({ page }) => setupSalesRoutes(page));

  test('renders searchable quotation and order registers with empty states', async ({ page }) => {
    await page.goto('/app/sales/quotations');
    await expect(page.locator('h1#sales-title')).toBeVisible();
    await expect(page.getByText('No quotations yet')).toBeVisible();
    await expect(page.locator('[role="search"]')).toBeVisible();

    await page.goto('/app/sales/orders');
    await expect(page.locator('h1#sales-title')).toBeVisible();
    await expect(page.getByText('No Sales Orders yet')).toBeVisible();
  });

  test('keeps quotation scope selection server-configured and omits raw GUID inputs', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/app/sales/quotations/new');
    const scope = page.locator('select[name="organizationScope"]');
    await expect(scope).toBeVisible();
    await expect(page.locator('input[name="companyId"]')).toHaveCount(0);
    await expect(page.locator('input[name="branchId"]')).toHaveCount(0);
    await expect(page.getByText('Prices and totals are server-authoritative', { exact: false })).toBeVisible();

    const rowSelects = page.locator('.form-grid--context select[name="organizationScope"], .form-grid--context select[name="customerId"], .form-grid--context select[name="currencyId"], .form-grid--context select[name="priceListId"]');
    const rowTops = await rowSelects.evaluateAll((elements) => elements.map((element) => (element as HTMLElement).getBoundingClientRect().top));
    expect(rowTops.length).toBeGreaterThanOrEqual(4);
    expect(Math.max(...rowTops) - Math.min(...rowTops)).toBeLessThanOrEqual(1);

    const customerSelect = page.locator('select[name="customerId"]');
    const valueImage = (await customerSelect.screenshot()).toString('base64');
    const valueBox = await page.evaluate(async (base64) => {
      const select = document.querySelector<HTMLSelectElement>('select[name="customerId"]')!;
      const style = getComputedStyle(select);
      const image = new Image();
      image.src = `data:image/png;base64,${base64}`;
      await image.decode();
      const canvas = document.createElement('canvas');
      canvas.width = image.naturalWidth;
      canvas.height = image.naturalHeight;
      const context = canvas.getContext('2d')!;
      context.drawImage(image, 0, 0);
      const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
      const rgb = (color: string) => color.match(/\d+/g)!.slice(0, 3).map(Number);
      const background = rgb(style.backgroundColor);
      const foreground = rgb(style.color);
      const textRows: number[] = [];
      for (let y = 3; y < canvas.height - 3; y++) {
        for (let x = 4; x < canvas.width - 30; x++) {
          const offset = (y * canvas.width + x) * 4;
          const pixel = [pixels[offset], pixels[offset + 1], pixels[offset + 2]];
          const distance = (color: number[]) => Math.hypot(pixel[0] - color[0], pixel[1] - color[1], pixel[2] - color[2]);
          if (distance(background) > 45 && distance(foreground) < distance(background)) { textRows.push(y); break; }
        }
      }
      const top = Math.min(...textRows);
      const bottom = Math.max(...textRows);
      return {
        alignment: style.alignItems,
        display: style.display,
        textCenterOffset: Math.abs((top + bottom + 1) / 2 - canvas.height / 2),
        textHeight: textRows.length,
      };
    }, valueImage);
    expect(valueBox.display).toBe('flex');
    expect(valueBox.alignment).toBe('center');
    expect(valueBox.textHeight).toBeGreaterThan(0);
    expect(valueBox.textCenterOffset).toBeLessThanOrEqual(2);
  });
});
