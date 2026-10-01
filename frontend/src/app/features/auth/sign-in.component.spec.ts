import { HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { FoundationEntryResponse, FoundationSessionResponse } from '../../core/api/foundation.models';
import { SignInComponent } from './sign-in.component';

const moduleRegistration = {
  module: 'platform-administration',
  name: 'Platform Administration',
  boundary: 'platform',
  registered: true,
  masterData: { module: 'master-data-catalog', name: 'Master Data Catalog', boundary: 'master-data', registered: true },
  businessParties: { module: 'business-parties', name: 'Business Parties', boundary: 'business-parties', registered: true },
};

const commonEntry: FoundationEntryResponse = {
  entryMode: 'CommonHost',
  canonicalHost: 'mesp.localhost',
  candidateTenantId: null,
  candidateTenantDisplayName: null,
  authorizedTenants: [],
  operationalContexts: [],
  selectedOperationalContextId: null,
  operationalSelectionVersion: 0,
  branding: { displayName: 'MESP', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'MESP', tenantConfigured: false, defaultTheme: null },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
  isDevelopment: true,
  developmentAccountHint: 'admin@mesp.com / 123',
};

const authenticatedSession: FoundationSessionResponse = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 1,
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
};

const ordinaryTenantContext = (contextId: string, tenantId: string, displayName: string) => ({
  contextId,
  kind: 'OrdinaryMembership',
  tenantId,
  displayName,
  eligibilityVersion: 1,
});

