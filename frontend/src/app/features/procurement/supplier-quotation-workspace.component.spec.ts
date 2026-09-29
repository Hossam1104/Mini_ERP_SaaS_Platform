import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyPresentationService } from '../../core/presentation/currency-presentation.service';
import { MasterDataService } from '../master-data/master-data.service';
import { PurchaseRequestListItemResponse, PurchaseRequestOrganizationScopeResponse, PurchaseRequestResponse } from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';
import {
  SupplierQuotationComparisonResponse,
  SupplierQuotationListItemResponse,
  SupplierQuotationResponse,
} from './supplier-quotation.model';
import { SupplierQuotationService } from './supplier-quotation.service';
import { SupplierQuotationWorkspaceComponent } from './supplier-quotation-workspace.component';

const companyId = '22222222-2222-2222-2222-222222222222';
const purchaseRequestId = '11111111-1111-1111-1111-111111111111';
const quotationId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

const organizationScope: PurchaseRequestOrganizationScopeResponse = {
  companyId,
  branchId: null,
  companyDisplayName: 'Acme Trading Co.',
  branchDisplayName: null,
  displayName: 'Acme Trading Co.',
};

const requestListItem: PurchaseRequestListItemResponse = {
  id: purchaseRequestId,
  companyId,
  branchId: null,
  requesterId: 'requester-1',
  status: 'Approved',
  purpose: 'Office restocking',
  lineCount: 1,
  createdAt: '2026-08-13T10:00:00Z',
  updatedAt: '2026-08-13T10:00:00Z',
  version: 'PRVERSION',
};

const request: PurchaseRequestResponse = {
  id: purchaseRequestId,
  tenantId: 'tenant-a',
  companyId,
  branchId: null,
  requesterId: 'requester-1',
  status: 'Approved',
  purpose: 'Office restocking',
  lines: [{
    id: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget', unitOfMeasureId: 'uom-1',
    unitOfMeasureCode: 'PCS', quantity: 5, needByDate: '2026-09-01', purpose: 'Restocking', version: 'LINEVERSION',
  }],
  approval: null,
  createdAt: '2026-08-13T10:00:00Z',
  updatedAt: '2026-08-13T10:00:00Z',
  submittedAt: '2026-08-13T11:00:00Z',
  approvedAt: '2026-08-13T12:00:00Z',
  cancelledAt: null,
  version: 'PRVERSION',
  canEdit: false,
  canSubmit: false,
  canApprove: false,
  canReject: false,
  canReturnForChange: false,
  canCancel: false,
};

const quotationListItem: SupplierQuotationListItemResponse = {
  id: quotationId,
  purchaseRequestId,
  supplier: { id: 'supplier-1', code: 'SUP-001', name: 'Supplier One' },
  status: 'Submitted',
  supplierQuotationReference: 'SUP-Q-2026-001',
  offerDate: '2026-08-14',
  validUntil: '2026-09-14',
  currency: { id: 'currency-sar', code: 'SAR', name: 'Saudi Riyal' },
  commercialTotal: 575,
  coveredLineCount: 1,
  requestedLineCount: 1,
  hasEvidence: true,
  version: 'QVERSION',
};

const comparison: SupplierQuotationComparisonResponse = {
  purchaseRequestId,
  hasMixedCurrencies: false,
  directCurrencyComparisonAvailable: true,
  comparisonBasis: 'Server-calculated commercial total within currency groups',
  currencyGroups: [{ currencyId: 'currency-sar', currencyCode: 'SAR', supplierQuotationIds: [quotationId], directlyComparableWithinGroup: true }],
  quotations: [{
    supplierQuotationId: quotationId,
    supplier: quotationListItem.supplier,
    status: 'Submitted',
    supplierQuotationReference: quotationListItem.supplierQuotationReference,
    offerDate: quotationListItem.offerDate,
    validUntil: quotationListItem.validUntil,
    currency: quotationListItem.currency,
    commercialTotal: 575,
    coveredLineCount: 1,
    requestedLineCount: 1,
    hasEvidence: true,
    isDirectlyComparableToAll: true,
    paymentTermCode: 'NET30',
    deliveryTerms: 'DAP',
    offeredDeliveryDate: '2026-09-01',
    offeredDeliveryLeadTime: null,
    lines: [{
      purchaseRequestLineId: 'line-1', productSku: 'SKU-001', productName: 'Widget', requestedQuantity: 5, quotedQuantity: 5,
      unitPrice: 100, discountAmount: null, discountPercentage: null, taxRatePercentage: 15, taxAmount: 75,
      requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', isCovered: true, qualificationIssue: null,
    }],
    qualificationIssues: [],
  }],
  currentSourceDecision: null,
};

