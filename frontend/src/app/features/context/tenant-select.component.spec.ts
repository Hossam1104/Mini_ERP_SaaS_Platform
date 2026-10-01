import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { FoundationContextCandidate, FoundationEntryResponse, FoundationSessionResponse } from '../../core/api/foundation.models';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { TenantSelectComponent } from './tenant-select.component';

const session: FoundationSessionResponse = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 0,
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
};

const tenantContext: FoundationContextCandidate = {
  contextId: 'context-a',
  kind: 'OrdinaryMembership',
  tenantId: 'tenant-a',
  displayName: 'Alpha Tenant',
  eligibilityVersion: 1,
};

const tenantEntry: FoundationEntryResponse = {
  entryMode: 'TenantHost',
  canonicalHost: 'tenant.localhost',
  candidateTenantId: 'tenant-a',
  candidateTenantDisplayName: 'Alpha Tenant',
  authorizedTenants: [{ tenantId: 'tenant-a', displayName: 'Alpha Tenant', canonicalHost: 'tenant.localhost' }],
  operationalContexts: [
    { contextId: 'company-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 1 },
    { contextId: 'branch-a', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 1 },
  ],
  selectedOperationalContextId: 'company-a',
  operationalSelectionVersion: 1,
  branding: { displayName: 'Alpha Tenant', logoLightUrl: null, logoDarkUrl: null, logoAltText: 'Alpha Tenant', tenantConfigured: true },
  currencyPresentation: { currencyCode: 'SAR', symbolAssetUrl: null, symbolTextFallback: 'SAR' },
  code: null,
  isDevelopment: true,
  developmentAccountHint: null,
};

describe('TenantSelectComponent', () => {
  let fixture: ComponentFixture<TenantSelectComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantSelectComponent],
      providers: [
        AuthService,
        ContextService,
        LanguageService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(AuthService).acceptServerSession(session);
    const context = TestBed.inject(ContextService);
    context.entry.set(tenantEntry);
    context.contexts.set([tenantContext]);
    context.operationalContexts.set(tenantEntry.operationalContexts);
    context.selectedOperationalContextId.set('company-a');
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TenantSelectComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('shows Tenant-host Tenant and active Company as fixed values with only Company/Branch choices', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Alpha Tenant');
    expect(element.textContent).toContain('Alpha Company · Company');
    expect(element.textContent).not.toContain('No Tenant context selected');
    expect(element.querySelector('app-context-switcher')).toBeNull();
    expect(element.querySelector('#workspace-select')).toBeNull();
    expect(Array.from(element.querySelectorAll('#operational-context-select option')).map((option) => option.textContent?.trim()))
      .toEqual(['Alpha Company - Company', 'Alpha Branch - Branch']);
  });

  it('shows a Tenant chooser on the common host only with multiple memberships', () => {
    const context = TestBed.inject(ContextService);
    context.entry.set({
      ...tenantEntry,
      entryMode: 'CommonHost',
      canonicalHost: 'mesp.localhost',
      candidateTenantId: null,
      candidateTenantDisplayName: null,
      authorizedTenants: [
        tenantEntry.authorizedTenants[0],
        { tenantId: 'tenant-b', displayName: 'Beta Tenant', canonicalHost: 'beta.localhost' },
      ],
      operationalContexts: [],
      selectedOperationalContextId: null,
      branding: { ...tenantEntry.branding, displayName: 'MESP', tenantConfigured: false },
    });
    context.contexts.set([
      tenantContext,
      { contextId: 'context-b', kind: 'OrdinaryMembership', tenantId: 'tenant-b', displayName: 'Beta Tenant', eligibilityVersion: 1 },
    ]);
    context.operationalContexts.set([]);
    context.selectedOperationalContextId.set(null);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('app-context-switcher')).not.toBeNull();
    expect(element.querySelectorAll('#workspace-select option')).toHaveLength(3);
    expect(element.textContent).toContain('Choose a Tenant');
  });

  it('uses the active common-host Tenant even if the session membership selection is absent', () => {
    const context = TestBed.inject(ContextService);
    context.entry.set({
      ...tenantEntry,
      entryMode: 'CommonHost',
      canonicalHost: 'mesp.localhost',
      authorizedTenants: [
        tenantEntry.authorizedTenants[0],
        { tenantId: 'tenant-b', displayName: 'Beta Tenant', canonicalHost: 'beta.localhost' },
      ],
    });
    context.contexts.set([
      tenantContext,
      { contextId: 'context-b', kind: 'OrdinaryMembership', tenantId: 'tenant-b', displayName: 'Beta Tenant', eligibilityVersion: 1 },
    ]);
    context.operationalContexts.set(tenantEntry.operationalContexts);
    context.selectedOperationalContextId.set('company-a');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('#context-switcher-title')?.textContent).toContain('Alpha Tenant');
    expect(element.textContent).toContain('Alpha Company · Company');
    expect(element.textContent).not.toContain('No Tenant context selected');
  });

  it('shows a single common-host Tenant as fixed without a Tenant chooser', () => {
    const context = TestBed.inject(ContextService);
    context.entry.set({ ...tenantEntry, entryMode: 'CommonHost', canonicalHost: 'mesp.localhost' });
    context.contexts.set([tenantContext]);
    context.operationalContexts.set([tenantEntry.operationalContexts[0]]);
    context.selectedOperationalContextId.set('company-a');
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Alpha Tenant');
    expect(element.textContent).toContain('Alpha Company · Company');
    expect(element.querySelector('app-context-switcher')).toBeNull();
    expect(element.querySelector('#workspace-select')).toBeNull();
    expect(element.textContent).not.toContain('No Tenant context selected');
  });
});
