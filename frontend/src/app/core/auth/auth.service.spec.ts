import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from '../api/auth.interceptor';
import { FoundationSessionResponse } from '../api/foundation.models';

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
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
};

async function flushSignOutRequest(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('bootstraps a server-issued session with credentials enabled', async () => {
    const result = service.ensureSession();
    const bypass = http.expectOne('/api/v1/auth/development-bypass');
    expect(bypass.request.method).toBe('POST');
    expect(bypass.request.body).toEqual({});
    expect(bypass.request.withCredentials).toBe(true);
    bypass.flush({ code: 'development_auth_unavailable' }, { status: 404, statusText: 'Not Found' });
    await flushSignOutRequest();
    const request = http.expectOne('/api/v1/auth/session');
    expect(request.request.withCredentials).toBe(true);
    request.flush({
      authenticated: true,
      actorId: 'actor-1',
      sessionId: 'session-1',
      lifecycleState: 'Active',
      absoluteExpiresAt: null,
      selectedPath: null,
      selectedTenantId: null,
      selectedContextId: null,
      selectionVersion: 0,
    });

    await expect(result).resolves.toBe(true);
    expect(service.status()).toBe('authenticated');
    expect(service.session()?.actorId).toBe('actor-1');
  });

  it('returns to an anonymous safe boundary on a rejected session bootstrap', async () => {
    const result = service.ensureSession();
    http.expectOne('/api/v1/auth/development-bypass').flush(
      { code: 'development_auth_unavailable' },
      { status: 404, statusText: 'Not Found' },
    );
    await flushSignOutRequest();
    const request = http.expectOne('/api/v1/auth/session');
    request.flush({ code: 'authentication_failed' }, { status: 401, statusText: 'Unauthorized' });

    await expect(result).resolves.toBe(false);
    expect(service.status()).toBe('anonymous');
    expect(service.session()).toBeNull();
  });

  it('accepts the server actor session when the explicit Development bypass is enabled', async () => {
    const result = service.ensureSession();
    const bypass = http.expectOne('/api/v1/auth/development-bypass');
    bypass.flush({
      authenticated: true,
      actorId: 'development-actor',
      sessionId: 'development-session',
      lifecycleState: 'Active',
      absoluteExpiresAt: null,
      selectedPath: null,
      selectedTenantId: null,
      selectedContextId: null,
      selectionVersion: 0,
    });

    await expect(result).resolves.toBe(true);
    expect(service.developmentBypassActive()).toBe(true);
    expect(service.status()).toBe('authenticated');
    expect(service.session()?.actorId).toBe('development-actor');
    http.expectNone('/api/v1/auth/session');
  });

  it('keeps antiforgery material in memory and exposes it only as a request header', async () => {
    const result = service.bootstrapAntiforgery();
    const request = http.expectOne('/api/v1/auth/antiforgery');
    request.flush({ status: 'issued' }, { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) });

    await expect(result).resolves.toBe(true);
    expect(service.requestHeaders().get('X-CSRF-TOKEN')).toBe('memory-token');
  });

  it('clears session and navigates only after a confirmed 204 sign-out', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    const antiforgery = http.expectOne('/api/v1/auth/antiforgery');
    antiforgery.flush({ status: 'issued' }, { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) });
    await flushSignOutRequest();
    const request = http.expectOne('/api/v1/auth/sign-out');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('logout-token');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await expect(result).resolves.toEqual({ outcome: 'signed-out' });
    expect(service.session()).toBeNull();
    expect(service.status()).toBe('anonymous');
    expect(service.requestHeaders().has('X-CSRF-TOKEN')).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('MESP-90 regression guard: does not desynchronize local session state ahead of the server sign-out response', async () => {
    // The MESP-90 defect cleared local session state and routed to /login as
    // soon as signOut() was called, desynchronized from the server outcome --
    // a false logout whenever server session revocation failed or was merely
    // slow. Local state must remain untouched until the sign-out request
    // actually settles, and must then track the server's real outcome.
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();

    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
    expect(navigate).not.toHaveBeenCalled();

    http.expectOne('/api/v1/auth/sign-out').flush(null, { status: 204, statusText: 'No Content' });

    await expect(result).resolves.toEqual({ outcome: 'signed-out' });
    expect(service.session()).toBeNull();
    expect(service.status()).toBe('anonymous');
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('clears session and navigates when 401 confirms the session is already invalid', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush(
      { code: 'authentication_failed' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await expect(result).resolves.toEqual({ outcome: 'session-already-invalid' });
    expect(service.session()).toBeNull();
    expect(service.status()).toBe('expired');
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('preserves the authenticated session when sign-out returns 503', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush(
      { code: 'request_failed' },
      { status: 503, statusText: 'Service Unavailable' },
    );

    await expect(result).resolves.toMatchObject({ outcome: 'not-confirmed' });
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
    expect(service.signOutFailed()).toBe(true);
    expect(service.signingOut()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('preserves the authenticated session when audit evidence is unavailable', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush(
      { code: 'audit_unavailable' },
      { status: 503, statusText: 'Service Unavailable' },
    );

    await expect(result).resolves.toMatchObject({
      outcome: 'not-confirmed',
      error: { code: 'audit_unavailable' },
    });
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('does not treat a malformed successful sign-out response as confirmed', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush({ unexpected: true }, { status: 200, statusText: 'OK' });

    await expect(result).resolves.toMatchObject({ outcome: 'not-confirmed' });
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('does not send sign-out when antiforgery bootstrap fails', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { code: 'request_failed' },
      { status: 503, statusText: 'Service Unavailable' },
    );

    await expect(result).resolves.toMatchObject({ outcome: 'not-confirmed' });
    http.expectNone('/api/v1/auth/sign-out');
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('preserves the authenticated session when antiforgery has no token', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush({ status: 'issued' });

    await expect(result).resolves.toMatchObject({ outcome: 'not-confirmed' });
    http.expectNone('/api/v1/auth/sign-out');
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('clears a cached token after antiforgery 403 but preserves the session for retry', async () => {
    service.acceptServerSession(authenticatedSession);

    const first = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'stale-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush(
      { code: 'antiforgery_failed' },
      { status: 403, statusText: 'Forbidden' },
    );
    await expect(first).resolves.toMatchObject({ outcome: 'not-confirmed' });
    expect(service.requestHeaders().has('X-CSRF-TOKEN')).toBe(false);
    expect(service.session()).toEqual(authenticatedSession);

    const retry = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'fresh-token' }) },
    );
    await flushSignOutRequest();
    const retryRequest = http.expectOne('/api/v1/auth/sign-out');
    expect(retryRequest.request.headers.get('X-CSRF-TOKEN')).toBe('fresh-token');
    retryRequest.flush({ code: 'request_failed' }, { status: 503, statusText: 'Service Unavailable' });
    await expect(retry).resolves.toMatchObject({ outcome: 'not-confirmed' });
  });

  it('preserves the authenticated session on a network failure', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').error(new ProgressEvent('network-error'), {
      status: 0,
      statusText: 'Network Error',
    });

    await expect(result).resolves.toMatchObject({ outcome: 'not-confirmed' });
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('does not claim anonymous or navigate after an unconfirmed sign-out', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'request_failed' }, { status: 500, statusText: 'Error' });
    await result;

    expect(service.status()).not.toBe('anonymous');
    expect(navigate).not.toHaveBeenCalled();
    expect(service.signOutFailed()).toBe(true);
  });

  it('clears the session on a successful retry after an unconfirmed attempt', async () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    service.acceptServerSession(authenticatedSession);

    const first = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'request_failed' }, { status: 503, statusText: 'Unavailable' });
    await first;

    const retry = service.signOut();
    await flushSignOutRequest();
    const retryRequest = http.expectOne('/api/v1/auth/sign-out');
    retryRequest.flush(null, { status: 204, statusText: 'No Content' });

    await expect(retry).resolves.toEqual({ outcome: 'signed-out' });
    expect(service.session()).toBeNull();
    expect(service.status()).toBe('anonymous');
    expect(service.signOutFailed()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('coalesces concurrent sign-out calls into one request', async () => {
    service.acceptServerSession(authenticatedSession);

    const first = service.signOut();
    const second = service.signOut();
    expect(second).toBe(first);
    const antiforgeryRequests = http.match('/api/v1/auth/antiforgery');
    expect(antiforgeryRequests).toHaveLength(1);
    antiforgeryRequests[0].flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    const signOutRequests = http.match('/api/v1/auth/sign-out');
    expect(signOutRequests).toHaveLength(1);
    signOutRequests[0].flush({ code: 'request_failed' }, { status: 503, statusText: 'Unavailable' });

    await expect(Promise.all([first, second])).resolves.toEqual([
      { outcome: 'not-confirmed', error: expect.anything() },
      { outcome: 'not-confirmed', error: expect.anything() },
    ]);
    expect(service.signingOut()).toBe(false);
  });

  it('uses finally only to clear the sign-out-in-progress state', async () => {
    service.acceptServerSession(authenticatedSession);

    const result = service.signOut();
    expect(service.signingOut()).toBe(true);
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'request_failed' }, { status: 503, statusText: 'Unavailable' });
    await result;

    expect(service.signingOut()).toBe(false);
    expect(service.session()).toEqual(authenticatedSession);
    expect(service.status()).toBe('authenticated');
  });

  it('does not let a stale sign-out response overwrite a newer confirmed sign-in', async () => {
    const newerSession: FoundationSessionResponse = {
      ...authenticatedSession,
      actorId: 'actor-2',
      sessionId: 'session-2',
    };
    service.acceptServerSession(authenticatedSession);

    const signOut = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'logout-token' }) },
    );
    await flushSignOutRequest();
    const signOutRequest = http.expectOne('/api/v1/auth/sign-out');

    const signIn = service.signIn('actor-2', 'password');
    http.expectOne('/api/v1/auth/sign-in').flush(newerSession);
    await expect(signIn).resolves.toBe(true);
    signOutRequest.flush(null, { status: 204, statusText: 'No Content' });
    await expect(signOut).resolves.toMatchObject({ outcome: 'not-confirmed' });

    expect(service.session()).toEqual(newerSession);
    expect(service.status()).toBe('authenticated');
  });

  it('does not write authentication material to browser storage', async () => {
    service.acceptServerSession(authenticatedSession);
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);

    const result = service.signOut();
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-only-token' }) },
    );
    await flushSignOutRequest();
    http.expectOne('/api/v1/auth/sign-out').flush({ code: 'request_failed' }, { status: 503, statusText: 'Unavailable' });
    await result;

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
