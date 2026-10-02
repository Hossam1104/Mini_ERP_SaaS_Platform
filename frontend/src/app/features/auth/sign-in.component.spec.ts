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
  entryMode: 'SignIn',
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
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 1,
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
};

const emergencySession: FoundationSessionResponse = {
  ...authenticatedSession,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  isEmergencySuperAdministrator: true,
};

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
    http.expectNone('/api/v1/auth/emergency-tenants');
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
    http.expectOne('/api/v1/auth/session').flush(authenticatedSession);
    await submit;

    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    initialEntry.flush(commonEntry);
    await tick();
  });

  it('uses MESP before sign-in, then applies account Tenant branding and enters Overview directly', async () => {
    const tenantEntry: FoundationEntryResponse = {
      ...commonEntry,
      entryMode: 'Tenant',
      branding: {
        displayName: 'Alpha ERP',
        logoLightUrl: '/assets/example-tenant-logo.png',
        logoDarkUrl: null,
        logoAltText: 'Alpha ERP',
        tenantConfigured: true,
        defaultTheme: 'brown',
      },
    };
    await initialize();
    expect(component.brandName()).toBe('MESP');
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');

    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    await flushSignInEntry(tenantEntry, authenticatedSession);
    await submit;
    fixture.detectChanges();

    expect(component.step()).toBe('credentials');
    expect(component.brandName()).toBe('Alpha ERP');
    expect(component.brandLogoUrl()).toBe('/assets/example-tenant-logo.png');
    expect(document.documentElement.dataset['theme']).toBe('brown');
    expect(component.auth.session()?.selectedContextId).toBe('context-a');
    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    expect(fixture.nativeElement.querySelector('#tenant-context')).toBeNull();
  });

  it('loads the super-administrator Tenant dropdown only after credentials are verified', async () => {
    await initialize();
    http.expectNone('/api/v1/auth/emergency-tenants');
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');

    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(emergencySession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, entryMode: 'EmergencySuperAdministrator' });
    await tick();
    http.expectOne('/api/v1/auth/session').flush(emergencySession);
    await tick();
    http.expectOne('/api/v1/auth/emergency-tenants').flush({ tenants: [
      { tenantId: 'tenant-a', displayName: 'Alpha ERP', arabicDisplayName: '\u0623\u0644\u0641\u0627' },
      { tenantId: 'tenant-b', displayName: 'Beta ERP', arabicDisplayName: '\u0628\u064a\u062a\u0627' },
    ] });
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

    component.language.toggle();
    fixture.detectChanges();
    expect(select.options[1].textContent).toBe('\u0623\u0644\u0641\u0627');
    expect(select.options[2].textContent).toBe('\u0628\u064a\u062a\u0627');
    component.language.toggle();
  });

  it('enters the chosen Tenant with an antiforgery-protected emergency switch', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(emergencySession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, entryMode: 'EmergencySuperAdministrator' });
    await tick();
    http.expectOne('/api/v1/auth/session').flush(emergencySession);
    await tick();
    http.expectOne('/api/v1/auth/emergency-tenants').flush({ tenants: [
      { tenantId: 'tenant-a', displayName: 'Alpha ERP' },
      { tenantId: 'tenant-b', displayName: 'Beta ERP' },
    ] });
    await submit;
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('#tenant-context') as HTMLSelectElement;
    select.value = 'tenant-b';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    const choose = component.chooseTenant();
    http.expectOne('/api/v1/auth/antiforgery').flush({ status: 'ok' }, {
      headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'csrf-token' }),
    });
    await tick();
    const switchRequest = http.expectOne('/api/v1/auth/emergency-tenant-switch');
    expect(switchRequest.request.body).toEqual({ tenantId: 'tenant-b', expectedSelectionVersion: emergencySession.selectionVersion });
    expect(switchRequest.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    switchRequest.flush({ ...emergencySession, selectedContextId: 'context-b', selectedTenantId: 'tenant-b' });
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({
      ...commonEntry,
      entryMode: 'Tenant',
      branding: { ...commonEntry.branding, displayName: 'Beta ERP', logoLightUrl: '/assets/beta-logo.png', tenantConfigured: true },
    });
    await choose;

    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    expect(component.brandName()).toBe('Beta ERP');
  });

  it('does not list Tenants for an ordinary account and enters its single active membership', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, entryMode: 'Tenant' });
    await tick();
    http.expectOne('/api/v1/auth/session').flush(authenticatedSession);
    await submit;

    expect(component.step()).toBe('credentials');
    expect(router.navigate).toHaveBeenCalledWith(['/app']);
    expect(fixture.nativeElement.querySelector('#tenant-context')).toBeNull();
    http.expectNone('/api/v1/auth/emergency-tenants');
  });
  it('shows a clear empty-context message and keeps sign-out available', async () => {
    await initialize();
    component.form.controls.login.setValue('admin@mesp.com');
    component.form.controls.password.setValue('123');
    const submit = component.submit();
    http.expectOne('/api/v1/auth/sign-in').flush(authenticatedSession);
    await tick();
    http.expectOne('/api/v1/auth/entry').flush({ ...commonEntry, entryMode: 'NoAccess', code: 'access_denied' });
    await tick();
    http.expectOne('/api/v1/auth/session').flush({
      ...authenticatedSession,
      selectedPath: null,
      selectedTenantId: null,
      selectedContextId: null,
    });
    await submit;
    fixture.detectChanges();

    expect(component.step()).toBe('empty');
    expect(fixture.nativeElement.textContent).toContain('This account has no active Tenant membership available here.');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Sign out');
    http.expectNone('/api/v1/auth/emergency-tenants');
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