describe('SignInComponent', () => {
  let fixture: ComponentFixture<SignInComponent>;
  let component: SignInComponent;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.removeItem('mesp.ui.theme');
    localStorage.removeItem('mesp.ui.dark');
    TestBed.configureTestingModule({
      imports: [SignInComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    fixture = TestBed.createComponent(SignInComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  function tick(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  async function initialize(entry: FoundationEntryResponse = commonEntry): Promise<void> {
    fixture.detectChanges();
    http.expectOne('/api/v1/auth/entry').flush(entry);
    http.expectOne('/api/v1/module-registration').flush(moduleRegistration);
    await tick();
    fixture.detectChanges();
  }

  async function flushSignInEntry(
    entry: FoundationEntryResponse,
    session: FoundationSessionResponse = authenticatedSession,
  ): Promise<void> {
    http.expectOne('/api/v1/auth/entry').flush(entry);
    await tick();
    http.expectOne('/api/v1/auth/session').flush(session);
    await tick();
  }

  it('uses a real password-manager form and never fills either credential in code', async () => {
    await initialize();
    const form = fixture.nativeElement.querySelector('.sign-in-form') as HTMLFormElement;
    const username = fixture.nativeElement.querySelector('#username') as HTMLInputElement;
    const password = fixture.nativeElement.querySelector('#password') as HTMLInputElement;
    const submit = form.querySelector('button[type="submit"]') as HTMLButtonElement;

    expect(form.tagName).toBe('FORM');
    expect(username.type).toBe('email');
    expect(username.name).toBe('username');
    expect(username.getAttribute('autocomplete')).toBe('username');
    expect(username.value).toBe('');
    expect(password.name).toBe('password');
    expect(password.getAttribute('autocomplete')).toBe('current-password');
    expect(password.value).toBe('');
    expect(submit.type).toBe('submit');
    expect(fixture.nativeElement.querySelector('.dev-account')?.textContent).toContain('admin@mesp.com / 123');
    http.expectNone('/api/v1/auth/contexts');
  });

  it('routes from the post-sign-in entry while the initial entry request is still pending', async () => {
    fixture.detectChanges();
    const initialEntry = http.expectOne('/api/v1/auth/entry');
    http.expectOne('/api/v1/module-registration').flush(moduleRegistration);
    expect(component.loginEntryMode()).toBe('NoAccess');

    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush(commonEntry);
    await tick();
    http.expectOne('/api/v1/auth/session').flush({ ...authenticatedSession, selectedContextId: 'context-a' });
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({
      contexts: [ordinaryTenantContext('context-a', 'tenant-a', 'Alpha ERP')],
    });
    await submit;

    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    initialEntry.flush({ ...commonEntry, entryMode: 'NoAccess', canonicalHost: null, code: 'access_denied' });
    await tick();
  });

  it('shows the Wafra host branding and auto-selects its one server-authorized context', async () => {
    const tenantEntry: FoundationEntryResponse = {
      ...commonEntry,
      entryMode: 'TenantHost',
      canonicalHost: 'wafra.localhost',
      branding: {
        displayName: 'Wafra ERP',
        logoLightUrl: '/assets/wafra-logo.jpeg',
        logoDarkUrl: null,
        logoAltText: 'Wafra',
        tenantConfigured: true,
        defaultTheme: 'meadow',
      },
    };
    await initialize(tenantEntry);
    expect(component.brandName()).toBe('Wafra ERP');
    expect(component.brandLogoUrl()).toBe('/assets/wafra-logo.jpeg');
    expect(document.documentElement.dataset['theme']).toBe('meadow');
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');

    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    const signedInEntry = { ...tenantEntry, candidateTenantId: 'tenant-a' };
    await flushSignInEntry(signedInEntry, { ...authenticatedSession, selectedContextId: 'context-a', selectedTenantId: 'tenant-a' });
    await submit;
    fixture.detectChanges();

    expect(component.step()).toBe('credentials');
    expect(component.auth.session()?.selectedContextId).toBe('context-a');
    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    expect(fixture.nativeElement.querySelector('#tenant-context')).toBeNull();
  });

  it('loads Tenant choices only after authentication and offers a labelled, keyboard-focusable native select', async () => {
    await initialize();
    http.expectNone('/api/v1/auth/contexts');
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');

    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    const signedInEntry: FoundationEntryResponse = {
      ...commonEntry,
      authorizedTenants: [
        { tenantId: 'tenant-a', displayName: 'Alpha ERP', canonicalHost: 'alpha.localhost' },
        { tenantId: 'tenant-b', displayName: 'Beta ERP', canonicalHost: 'beta.localhost' },
      ],
    };
    http.expectOne('/api/v1/auth/entry').flush(signedInEntry);
    await tick();
    http.expectOne('/api/v1/auth/session').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({
      contexts: [
        ordinaryTenantContext('context-a', 'tenant-a', 'Alpha ERP'),
        ordinaryTenantContext('context-b', 'tenant-b', 'Beta ERP'),
      ],
    });
    await submit;
    fixture.detectChanges();

    expect(component.step()).toBe('chooseTenant');
    expect(router.navigate).not.toHaveBeenCalled();
    const select = fixture.nativeElement.querySelector('#tenant-context') as HTMLSelectElement;
    const label = fixture.nativeElement.querySelector('label[for="tenant-context"]') as HTMLLabelElement;
    expect(select.tagName).toBe('SELECT');
    expect(select.required).toBe(true);
    expect(select.getAttribute('aria-describedby')).toBe('choose-tenant-copy');
    expect(label.textContent).toContain('Available Tenants');
    expect(select.options[1].textContent).toBe('Alpha ERP');
    expect(select.options[2].textContent).toBe('Beta ERP');
    select.focus();
    expect(document.activeElement).toBe(select);
    expect(fixture.nativeElement.querySelector('.tenant-form button[type="submit"]')).toBeTruthy();
  });

  it('switches the selected Tenant through the existing antiforgery-protected context operation', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, authorizedTenants: [
      { tenantId: 'tenant-a', displayName: 'Alpha ERP', canonicalHost: 'alpha.localhost' },
      { tenantId: 'tenant-b', displayName: 'Beta ERP', canonicalHost: 'beta.localhost' },
    ] });
    await tick();
    http.expectOne('/api/v1/auth/session').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({ contexts: [
      ordinaryTenantContext('context-a', 'tenant-a', 'Alpha ERP'),
      ordinaryTenantContext('context-b', 'tenant-b', 'Beta ERP'),
    ] });
    await submit;
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('#tenant-context') as HTMLSelectElement;
    select.value = 'context-b';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    const chooserForm = fixture.nativeElement.querySelector('.tenant-form') as HTMLFormElement;
    const choose = component.chooseTenant();
    http.expectOne('/api/v1/auth/antiforgery').flush({ status: 'ok' }, {
      headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'csrf-token' }),
    });
    await tick();
    const switchRequest = http.expectOne('/api/v1/auth/context-switch');
    expect(switchRequest.request.body.contextId).toBe('context-b');
    expect(switchRequest.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    switchRequest.flush({ ...authenticatedSession, selectedContextId: 'context-b', selectedTenantId: 'tenant-b' });
    await choose;

    expect(chooserForm.tagName).toBe('FORM');
    expect(router.navigate).toHaveBeenCalledWith(['/app']);
  });

  it('skips the chooser and reaches Overview when the server auto-selects one context', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, candidateTenantId: 'tenant-a' });
    await tick();
    http.expectOne('/api/v1/auth/session').flush({ ...authenticatedSession, selectedContextId: 'context-a' });
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({ contexts: [ordinaryTenantContext('context-a', 'tenant-a', 'Alpha ERP')] });
    await submit;

    expect(component.step()).toBe('credentials');
    expect(router.navigate).toHaveBeenCalledWith(['/app']);
  });

  it('shows a clear empty-context message and keeps sign-out available', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush(commonEntry);
    await tick();
    http.expectOne('/api/v1/auth/session').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/contexts').flush({ contexts: [] });
    await submit;
    fixture.detectChanges();

    expect(component.step()).toBe('empty');
    expect(fixture.nativeElement.textContent).toContain('This account has no active Tenant membership available here.');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Sign out');
  });

  it('shows safe sign-in errors and its credential hint only when the server identifies Development', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('wrong-password');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(
      { code: 'authentication_failed' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await submit;
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('We could not sign you in. Check your details and try again.');
    expect(component.showDevPasswordHint()).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.dev-account')?.textContent).toContain('admin@mesp.com / 123');
  });

  it('does not render the dummy account hint when the server environment is not Development', async () => {
    await initialize({ ...commonEntry, isDevelopment: false, developmentAccountHint: null });

    expect(component.developmentAccountHint()).toBeNull();
    expect(fixture.nativeElement.querySelector('.dev-account')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('admin@mesp.com');
    expect(fixture.nativeElement.textContent).not.toContain('123');
  });

  it('announces an expired session politely without showing the Development password hint', async () => {
    await initialize();
    component.auth.markSessionExpired();
    fixture.detectChanges();

    const message = fixture.nativeElement.querySelector('.auth-error') as HTMLElement;
    expect(message.getAttribute('role')).toBe('status');
    expect(message.getAttribute('aria-live')).toBe('polite');
    expect(fixture.nativeElement.querySelector('.dev-error-hint')).toBeNull();
  });

  it('clears an authentication error when the user changes a credential', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('wrong-password');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(
      { code: 'authentication_failed' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await submit;
    expect(component.errorMessage()).not.toBe('');

    component.form.controls.password.setValue('trying-again');
    expect(component.errorMessage()).toBe('');
  });

  it('toggles password visibility and preserves the current-password autocomplete value', async () => {
    await initialize();
    expect(component.passwordVisible()).toBe(false);
    const button = fixture.nativeElement.querySelector('.password-toggle') as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    expect(component.passwordVisible()).toBe(true);
    expect((fixture.nativeElement.querySelector('#password') as HTMLInputElement).type).toBe('text');
    expect((fixture.nativeElement.querySelector('#password') as HTMLInputElement).autocomplete).toBe('current-password');
  });

  it('switches language and direction while keeping the logo unmirrored in Arabic', async () => {
    await initialize();
    component.language.toggle();
    fixture.detectChanges();

    expect(document.documentElement.dir).toBe('rtl');
    expect(component.language.text('signIn')).toBe('تسجيل الدخول');
    const logoImg = fixture.nativeElement.querySelector('img.brand-logo') as HTMLImageElement;
    expect(logoImg.getAttribute('src')).toBe('assets/Logo_4_3_BG_Removed.png');
    expect(window.getComputedStyle(logoImg).transform).not.toContain('matrix(-1');
  });
});
