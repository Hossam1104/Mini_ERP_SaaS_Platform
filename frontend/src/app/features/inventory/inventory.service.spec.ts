import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { InventoryService } from './inventory.service';

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

describe('InventoryService', () => {
  let auth: AuthService;
  let service: InventoryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        InventoryService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    auth = TestBed.inject(AuthService);
    service = TestBed.inject(InventoryService);
    http = TestBed.inject(HttpTestingController);
    auth.acceptServerSession(session);
  });

  afterEach(() => http.verify());

  it('keeps availability queries server-readable and does not send a client Tenant id', () => {
    service.availability({
      warehouseId: 'warehouse-1',
      companyId: 'company-1',
      branchId: null,
      productId: 'product-1',
      unitOfMeasureId: 'unit-1',
      trackingIdentity: 'LOT-1',
    }).subscribe();

    const request = http.expectOne('/api/v1/inventory/availability?warehouseId=warehouse-1&companyId=company-1&productId=product-1&unitOfMeasureId=unit-1&trackingIdentity=LOT-1');
    expect(request.request.method).toBe('GET');
    expect(request.request.urlWithParams).not.toContain('tenantId');
    request.flush({});
  });

  it('sends antiforgery, idempotency, and If-Match evidence for opening actions', async () => {
    const saving = service.validateOpening('opening-1', 'AQ==', 'Validated by Inventory');

    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    const request = http.expectOne('/api/v1/inventory/opening-balances/opening-1/validate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Validated by Inventory' });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('memory-token');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.headers.get('If-Match')).toBe('"AQ=="');
    request.flush({ id: 'opening-1', version: 'Ag==' });

    await expect(saving).resolves.toMatchObject({ id: 'opening-1', version: 'Ag==' });
  });

  it('exposes stock-control reads and sends mutation evidence for reason catalogue updates and movement corrections', async () => {
    service.reasonCodes('Adjustment').subscribe();
    const reasons = http.expectOne('/api/v1/inventory/reason-codes?category=Adjustment');
    expect(reasons.request.method).toBe('GET');
    reasons.flush([]);

    const updating = service.updateReasonCode('reason-1', 'AQ==', {
      englishName: 'Damage',
      arabicName: 'تلف',
      category: 'Adjustment',
      isActive: true,
    });
    http.expectOne('/api/v1/auth/antiforgery').flush(
      { status: 'issued' },
      { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) },
    );
    await new Promise((resolve) => setTimeout(resolve, 0));
    const update = http.expectOne('/api/v1/inventory/reason-codes/reason-1/update');
    expect(update.request.method).toBe('POST');
    expect(update.request.headers.get('If-Match')).toBe('"AQ=="');
    expect(update.request.headers.get('Idempotency-Key')).toBeTruthy();
    update.flush({ id: 'reason-1', version: 'Ag==' });
    await expect(updating).resolves.toMatchObject({ id: 'reason-1' });

    const correcting = service.correctMovement('movement-1', 'Ag==', 'DAMAGE', 'reviewed');
    await new Promise((resolve) => setTimeout(resolve, 0));
    const correction = http.expectOne('/api/v1/inventory/ledger/movement-1/correct');
    expect(correction.request.method).toBe('POST');
    expect(correction.request.body).toEqual({ reasonCode: 'DAMAGE', reason: 'reviewed' });
    expect(correction.request.headers.get('If-Match')).toBe('"Ag=="');
    expect(correction.request.headers.get('Idempotency-Key')).toBeTruthy();
    correction.flush({ id: 'correction-1' });
    await expect(correcting).resolves.toMatchObject({ id: 'correction-1' });
  });

  it('submits physical count observations and records a reviewer variance reason separately', async () => {
    const submitting = service.submitCount('count-1', 'AQ==', { observations: [{ countLineId: 'line-1', countedQuantity: 7 }] });
    http.expectOne('/api/v1/auth/antiforgery').flush({ status: 'issued' }, { headers: new HttpHeaders({ 'X-CSRF-TOKEN': 'memory-token' }) });
    await new Promise((resolve) => setTimeout(resolve, 0));
    const submit = http.expectOne('/api/v1/inventory/counts/count-1/submit');
    expect(submit.request.body).toEqual({ observations: [{ countLineId: 'line-1', countedQuantity: 7 }] });
    expect(submit.request.body.observations[0]).not.toHaveProperty('varianceReasonCode');
    submit.flush({ id: 'count-1', status: 'PendingApproval', version: 'Ag==' });
    await expect(submitting).resolves.toMatchObject({ id: 'count-1' });

    const recording = service.recordCountVarianceReason('count-1', 'Ag==', 'line-1', 'DAMAGE');
    await new Promise((resolve) => setTimeout(resolve, 0));
    const reason = http.expectOne('/api/v1/inventory/counts/count-1/variance-reason');
    expect(reason.request.method).toBe('POST');
    expect(reason.request.body).toEqual({ countLineId: 'line-1', reasonCode: 'DAMAGE' });
    expect(reason.request.headers.get('If-Match')).toBe('"Ag=="');
    reason.flush({ id: 'count-1', status: 'PendingApproval', version: 'Aw==' });
    await expect(recording).resolves.toMatchObject({ id: 'count-1' });
  });
});
