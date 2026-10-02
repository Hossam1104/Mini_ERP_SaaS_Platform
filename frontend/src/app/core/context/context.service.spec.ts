import { HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { authInterceptor } from '../api/auth.interceptor';
import { ContextService } from './context.service';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 4,
  displayName: 'Owner',
  login: 'owner@example.com',
};

describe('ContextService', () => {
  let auth: AuthService;
  let service: ContextService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        ContextService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(ContextService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads emergency Tenant choices through the protected list operation', async () => {
    const adminSession = { ...session, isEmergencySuperAdministrator: true };
    auth.acceptServerSession(adminSession);
    const loading = service.loadEmergencyTenants();
    const request = http.expectOne('/api/v1/auth/emergency-tenants');
    expect(request.request.method).toBe('GET');
    request.flush({ tenants: [
      { tenantId: 'tenant-a', displayName: 'Alpha ERP', arabicDisplayName: 'Alpha AR' },
      { tenantId: 'tenant-b', displayName: 'Beta ERP', arabicDisplayName: null },
    ] });

    await expect(loading).resolves.toHaveLength(2);
    expect(service.emergencyTenants()[0].displayName).toBe('Alpha ERP');
  });

  it('rejects ordinary client state before sending an emergency Tenant switch', async () => {
    auth.acceptServerSession(session);
    service.emergencyTenants.set([{ tenantId: 'tenant-a', displayName: 'Alpha ERP' }]);

    await expect(service.switchEmergencyTenant('tenant-a')).resolves.toBe(false);
    expect(service.lastError()?.status).toBe(403);
    http.expectNone('/api/v1/auth/antiforgery');
    http.expectNone('/api/v1/auth/emergency-tenant-switch');
  });

  it('sends the selected Tenant switch with antiforgery and adopts only the server session', async () => {
    const adminSession = { ...session, isEmergencySuperAdministrator: true };
    auth.acceptServerSession(adminSession);
    service.emergencyTenants.set([{ tenantId: 'tenant-b', displayName: 'Beta ERP' }]);

    const switching = service.switchEmergencyTenant('tenant-b');
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));
    const request = http.expectOne('/api/v1/auth/emergency-tenant-switch');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ tenantId: 'tenant-b', expectedSelectionVersion: 4 });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('memory-token');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    request.flush({
      ...adminSession,
      selectedPath: 'OrdinaryMembership',
      selectedTenantId: 'tenant-b',
      selectedContextId: 'context-b',
      selectionVersion: 5,
    });

    await expect(switching).resolves.toBe(true);
    expect(auth.session()?.selectedTenantId).toBe('tenant-b');
    expect(auth.session()?.selectionVersion).toBe(5);
  });

  it('switches only an operational context already supplied by the server', async () => {
    auth.acceptServerSession(session);
    service.operationalContexts.set([
      { contextId: 'company-a', kind: 'Company', displayName: 'Alpha Company', eligibilityVersion: 2 },
      { contextId: 'branch-a', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 3 },
    ]);
    service.selectedOperationalContextId.set('company-a');
    service.operationalSelectionVersion.set(7);

    const switching = service.switchOperationalContext('branch-a');
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'operational-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));
    const request = http.expectOne('/api/v1/auth/operational-context-switch');
    expect(request.request.body).toEqual({
      contextId: 'branch-a',
      expectedSelectionVersion: 7,
      expectedEligibilityVersion: 3,
    });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('operational-token');
    request.flush({
      selectedContext: { contextId: 'branch-a', kind: 'Branch', displayName: 'Alpha Branch', eligibilityVersion: 3 },
      selectionVersion: 8,
    });

    await expect(switching).resolves.toBe(true);
    expect(service.selectedOperationalContextId()).toBe('branch-a');
    expect(service.operationalSelectionVersion()).toBe(8);
  });
});
