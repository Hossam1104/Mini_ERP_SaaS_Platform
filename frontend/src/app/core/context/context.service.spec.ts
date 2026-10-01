import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
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
  selectionVersion: 0,
  displayName: null,
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
    auth.acceptServerSession(session);
  });

  afterEach(() => http.verify());

  it('loads only server-derived context candidates', async () => {
    const result = service.load();
    const request = http.expectOne('/api/v1/auth/contexts');
    expect(request.request.method).toBe('GET');
    request.flush({
      contexts: [{
        contextId: 'context-a',
        kind: 'OrdinaryMembership',
        tenantId: 'tenant-a',
        displayName: 'Alpha workspace',
        eligibilityVersion: 3,
      }],
    });

    await expect(result).resolves.toHaveLength(1);
    expect(service.contexts()[0].displayName).toBe('Alpha workspace');
  });

  it('posts the exact server-confirmed switch contract and adopts only its response', async () => {
    const load = service.load();
    http.expectOne('/api/v1/auth/contexts').flush({
      contexts: [{
        contextId: 'context-a',
        kind: 'OrdinaryMembership',
        tenantId: 'tenant-a',
        displayName: 'Alpha workspace',
        eligibilityVersion: 3,
      }],
    });
    await load;

    const switching = service.switchContext('context-a');
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));
    const request = http.expectOne('/api/v1/auth/context-switch');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      contextId: 'context-a',
      expectedSelectionVersion: 0,
      expectedEligibilityVersion: 3,
    });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('memory-token');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    request.flush({
      ...session,
      selectedPath: 'OrdinaryMembership',
      selectedTenantId: 'tenant-a',
      selectedContextId: 'context-a',
      selectionVersion: 1,
    });

    await expect(switching).resolves.toBe(true);
    expect(auth.session()?.selectedContextId).toBe('context-a');
    expect(auth.session()?.selectedTenantId).toBe('tenant-a');
  });
});
