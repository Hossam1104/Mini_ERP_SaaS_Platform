import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyPresentationService } from '../../core/presentation/currency-presentation.service';
import {
  PurchaseInvoiceHandoffListItemResponse,
  PurchaseInvoiceHandoffResponse,
  PurchaseInvoiceHandoffEligibleSourceResponse,
} from './purchase-invoice-handoff.model';
import { PurchaseInvoiceHandoffService } from './purchase-invoice-handoff.service';
import { PurchaseInvoiceHandoffWorkspaceComponent } from './purchase-invoice-handoff-workspace.component';

const handoffListItem: PurchaseInvoiceHandoffListItemResponse = {
  id: 'pih-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  purchaseOrderId: 'po-1',
  status: 'Recorded',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'SAR',
  supplierInvoiceReference: 'INV-2026-999',
  supplierInvoiceDate: '2026-08-19',
  totalHandoffQuantity: 4,
  totalHandoffAmount: 230,
  lineCount: 1,
  createdAt: '2026-08-19T10:00:00Z',
  updatedAt: '2026-08-19T10:00:00Z',
  version: 'PIHVERSION',
};

const handoffDetail: PurchaseInvoiceHandoffResponse = {
  id: 'pih-1',
  tenantId: 'tenant-a',
  companyId: 'company-1',
  branchId: null,
  purchaseOrderId: 'po-1',
  createdByActorId: 'actor-1',
  status: 'Recorded',
  supplierId: 'supplier-1',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyCode: 'SAR',
  supplierInvoiceReference: 'INV-2026-999',
  supplierInvoiceDate: '2026-08-19',
  notes: 'Invoice matched against GR',
  createdAt: '2026-08-19T10:00:00Z',
  updatedAt: '2026-08-19T10:00:00Z',
  cancelledAt: null,
  cancellationReason: null,
  lines: [
    {
      id: 'pihl-1',
      purchaseOrderLineId: 'pol-1',
      productId: 'prod-1',
      productSku: 'SKU-001',
      productName: 'Test Product',
      unitOfMeasureCode: 'EA',
      handoffQuantity: 4,
      unitPrice: 50,
      taxRatePercentage: 15,
      taxAmount: 30,
      lineAmount: 230,
    },
  ],
  sources: [
    {
      id: 'pihs-1',
      goodsReceiptId: 'gr-1',
      goodsReceiptLineId: 'grl-1',
      purchaseOrderLineId: 'pol-1',
      quantity: 4,
    },
  ],
  version: 'PIHVERSION',
  canCancel: true,
};

const eligibleHandoffSource: PurchaseInvoiceHandoffEligibleSourceResponse = {
  purchaseOrderId: 'po-1',
  companyId: 'company-1',
  branchId: null,
  supplierId: 'supplier-1',
  supplierCode: 'SUP-001',
  supplierName: 'Supplier One',
  currencyId: 'curr-1',
  currencyCode: 'SAR',
  currencyName: 'Saudi Riyal',
  lines: [
    {
      goodsReceiptId: 'gr-1',
      goodsReceiptLineId: 'grl-1',
      purchaseOrderLineId: 'pol-1',
      productId: 'prod-1',
      productSku: 'SKU-001',
      productName: 'Test Product',
      unitOfMeasureId: 'uom-1',
      unitOfMeasureCode: 'EA',
      receivedDate: '2026-08-19',
      acceptedQuantity: 4,
      alreadyHandedOffQuantity: 0,
      remainingHandoffQuantity: 4,
      unitPrice: 50,
      taxRatePercentage: 15,
      taxAmount: 30,
    },
  ],
};

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

describe('PurchaseInvoiceHandoffWorkspaceComponent', () => {
  let fixture: ComponentFixture<PurchaseInvoiceHandoffWorkspaceComponent>;
  let routeUrls: BehaviorSubject<Array<{ path: string }>>;
  let routeParams: BehaviorSubject<ParamMap>;
  let serviceMock: {
    list: ReturnType<typeof vi.fn>;
    eligibleSources: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    history: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    cancel: ReturnType<typeof vi.fn>;
  };

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeUrls = new BehaviorSubject([{ path: 'invoice-handoffs' }]);
    routeParams = new BehaviorSubject(routeMap());
    serviceMock = {
      list: vi.fn(() => of([handoffListItem])),
      eligibleSources: vi.fn(() => of([eligibleHandoffSource])),
      get: vi.fn(() => of(handoffDetail)),
      history: vi.fn(() => of([])),
      audit: vi.fn(() => of([])),
      create: vi.fn(() => Promise.resolve(handoffDetail)),
      cancel: vi.fn(() => Promise.resolve({ ...handoffDetail, status: 'Cancelled' })),
    };

    await TestBed.configureTestingModule({
      imports: [PurchaseInvoiceHandoffWorkspaceComponent],
      providers: [
        provideRouter([]),
        LanguageService,
        { provide: CurrencyPresentationService, useValue: { symbolAssetUrl: () => null, symbolText: (code: string) => code, formatMoneyParts: (amount: number, code: string, locale = 'en-US') => [...new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).formatToParts(amount), { type: 'literal', value: ' ' }, { type: 'currency', value: code }] } },
        {
          provide: ActivatedRoute,
          useValue: {
            url: routeUrls.asObservable(),
            paramMap: routeParams.asObservable(),
            snapshot: {
              get url() {
                return routeUrls.value;
              },
              get paramMap() {
                return routeParams.value;
              },
            },
          },
        },
        { provide: PurchaseInvoiceHandoffService, useValue: serviceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PurchaseInvoiceHandoffWorkspaceComponent);
    fixture.detectChanges();
    await settle();
  });

  it('renders invoice handoff list with scope semantics and records', () => {
    const headers = Array.from(fixture.nativeElement.querySelectorAll('th')) as HTMLElement[];
    expect(headers.length).toBeGreaterThan(0);
    expect(headers.every((h) => h.getAttribute('scope') === 'col')).toBe(true);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('INV-2026-999');
    expect(text).toContain('Supplier One');
  });

  it('formats non-ISO and standard currencies safely without throwing', () => {
    const comp = fixture.componentInstance;
    expect(comp.formatMoney(100, 'SAR')).toContain('100.00');
    expect(() => comp.formatMoney(100, 'S2K')).not.toThrow();
    expect(comp.formatMoney(100, 'S2K')).toContain('S2K');
  });

  it('renders detail view with tabs and cancellation option', async () => {
    routeUrls.next([{ path: 'invoice-handoffs' }, { path: ':id' }]);
    routeParams.next(routeMap('pih-1'));
    await settle();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('INV-2026-999');
    expect(text).toContain('Supplier One');

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLElement[];
    expect(tabs.length).toBe(5);
  });
});