const quotation: SupplierQuotationResponse = {
  id: quotationId,
  tenantId: 'tenant-a',
  purchaseRequestId,
  companyId,
  branchId: null,
  createdByActorId: 'actor-1',
  supplier: quotationListItem.supplier,
  status: 'Draft',
  supplierQuotationReference: 'SUP-Q-2026-001',
  offerDate: '2026-08-14',
  validUntil: '2026-09-14',
  currency: quotationListItem.currency,
  paymentTerm: { id: 'payment-term-1', code: 'NET30', name: 'Net 30 days', version: 1 },
  deliveryTerms: 'DAP',
  offeredDeliveryDate: '2026-09-01',
  offeredDeliveryLeadTime: null,
  notes: 'Routine office restocking quote.',
  lines: [{
    id: 'quotation-line-1', purchaseRequestLineId: 'line-1', productId: 'product-1', productSku: 'SKU-001', productName: 'Widget',
    unitOfMeasureId: 'uom-1', unitOfMeasureCode: 'PCS', requestedQuantity: 5, quotedQuantity: 5, unitPrice: 100,
    discountAmount: null, discountPercentage: null, taxId: 'tax-1', taxCode: 'VAT', taxName: 'Configured VAT', taxRatePercentage: 15,
    taxAmount: 75, taxReference: null, requestedNeedByDate: '2026-09-01', offeredDeliveryDate: '2026-09-01', offeredDeliveryLeadTime: null,
    notes: null, version: 'LINEVERSION',
  }],
  evidence: [],
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  submittedAt: null,
  isSelected: false,
  version: 'QVERSION',
  canEdit: true,
  canSubmit: true,
  canWithdraw: false,
  canDisqualify: false,
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('SupplierQuotationWorkspaceComponent', () => {
  let fixture: ComponentFixture<SupplierQuotationWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let purchaseRequests: { list: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn>; organizationScopes: ReturnType<typeof vi.fn> };
  let quotations: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    comparison: ReturnType<typeof vi.fn>;
    sourceDecision: ReturnType<typeof vi.fn>;
    sourceDecisionHistory: ReturnType<typeof vi.fn>;
    recordSourceDecision: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function navigate(url: Array<{ path: string }>, id?: string): Promise<void> {
    routeParams.next(routeMap(id));
    routeUrls.next(url);
    await settle();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'supplier-quotations' }]);
    routeParams = new BehaviorSubject(routeMap());
    purchaseRequests = {
      list: vi.fn(() => of([requestListItem])),
      get: vi.fn(() => of(request)),
      organizationScopes: vi.fn(() => of([organizationScope])),
    };
    quotations = {
      list: vi.fn(() => of([quotationListItem])),
      get: vi.fn(() => of(quotation)),
      comparison: vi.fn(() => of(comparison)),
      sourceDecision: vi.fn(() => of(null)),
      sourceDecisionHistory: vi.fn(() => of([])),
      recordSourceDecision: vi.fn(() => Promise.resolve({} as never)),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
    };

    await TestBed.configureTestingModule({
      imports: [SupplierQuotationWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: CurrencyPresentationService, useValue: { symbolAssetUrl: () => null, symbolText: (code: string) => code, formatMoneyParts: (amount: number, code: string, locale = 'en-US') => [...new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).formatToParts(amount), { type: 'literal', value: ' ' }, { type: 'currency', value: code }] } },
        { provide: ActivatedRoute, useValue: { url: routeUrls.asObservable(), snapshot: { get url() { return routeUrls.value; }, get paramMap() { return routeParams.value; } } } },
        { provide: PurchaseRequestService, useValue: purchaseRequests },
        { provide: SupplierQuotationService, useValue: quotations },
        { provide: MasterDataService, useValue: { list: vi.fn(() => of([])) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SupplierQuotationWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('renders a business-readable quotation list and filters loaded rows without raw GUID labels', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Supplier One');
    expect(text).toContain('Acme Trading Co.');
    expect(text).toContain('SUP-Q-2026-001');
    expect(text).not.toContain(companyId);
    expect(text).not.toContain('tenant-a');

    fixture.componentInstance.search.set('no-result');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(TestBed.inject(LanguageService).text('supplierQuotationNoRecords'));
    expect(quotations.list).toHaveBeenCalledWith(purchaseRequestId);
  });

  it('starts create from an approved Purchase Request and shows server-provided lineage read-only', async () => {
    await navigate([{ path: 'supplier-quotations' }, { path: 'new' }]);

    expect(fixture.nativeElement.querySelector('[data-testid="quotation-request-select"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[name="companyId"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('input[name="branchId"]')).toBeNull();

    await fixture.componentInstance.selectPurchaseRequest(purchaseRequestId);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Acme Trading Co.');
    expect(fixture.nativeElement.textContent).toContain('SKU-001');
    expect(fixture.nativeElement.textContent).not.toContain(companyId);
  });

  it('gates lifecycle actions from server flags and exposes comparison/source-decision controls on detail', async () => {
    await navigate([{ path: 'supplier-quotations' }, quotationId ? { path: quotationId } : { path: '' }], quotationId);

    const text = fixture.nativeElement.textContent as string;
    const language = TestBed.inject(LanguageService);
    expect(text).toContain(language.text('supplierQuotationSubmit'));
    expect(text).not.toContain(language.text('supplierQuotationWithdraw'));
    expect(text).not.toContain(language.text('supplierQuotationDisqualify'));

    fixture.componentInstance.setTab('comparison');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[type="radio"][name="source-quotation"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('textarea')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain(language.text('supplierQuotationRecordDecision'));
    expect(fixture.nativeElement.querySelector('.winner, [data-winner]')).toBeNull();
  });

  it('formats currency safely for both standard ISO and non-ISO MESP codes without throwing RangeError', () => {
    const comp = fixture.componentInstance;
    const lang = TestBed.inject(LanguageService);

    // Standard ISO currencies
    expect(comp.formatMoney(1234.56, 'SAR')).toContain('1,234.56');
    expect(comp.formatMoney(1234.56, 'SAR')).toContain('SAR');
    expect(comp.formatMoney(1234.56, 'USD')).toContain('1,234.56');

    // Non-ISO MESP configured currencies must not throw RangeError and must retain code
    expect(() => comp.formatMoney(1234.56, 'S2K')).not.toThrow();
    const s2kFormatted = comp.formatMoney(1234.56, 'S2K');
    expect(s2kFormatted).toContain('1,234.56');
    expect(s2kFormatted).toContain('S2K');

    expect(() => comp.formatMoney(500, 'CUSTOM')).not.toThrow();
    expect(comp.formatMoney(500, 'CUSTOM')).toContain('500.00');
    expect(comp.formatMoney(500, 'CUSTOM')).toContain('CUSTOM');

    // Arabic locale safe fallback
    lang.setLanguage('ar');
    expect(() => comp.formatMoney(1234.56, 'S2K')).not.toThrow();
    const arFormatted = comp.formatMoney(1234.56, 'S2K');
    expect(arFormatted).toContain('S2K');
    lang.setLanguage('en');
  });

  it('renders quotation list with non-ISO currency code without breaking list rendering or subsequent rows', async () => {
    const s2kItem: SupplierQuotationListItemResponse = {
      ...quotationListItem,
      id: 's2k-quotation-id',
      currency: { id: 'currency-s2k', code: 'S2K', name: 'Custom S2K' },
      commercialTotal: 1250,
      supplierQuotationReference: 'SUP-Q-S2K',
    };
    const usdItem: SupplierQuotationListItemResponse = {
      ...quotationListItem,
      id: 'usd-quotation-id',
      currency: { id: 'currency-usd', code: 'USD', name: 'US Dollar' },
      commercialTotal: 2500,
      supplierQuotationReference: 'SUP-Q-USD',
    };

    quotations.list.mockReturnValue(of([s2kItem, usdItem]));
    await fixture.componentInstance.loadList();
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('SUP-Q-S2K');
    expect(text).toContain('S2K');
    expect(text).toContain('1,250.00');
    // Subsequent row renders successfully
    expect(text).toContain('SUP-Q-USD');
    expect(text).toContain('2,500.00');
  });

  it('submits first source decision using the approved purchase request version as concurrency token', async () => {
    await navigate([{ path: 'supplier-quotations' }, quotationId ? { path: quotationId } : { path: '' }], quotationId);

    fixture.componentInstance.setTab('comparison');
    fixture.detectChanges();

    quotations.recordSourceDecision = vi.fn().mockResolvedValue({
      id: 'decision-1',
      tenantId: 'tenant-a',
      purchaseRequestId,
      selectedQuotationId: quotationId,
      supplier: quotationListItem.supplier,
      supplierQuotationReference: quotationListItem.supplierQuotationReference,
      actorId: 'actor-1',
      selectedAt: '2026-08-16T12:00:00Z',
      rationale: 'First decision rationale.',
      policyId: null,
      policyVersion: null,
      stageKey: null,
      comparisonSnapshotReference: 'sha256:test',
      version: 'DECISION-V1',
    });

    fixture.componentInstance.selectDecisionCandidate(quotationId);
    fixture.componentInstance.decisionRationale = 'First decision rationale.';
    await fixture.componentInstance.recordDecision();
    await settle();

    // First decision: passed request.version ('PRVERSION')
    expect(quotations.recordSourceDecision).toHaveBeenCalledWith(
      purchaseRequestId,
      quotationId,
      'First decision rationale.',
      'PRVERSION',
    );
    expect(fixture.componentInstance.successNotice()).toBe(TestBed.inject(LanguageService).text('supplierQuotationDecisionSaved'));
  });

  it('submits reselection source decision using the current source decision version as concurrency token', async () => {
    await navigate([{ path: 'supplier-quotations' }, quotationId ? { path: quotationId } : { path: '' }], quotationId);

    // Set existing current decision on the component
    fixture.componentInstance.currentDecision.set({
      id: 'decision-1',
      tenantId: 'tenant-a',
      purchaseRequestId,
      selectedQuotationId: quotationId,
      supplier: quotationListItem.supplier,
      supplierQuotationReference: quotationListItem.supplierQuotationReference,
      actorId: 'actor-1',
      selectedAt: '2026-08-16T12:00:00Z',
      rationale: 'Initial rationale.',
      policyId: null,
      policyVersion: null,
      stageKey: null,
      comparisonSnapshotReference: 'sha256:test',
      version: 'CURRENT-DECISION-V1',
    });

    fixture.componentInstance.setTab('comparison');
    fixture.detectChanges();

    quotations.recordSourceDecision = vi.fn().mockResolvedValue({
      id: 'decision-2',
      tenantId: 'tenant-a',
      purchaseRequestId,
      selectedQuotationId: quotationId,
      supplier: quotationListItem.supplier,
      supplierQuotationReference: quotationListItem.supplierQuotationReference,
      actorId: 'actor-1',
      selectedAt: '2026-08-16T13:00:00Z',
      rationale: 'Reselection rationale.',
      policyId: null,
      policyVersion: null,
      stageKey: null,
      comparisonSnapshotReference: 'sha256:test-2',
      version: 'CURRENT-DECISION-V2',
    });

    fixture.componentInstance.selectDecisionCandidate(quotationId);
    fixture.componentInstance.decisionRationale = 'Reselection rationale.';
    await fixture.componentInstance.recordDecision();
    await settle();

    // Reselection: passed currentDecision().version ('CURRENT-DECISION-V1')
    expect(quotations.recordSourceDecision).toHaveBeenCalledWith(
      purchaseRequestId,
      quotationId,
      'Reselection rationale.',
      'CURRENT-DECISION-V1',
    );
    expect(fixture.componentInstance.successNotice()).toBe(TestBed.inject(LanguageService).text('supplierQuotationDecisionSaved'));
  });

  it('handles 409 concurrency conflict on source decision cleanly without false success notice', async () => {
    await navigate([{ path: 'supplier-quotations' }, quotationId ? { path: quotationId } : { path: '' }], quotationId);

    fixture.componentInstance.setTab('comparison');
    fixture.detectChanges();

    quotations.recordSourceDecision = vi.fn().mockRejectedValue(
      new HttpErrorResponse({
        status: 409,
        error: { code: 'concurrency_conflict' },
      }),
    );

    fixture.componentInstance.selectDecisionCandidate(quotationId);
    fixture.componentInstance.decisionRationale = 'Conflicting rationale.';
    await fixture.componentInstance.recordDecision();
    await settle();

    expect(fixture.componentInstance.mutationError()?.code).toBe('concurrency_conflict');
    expect(fixture.componentInstance.successNotice()).toBeNull();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain(TestBed.inject(LanguageService).text('prConcurrencyConflictError'));
    expect(text).toContain(TestBed.inject(LanguageService).text('reloadLatestVersion'));
  });
});
