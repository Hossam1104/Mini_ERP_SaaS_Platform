import { expect, test, type Page } from '@playwright/test';

async function waitForEntrance(page: Page): Promise<void> {
  await page.locator('.auth-surface').evaluate(async (surface) => {
    const entrance = surface.getAnimations({ subtree: true })
      .filter((animation) => animation.effect?.getTiming().iterations !== Infinity);
    await Promise.all(entrance.map((animation) => animation.finished.catch(() => undefined)));
  });
}

const entry = {
  entryMode: 'TenantHost',
  canonicalHost: '127.0.0.1',
  candidateTenantId: 'tenant-ui',
  candidateTenantDisplayName: 'Wafra ERP',
  authorizedTenants: [],
  operationalContexts: [],
  selectedOperationalContextId: null,
  operationalSelectionVersion: 0,
  branding: {
    displayName: 'Wafra ERP',
    arabicDisplayName: 'وفرة',
    logoLightUrl: '/assets/wafra-logo.png',
    logoDarkUrl: '/assets/wafra-logo-dark.png',
    logoAltText: 'Wafra',
    tenantConfigured: true,
    defaultTheme: 'meadow',
  },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
  isDevelopment: true,
  developmentAccountHint: null,
};

test('MESP-205 captures Wafra sign-in across language, scheme, and viewport', async ({ page }) => {
  const phase = process.env['MESP_205_LOGIN_CAPTURE'] ?? 'after';
  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: entry }));
  await page.route('**/api/v1/module-registration', (route) => route.fulfill({ json: {
    module: 'platform-administration',
    name: 'Platform Administration',
    boundary: 'platform',
    registered: true,
    masterData: { module: 'master-data-catalog', name: 'Master Data Catalog', boundary: 'master-data', registered: true },
    businessParties: { module: 'business-parties', name: 'Business Parties', boundary: 'business-parties', registered: true },
  } }));

  await page.goto('/login');
  const screenshotDirectory = '../.playwright-mcp/mesp-205';
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: width === 390 ? 844 : 900 });
    for (const language of ['en', 'ar'] as const) {
      for (const scheme of ['light', 'dark'] as const) {
        await page.evaluate(({ language, scheme }) => {
          localStorage.setItem('mesp.ui.language', language);
          localStorage.setItem('mesp.ui.theme', 'meadow');
          localStorage.setItem('mesp.ui.dark', String(scheme === 'dark'));
        }, { language, scheme });
        await page.reload();
        await expect(page.locator('.auth-brand')).toBeVisible();
        await expect(page.locator('.brand-logo')).toBeVisible();
        await expect(page.locator('html')).toHaveAttribute('lang', language);
        await expect(page.locator('html')).toHaveAttribute('dir', language === 'ar' ? 'rtl' : 'ltr');
        await expect(page.locator('html')).toHaveAttribute('data-color-scheme', scheme);
        await page.locator('.brand-logo').evaluate((element) => (element as HTMLImageElement).decode());
        const logoMetrics = await page.locator('.brand-logo').evaluate((element) => {
          const image = element as HTMLImageElement;
          const mark = image.parentElement as HTMLElement;
          const imageStyle = getComputedStyle(image);
          const markStyle = getComputedStyle(mark);
          const bounds = image.getBoundingClientRect();
          return {
            width: bounds.width,
            height: bounds.height,
            ratio: bounds.width / bounds.height,
            naturalRatio: image.naturalWidth / image.naturalHeight,
            objectFit: imageStyle.objectFit,
            background: markStyle.backgroundColor,
            boxShadow: markStyle.boxShadow,
            borderRadius: markStyle.borderRadius,
          };
        });
        expect(logoMetrics.objectFit).toBe('contain');
        expect(logoMetrics.ratio).toBeCloseTo(logoMetrics.naturalRatio, 2);
        expect(logoMetrics.background).toBe('rgba(0, 0, 0, 0)');
        expect(logoMetrics.boxShadow).toBe('none');
        expect(logoMetrics.borderRadius).toBe('0px');
        if (width === 1440) expect(logoMetrics.width).toBeGreaterThanOrEqual(220);
        else expect(logoMetrics.width).toBeLessThan(384);

        const suffix = `${phase}-login-${scheme}-${language}-${width}`;
        await page.screenshot({
          path: `${screenshotDirectory}/${suffix}.png`,
          fullPage: true,
          animations: 'disabled',
        });
      }
    }
  }
});

