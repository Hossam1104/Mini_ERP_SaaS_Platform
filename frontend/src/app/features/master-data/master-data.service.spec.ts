import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { MasterDataService } from './master-data.service';

const session = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: 'OrdinaryMembership',
  selectedTenantId: 'tenant-a',
  selectedContextId: 'context-a',
  selectionVersion: 1,
  displayName: null,
  login: 'owner@example.com',
};

describe('MasterDataService', () => {
  let auth: AuthService;
  let service: MasterDataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        MasterDataService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(MasterDataService);
    http = TestBed.inject(HttpTestingController);
    auth.acceptServerSession(session);
  });

  afterEach(() => http.verify());

  it('uses the server API paths and sends antiforgery, idempotency, and optimistic concurrency headers', async () => {
    const saving = service.edit(
      'categories',
      'category-1',
      {
        code: 'CAT-01',
        englishName: 'Materials',
        arabicName: null,
        parentCategoryId: null,
        trackingDefaultEnabled: false,
      },
      'AQ==',
    );

    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    const request = http.expectOne('/api/v1/master-data/categories/category-1/edit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.code).toBe('CAT-01');
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('memory-token');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.headers.get('If-Match')).toBe('"AQ=="');
    request.flush({ id: 'category-1', lifecycleState: 'Active', version: 'Ag==' });

    await expect(saving).resolves.toMatchObject({ id: 'category-1', version: 'Ag==' });
  });

  it('keeps lifecycle reason in the request body without inventing a client-side approval step', async () => {
    const changing = service.lifecycle('suppliers', 'supplier-1', 'deactivate', 'Ag==', 'No longer trading');
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    const request = http.expectOne('/api/v1/master-data/suppliers/supplier-1/deactivate');
    expect(request.request.body).toEqual({ reason: 'No longer trading' });
    expect(request.request.headers.get('If-Match')).toBe('"Ag=="');
    request.flush({ id: 'supplier-1', lifecycleState: 'Inactive', version: 'Aw==' });

    await expect(changing).resolves.toMatchObject({ lifecycleState: 'Inactive' });
  });
});
