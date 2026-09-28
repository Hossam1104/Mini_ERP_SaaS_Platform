import { HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { FoundationContextCandidate, FoundationSessionResponse } from '../../core/api/foundation.models';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ApplicationShellComponent } from './application-shell.component';

@Component({ standalone: true, template: '' })
class NavigationTestRouteComponent {}

const authenticatedSession: FoundationSessionResponse = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 2,
};

const contextCandidate: FoundationContextCandidate = {
  contextId: 'context-a',
  kind: 'OrdinaryMembership',
  tenantId: 'tenant-a',
  displayName: 'Alpha workspace',
  eligibilityVersion: 3,
};

async function flushAsyncWork(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe('ApplicationShellComponent sign-out behavior', () => {
  let fixture: ComponentFixture<ApplicationShellComponent>;
  let auth: AuthService;
  let context: ContextService;
  let language: LanguageService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApplicationShellComponent],
      providers: [
        AuthService,
        ContextService,
        LanguageService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    auth = TestBed.inject(AuthService);
    context = TestBed.inject(ContextService);
    language = TestBed.inject(LanguageService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    auth.acceptServerSession(authenticatedSession);
    context.contexts.set([contextCandidate]);
    fixture = TestBed.createComponent(ApplicationShellComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('renders one canonical workspace route and keeps the context selector out of the shell rail', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('a[href="/app/workspaces"]')).not.toBeNull();
    expect(element.querySelector('.context-rail')).toBeNull();
    expect(element.textContent).toContain('Master Data');
    expect(element.textContent).toContain('Price Lists');
    expect(element.textContent).toContain('Purchase Requests');
  });

  it('uses the longest route prefix for the breadcrumb and a single current navigation item', async () => {
    router.resetConfig([{ path: 'app/inventory/valuation', component: NavigationTestRouteComponent }]);
    await router.navigateByUrl('/app/inventory/valuation');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const currentLinks = element.querySelectorAll('.sidebar .nav-link.is-active[aria-current="page"]');
    expect(fixture.componentInstance.currentPage()).toBe('Inventory Valuation');
    expect(element.querySelector('.breadcrumbs [aria-current="page"]')?.textContent).toBe('Inventory Valuation');
    expect(currentLinks).toHaveLength(1);
    expect(currentLinks[0].textContent).toContain('Inventory Valuation');
  });

  async function failSignOut(code = 'audit_unavailable', status = 503): Promise<void> {
    const signOut = fixture.componentInstance.signOut();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.sign-out')?.hasAttribute('disabled')).toBe(true);

    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushAsyncWork();
    http.expectOne('/api/v1/auth/sign-out').flush({ code }, { status, statusText: 'Unavailable' });
    await signOut;
    fixture.detectChanges();
  }

  it('announces a safe failure, keeps the authenticated shell and leaves retry available', async () => {
    await failSignOut();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    const signOutButton = (fixture.nativeElement as HTMLElement).querySelector('.sign-out') as HTMLButtonElement | null;
    expect(alert?.textContent).toContain('Sign-out could not be confirmed. Your session may still be active. Please try again.');
    expect(alert?.getAttribute('aria-live')).toBe('assertive');
    expect(signOutButton?.disabled).toBe(false);
    expect(signOutButton?.getAttribute('aria-describedby')).toBe('sign-out-feedback');
    expect(fixture.nativeElement.textContent).toContain('Alpha workspace');
    expect(auth.status()).toBe('authenticated');
    expect(auth.session()).toEqual(authenticatedSession);
    expect(router.navigate).not.toHaveBeenCalledWith(['/login']);
  });

  it('disables the action only while the sign-out request is active', async () => {
    const signOut = fixture.componentInstance.signOut();
    fixture.detectChanges();
    const button = (fixture.nativeElement as HTMLElement).querySelector('.sign-out') as HTMLButtonElement | null;
    expect(button?.disabled).toBe(true);
    expect(button?.textContent).toContain('Signing out');

    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushAsyncWork();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'request_failed' }, { status: 503, statusText: 'Unavailable' });
    await signOut;
    fixture.detectChanges();

    expect(button?.disabled).toBe(false);
    expect(button?.textContent).toContain('Sign out');
  });

  it('returns to the login boundary when a retry is confirmed successful', async () => {
    await failSignOut('request_failed');

    const retry = fixture.componentInstance.signOut();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.sign-out')?.hasAttribute('disabled')).toBe(true);
    await flushAsyncWork();
    http.expectOne('/api/v1/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });
    await retry;
    fixture.detectChanges();

    expect(auth.session()).toBeNull();
    expect(auth.status()).toBe('anonymous');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('renders the Arabic failure message and keeps the alert accessible in RTL', async () => {
    language.setLanguage('ar');
    fixture.detectChanges();
    await failSignOut();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('تعذّر تأكيد تسجيل الخروج. قد تظل جلستك نشطة. يُرجى المحاولة مرة أخرى.');
    expect(alert?.getAttribute('aria-live')).toBe('assertive');
    expect(document.documentElement.dir).toBe('rtl');
  });

  it('renders one aspect-ratio-safe owner logo in the header and no duplicate sidebar logo', () => {
    const element = fixture.nativeElement as HTMLElement;
    const brand = element.querySelector('.topbar__brand app-brand-mark') as HTMLElement | null;
    expect(brand).not.toBeNull();
    const img = brand?.querySelector('img') as HTMLImageElement | null;
    expect(img?.getAttribute('src')).toBe('assets/Logo_16_9_BG_Removed.png');
    expect(img?.getAttribute('width')).toBe('1536');
    expect(img?.getAttribute('height')).toBe('1024');
    expect(img?.getAttribute('alt')).toBe('MESP');
    expect(element.querySelector('.sidebar__brand')).toBeNull();
    expect(element.innerHTML).not.toContain('assets/brand/icon-96.png');
    expect(element.querySelectorAll('.topbar__brand img').length).toBe(1);
  });

  it('keeps the light-surface header artwork unmirrored in RTL', () => {
    language.setLanguage('ar');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const img = element.querySelector('.topbar__brand img') as HTMLImageElement | null;
    expect(img?.getAttribute('src')).toBe('assets/Logo_16_9_BG_Removed.png');
    expect(img?.style.transform).toBe('');
  });
});