test('MESP-205 records password and submit motion states with reduced motion support', async ({ page }) => {
  const authenticatedSession = {
    authenticated: true,
    actorId: 'actor-ui',
    sessionId: 'session-ui',
    lifecycleState: 'Active',
    absoluteExpiresAt: null,
    selectedPath: 'OrdinaryMembership',
    selectedTenantId: 'tenant-ui',
    selectedContextId: 'operation-ui',
    selectionVersion: 1,
    displayName: 'Amina Hassan',
    login: 'owner@example.com',
  };
  let sessionCalls = 0;
  let signInMode: 'success' | 'error' = 'success';
  let holdNextSignIn = true;
  let holdNextRefresh = false;
  let authenticatedFlowStarted = false;
  let refreshHeld = false;
  let releaseSignIn: (() => void) | null = null;
  let releaseRefresh: (() => void) | null = null;

  await page.route('**/api/v1/auth/development-bypass', (route) => route.fulfill({ json: { authenticated: false } }));
  await page.route('**/api/v1/auth/session', async (route) => {
    if (!authenticatedFlowStarted) return route.fulfill({ json: { authenticated: false } });
    sessionCalls += 1;
    if (holdNextRefresh) {
      holdNextRefresh = false;
      refreshHeld = true;
      await new Promise<void>((resolve) => {
        releaseRefresh = () => { void route.fulfill({ json: authenticatedSession }).then(resolve); };
      });
      return;
    }
    await route.fulfill({ json: authenticatedSession });
  });
  await page.route('**/api/v1/auth/entry', (route) => route.fulfill({ json: entry }));
  await page.route('**/api/v1/module-registration', (route) => route.fulfill({ json: {
    module: 'platform-administration',
    name: 'Platform Administration',
    boundary: 'platform',
    registered: true,
    masterData: { module: 'master-data-catalog', name: 'Master Data Catalog', boundary: 'master-data', registered: true },
    businessParties: { module: 'business-parties', name: 'Business Parties', boundary: 'business-parties', registered: true },
  } }));
  await page.route('**/api/v1/auth/sign-in', async (route) => {
    if (holdNextSignIn) {
      holdNextSignIn = false;
      await new Promise<void>((resolve) => { releaseSignIn = resolve; });
    }
    if (signInMode === 'error') {
      return route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ code: 'authentication_failed' }) });
    }
    return route.fulfill({ json: authenticatedSession });
  });

  await page.goto('/login');
  await expect(page.locator('.auth-brand')).toBeVisible();
  await waitForEntrance(page);
  await page.getByLabel('Email or username').fill('owner@example.com');
  await page.getByLabel('Password', { exact: true }).fill('example-password');
  const password = page.locator('#password');
  const passwordToggle = page.locator('.password-toggle');
  const screenshotDirectory = '../.playwright-mcp/mesp-205';
  const capture = (name: string) => page.screenshot({
    path: `${screenshotDirectory}/${name}.png`,
    animations: 'allow',
  });

  await expect(passwordToggle).toHaveAttribute('aria-label', 'Show password');
  await expect(passwordToggle).toHaveAttribute('aria-pressed', 'false');
  await capture('login-sequence-01-password-hidden');
  await passwordToggle.click();
  await expect(password).toHaveAttribute('type', 'text');
  await expect(passwordToggle).toHaveAttribute('aria-label', 'Hide password');
  await expect(passwordToggle).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('button', { name: 'Hide password' })).toBeVisible();
  await expect.poll(() => page.locator('.password-toggle__icon--closed').evaluate((element) => getComputedStyle(element).animationName)).toContain('auth-icon-pop');
  await expect.poll(() => password.evaluate((element) => getComputedStyle(element).animationName)).toContain('auth-password-reveal');
  await Promise.all([
    password.evaluate((element) => Promise.all((element as HTMLElement).getAnimations().map((animation) => animation.finished))),
    page.locator('.password-toggle__icon--closed').evaluate((element) => Promise.all(element.getAnimations().map((animation) => animation.finished))),
  ]);
  await capture('login-sequence-02-password-visible');

  const submit = page.locator('.sign-in-form .primary-button');
  await page.keyboard.press('Tab');
  await expect(submit).toBeFocused();
  await expect(submit).toHaveCSS('outline-style', 'solid');
  await capture('login-sequence-03-button-focus');
  await submit.hover();
  await expect.poll(() => submit.evaluate((element) => getComputedStyle(element, '::before').animationName)).toContain('auth-button-shine');
  await capture('login-sequence-04-button-hover');
  const hoverTransform = await submit.evaluate((element) => getComputedStyle(element).transform);
  await submit.hover();
  await page.mouse.down();
  await expect.poll(() => submit.evaluate((element) => getComputedStyle(element).transform)).not.toBe(hoverTransform);
  await capture('login-sequence-05-button-press');
  await page.mouse.up();

  await submit.evaluate((element) => (element as HTMLButtonElement).click());
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveAttribute('aria-busy', 'true');
  await expect(submit.locator('.button-spinner')).toBeVisible();
  await expect.poll(() => submit.evaluate((element) => getComputedStyle(element, '::after').animationName)).toContain('auth-progress-sweep');
  await capture('login-sequence-06-button-loading');

  authenticatedFlowStarted = true;
  holdNextRefresh = true;
  releaseSignIn?.();
  await expect(submit).toHaveClass(/is-success/);
  await expect(submit.locator('.button-success-check')).toBeVisible();
  await expect.poll(() => refreshHeld).toBe(true);
  await submit.locator('.button-success-check').evaluate((element) => Promise.all(element.getAnimations().map((animation) => animation.finished)));
  await capture('login-sequence-07-button-success');
  releaseRefresh?.();
  await expect(page).toHaveURL(/\/app$/);

  signInMode = 'error';
  authenticatedFlowStarted = false;
  sessionCalls = 0;
  refreshHeld = false;
  await page.goto('/login');
  await expect(page.locator('.auth-brand')).toBeVisible();
  await waitForEntrance(page);
  await page.getByLabel('Email or username').fill('owner@example.com');
  await page.getByLabel('Password', { exact: true }).fill('wrong-password');
  const errorSubmit = page.locator('.sign-in-form .primary-button');
  await errorSubmit.evaluate((element) => (element as HTMLButtonElement).click());
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.locator('.auth-surface')).toHaveClass(/auth-surface--failed/);
  const errorAnimations = await page.evaluate(() => ({
    button: getComputedStyle(document.querySelector('.sign-in-form .primary-button')!).animationName,
    field: getComputedStyle(document.querySelector('#password')!).animationName,
  }));
  expect(errorAnimations.button).toContain('auth-shake');
  expect(errorAnimations.button).toContain('auth-button-error-pulse');
  expect(errorAnimations.field).toContain('auth-field-error-pulse');
  await capture('login-sequence-08-button-error');

  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.reload();
  await expect(page.locator('.auth-brand')).toBeVisible();
  const reducedMotion = await page.evaluate(() => ({
    brandGlow: getComputedStyle(document.querySelector('.auth-brand')!, '::before').animationName,
    surface: getComputedStyle(document.querySelector('.auth-surface')!).animationName,
    fieldTransition: getComputedStyle(document.querySelector('#password')!).transitionDuration,
    buttonTransition: getComputedStyle(document.querySelector('.sign-in-form .primary-button')!).transitionDuration,
  }));
  expect(reducedMotion.brandGlow).toBe('none');
  expect(reducedMotion.surface).toBe('none');
  expect(reducedMotion.fieldTransition).toBe('0s');
  expect(reducedMotion.buttonTransition).toBe('0s');
});
