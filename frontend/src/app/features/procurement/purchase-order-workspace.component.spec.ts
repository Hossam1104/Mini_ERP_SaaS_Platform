import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyPresentationService } from '../../core/presentation/currency-presentation.service';
import { PurchaseOrderListItemResponse, PurchaseOrderResponse } from './purchase-order.model';
import { PurchaseOrderService } from './purchase-order.service';
import { PurchaseOrderWorkspaceComponent } from './purchase-order-workspace.component';

const purchaseOrderListItem: PurchaseOrderListItemResponse = {
  id: 'po-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  status: 'Draft',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  supplierQuotationReference: 'SUP-Q-2026-001',
  currencyCode: 'SAR',
  total: 575,
  lineCount: 1,
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  version: 'POVERSION',
};

const purchaseOrderDetail: PurchaseOrderResponse = {
  id: 'po-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  createdByActorId: 'creator-1',
  status: 'Draft',
  source: {
    purchaseRequestId: 'pr-1',
    supplierQuotationId: 'quotation-1',
    sourceDecisionId: 'decision-1',
    purchaseRequestReference: 'PR-0001',
    purchaseRequestPurpose: 'Office supplies',
    supplierQuotationReference: 'SUP-Q-2026-001',
    supplier: { id: 'supplier-1', code: 'SUP-001', name: 'Supplier One' },
    currency: { id: 'currency-1', code: 'SAR', name: 'Saudi Riyal' },
    paymentTerm: null,
    sourceDecisionRationale: 'Selected quotation',
    selectedAt: '2026-08-14T10:00:00Z',
  },
  notes: null,
  createdAt: '2026-08-14T10:00:00Z',
  updatedAt: '2026-08-14T10:00:00Z',
  submittedAt: null,
  approvedAt: null,
  issuedAt: null,
  cancelledAt: null,
  latestConfirmationId: null,
  latestConfirmationStatus: null,
  approval: null,
  lines: [{
    id: 'line-1', sourceQuotationLineId: 'quotation-line-1', purchaseRequestLineId: 'request-line-1',
    productSku: 'SKU-001', productName: 'Widget', unitOfMeasureCode: 'PCS', orderedQuantity: 5,
    confirmedQuantity: 0, remainingQuantity: 5, unitPrice: 10, discountAmount: null, discountPercentage: null,
    taxCode: null, taxName: null, taxRatePercentage: null, taxAmount: null, requestedNeedByDate: '2026-09-01',
    deliveryDate: null, notes: null, version: 'LINEVERSION',
  }],
  pendingChanges: [],
  version: 'POVERSION',
  canEdit: true,
  canSubmit: true,
  canApprove: false,
  canReject: true,
  canReturnForChange: false,
  canIssue: false,
  canCancel: true,
  canCaptureConfirmation: false,
  canApproveSupplierChange: false,
  canRejectSupplierChange: false,
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PurchaseOrderWorkspaceComponent', () => {
  let fixture: ComponentFixture<PurchaseOrderWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let orders: {
    list: ReturnType<typeof vi.fn>;
    sourceOptions: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    confirmations: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'purchase-orders' }]);
    routeParams = new BehaviorSubject(routeMap());
    orders = {
      list: vi.fn(() => of([purchaseOrderListItem])),
      sourceOptions: vi.fn(() => of([])),
      get: vi.fn(() => of(null)),
      confirmations: vi.fn(() => of([])),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
    };

    await TestBed.configureTestingModule({
      imports: [PurchaseOrderWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: CurrencyPresentationService, useValue: { symbolAssetUrl: () => null, symbolText: (code: string) => code } },
        { provide: ActivatedRoute, useValue: { url: routeUrls.asObservable(), paramMap: routeParams.asObservable(), snapshot: { get url() { return routeUrls.value; }, get paramMap() { return routeParams.value; } } } },
        { provide: PurchaseOrderService, useValue: orders },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseOrderWorkspaceComponent);
    fixture.detectChanges();
    await settle();
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

  it('uses the configured SAR fallback in the list amount cell', () => {
    const amount = (fixture.nativeElement as HTMLElement).querySelector('.data-grid-money');
    expect(amount?.textContent).toContain('SAR');
    expect(amount?.querySelector('img')).toBeNull();
  });

  it('uses singular English and Arabic plural categories for line counts', () => {
    const component = fixture.componentInstance;
    const language = TestBed.inject(LanguageService);
    expect(component.formatLineCount(1)).toBe('1 line');
    expect(component.formatLineCount(2)).toBe('2 lines');

    language.setLanguage('ar');
    const count = (value: number) => new Intl.NumberFormat('ar').format(value);
    expect(component.formatLineCount(0)).toBe(`${count(0)} بنود`);
    expect(component.formatLineCount(1)).toBe(`${count(1)} بند`);
    expect(component.formatLineCount(2)).toBe(`${count(2)} بندان`);
    expect(component.formatLineCount(3)).toBe(`${count(3)} بنود`);
    expect(component.formatLineCount(11)).toBe(`${count(11)} بندًا`);
    expect(component.formatLineCount(100)).toBe(`${count(100)} بند`);
    language.setLanguage('en');
  });

  it('renders purchase order list with non-ISO currency code without breaking list rendering or subsequent rows', async () => {
    const s2kItem: PurchaseOrderListItemResponse = {
      ...purchaseOrderListItem,
      id: 's2k-po-id',
      currencyCode: 'S2K',
      total: 1250,
      supplierQuotationReference: 'SUP-Q-S2K',
    };
    const usdItem: PurchaseOrderListItemResponse = {
      ...purchaseOrderListItem,
      id: 'usd-po-id',
      currencyCode: 'USD',
      total: 2500,
      supplierQuotationReference: 'SUP-Q-USD',
    };

    orders.list.mockReturnValue(of([s2kItem, usdItem]));
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

  it('renders column headers with explicit scope semantics', () => {
    const headers = Array.from(fixture.nativeElement.querySelectorAll('th')) as HTMLElement[];
    expect(headers.length).toBeGreaterThan(0);
    expect(headers.every((header) => header.getAttribute('scope') === 'col')).toBe(true);
  });

  it('keeps detail tabs and panels linked with stable accessible ids', async () => {
    orders.get.mockReturnValue(of(purchaseOrderDetail));
    routeUrls.next([{ path: 'purchase-orders' }, { path: ':id' }]);
    routeParams.next(routeMap(purchaseOrderDetail.id));
    await settle();

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLElement[];
    expect(tabs).toHaveLength(5);
    for (const tab of tabs) {
      const panelId = tab.getAttribute('aria-controls');
      expect(tab.id).toMatch(/^purchase-order-tab-/);
      expect(panelId).toMatch(/^purchase-order-tabpanel-/);
      const panel = fixture.nativeElement.querySelector(`#${panelId}`) as HTMLElement | null;
      expect(panel).not.toBeNull();
      expect(panel?.getAttribute('aria-labelledby')).toBe(tab.id);
    }
  });

  it('communicates new-source-decision recovery for terminal rejected or cancelled orders', async () => {
    orders.get.mockReturnValue(of({ ...purchaseOrderDetail, status: 'Rejected' }));
    routeUrls.next([{ path: 'purchase-orders' }, { path: ':id' }]);
    routeParams.next(routeMap(purchaseOrderDetail.id));
    await settle();

    const recovery = fixture.nativeElement.querySelector('[data-testid="purchase-order-terminal-recovery"]') as HTMLElement | null;
    expect(recovery).not.toBeNull();
    expect(recovery?.textContent).toContain('consumed its source decision');
    expect(recovery?.textContent).toContain('Controlled reopening is not available');
  });

  it('traps action-dialog focus, closes on Escape, and restores the opener focus', async () => {
    orders.get.mockReturnValue(of(purchaseOrderDetail));
    routeUrls.next([{ path: 'purchase-orders' }, { path: ':id' }]);
    routeParams.next(routeMap(purchaseOrderDetail.id));
    await settle();

    const opener = fixture.nativeElement.querySelector('button.button--danger') as HTMLButtonElement;
    opener.focus();
    fixture.componentInstance.openAction('reject');
    fixture.detectChanges();
    await settle();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    const buttons = Array.from(dialog.querySelectorAll('button')) as HTMLButtonElement[];
    const focusable = Array.from(dialog.querySelectorAll('textarea, button')) as HTMLElement[];
    expect(document.activeElement).toBe(buttons[0]);

    focusable.at(-1)?.focus();
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(focusable[0]);

    focusable[0].focus();
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(focusable.at(-1));

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    fixture.detectChanges();
    await settle();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });
});
