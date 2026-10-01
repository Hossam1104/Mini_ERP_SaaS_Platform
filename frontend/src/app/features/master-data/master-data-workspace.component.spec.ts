import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { BehaviorSubject, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyRecord, CustomerRecord, ExchangeRateRecord, MasterDataRecord, PaymentTermRecord, SupplierRecord } from './master-data.models';
import { MasterDataService } from './master-data.service';
import { MasterDataWorkspaceComponent } from './master-data-workspace.component';

@Component({ standalone: true, template: '' })
class RouterTargetComponent {}

const category: MasterDataRecord = {
  id: 'category-1',
  tenantId: 'tenant-a',
  code: 'CAT-01',
  englishName: 'Materials',
  arabicName: 'مواد',
  parentCategoryId: null,
  lifecycleState: 'Active',
  version: 'AQ==',
  trackingDefaultEnabled: false,
};

const supplier: SupplierRecord = {
  id: 'supplier-1',
  tenantId: 'tenant-a',
  code: 'SUP-01',
  englishLegalName: 'Northwind Supply',
  arabicLegalName: null,
  englishTradingName: 'Northwind',
  arabicTradingName: null,
  registrationReference: null,
  lifecycleState: 'Active',
  version: 'Ag==',
  contacts: [],
};

const customer: CustomerRecord = {
  id: 'customer-1',
  tenantId: 'tenant-a',
  code: 'CUS-01',
  englishLegalName: 'Acme Buyer',
  arabicLegalName: null,
  englishTradingName: null,
  arabicTradingName: null,
  lifecycleState: 'Inactive',
  version: 'Aw==',
  contacts: [],
};

const currency: CurrencyRecord = {
  id: 'currency-1',
  tenantId: 'tenant-a',
  code: 'SAR',
  englishName: 'Saudi Riyal',
  arabicName: null,
  revision: 2,
  lifecycleState: 'Active',
  version: 'BA==',
};

const paymentTerm: PaymentTermRecord = {
  id: 'term-1',
  tenantId: 'tenant-a',
  lifecycleState: 'Active',
  version: 'BA==',
  code: 'NET30',
  englishName: 'Net 30',
  arabicName: null,
  currentVersionNumber: 1,
  versions: [{
    id: 'term-version-1',
    versionNumber: 1,
    effectiveFrom: '2026-01-01',
    effectiveTo: null,
    baseDateRule: 'InvoiceDate',
    scheduleMode: 'SingleDueDate',
    dueOffsetDays: 30,
    dueOffsetMonths: 0,
    installments: [],
    earlySettlementDiscountEnabled: false,
    earlySettlementDiscountPercentage: null,
    earlySettlementDiscountDays: 0,
    earlySettlementDiscountMonths: 0,
    code: 'NET30',
    englishName: 'Net 30',
    arabicName: null,
  }],
};

const exchangeRate: ExchangeRateRecord = {
  id: 'exchange-rate-1',
  tenantId: 'tenant-a',
  lifecycleState: 'Active',
  version: 'BQ==',
  sourceCurrencyId: 'currency-1',
  targetCurrencyId: 'currency-2',
  sourceCurrencyCode: 'USD',
  targetCurrencyCode: 'SAR',
  currentVersionNumber: 1,
  versions: [{
    id: 'exchange-rate-version-1',
    versionNumber: 1,
    effectiveFrom: '2026-01-01',
    effectiveTo: null,
    rate: 3.75,
    rateScale: 2,
    provenance: 'Manual',
    sourceNotes: 'Treasury import',
    sourceCurrencyCode: 'USD',
    targetCurrencyCode: 'SAR',
  }],
};

function routeMap(resource: string, id?: string): ParamMap {
  return convertToParamMap(id ? { resource, id } : { resource });
}

describe('MasterDataWorkspaceComponent', () => {
  let fixture: ComponentFixture<MasterDataWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let data: {
    list: ReturnType<typeof vi.fn>;
    get: ReturnType<typeof vi.fn>;
    audit: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    edit: ReturnType<typeof vi.fn>;
    lifecycle: ReturnType<typeof vi.fn>;
    referenceExchangeRate: ReturnType<typeof vi.fn>;
  };

  async function settleAsyncWork(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(async () => {
    routeParams = new BehaviorSubject(routeMap('categories'));
    data = {
      list: vi.fn((resource: string) => of(resource === 'categories' ? [category] : resource === 'suppliers' ? [supplier] : resource === 'customers' ? [customer] : resource === 'currencies' ? [currency] : resource === 'payment-terms' ? [paymentTerm] : resource === 'exchange-rates' ? [exchangeRate] : [])),
      get: vi.fn((resource: string) => of(resource === 'suppliers' ? supplier : resource === 'customers' ? customer : resource === 'currencies' ? currency : resource === 'payment-terms' ? paymentTerm : resource === 'exchange-rates' ? exchangeRate : category)),
      audit: vi.fn(() => of([])),
      create: vi.fn(),
      edit: vi.fn(),
      lifecycle: vi.fn(),
      referenceExchangeRate: vi.fn(() => of({
        ...exchangeRate,
        versionNumber: 1,
        versionId: 'exchange-rate-version-1',
        effectiveOn: '2026-02-01',
        effectiveFrom: '2026-01-01',
        effectiveTo: null,
        rate: 3.75,
        rateScale: 2,
        provenance: 'Manual',
        sourceNotes: 'Treasury import',
        referenceValue: 'USD->SAR;v1',
      })),
    };

    await TestBed.configureTestingModule({
      imports: [MasterDataWorkspaceComponent],
      providers: [
        provideRouter([
          { path: 'app/master-data/:resource', component: RouterTargetComponent },
          { path: 'app/master-data/:resource/:id', component: RouterTargetComponent },
        ]),
        LanguageService,
        { provide: MasterDataService, useValue: data },
        {
          provide: AuthService,
          useValue: {
            status: signal('authenticated'),
            session: signal({ selectedContextId: 'context-a' }),
          },
        },
        { provide: ActivatedRoute, useValue: { paramMap: routeParams.asObservable(), snapshot: { paramMap: routeMap('categories') } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MasterDataWorkspaceComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  });

  it('renders nine deep-linkable resource tabs under the shared page header', async () => {
    await TestBed.inject(Router).navigateByUrl('/app/master-data/categories');
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const tabs = Array.from(element.querySelectorAll<HTMLAnchorElement>('app-tabs [role="tab"]'));
    expect(tabs).toHaveLength(9);
    expect(element.querySelector('app-tabs [role="tablist"]')?.getAttribute('aria-label')).toBe('Master data resources');
    expect(element.querySelector('app-page-header h1#master-data-title')?.textContent).toContain('Categories');
    expect(tabs.map((tab) => tab.getAttribute('href'))).toContain('/app/master-data/categories');
    expect(tabs.filter((tab) => tab.tabIndex === 0)).toHaveLength(1);
    expect(tabs.find((tab) => tab.getAttribute('aria-selected') === 'true')?.tabIndex).toBe(0);
    expect(element.textContent).toContain('Categories');
    expect(element.textContent).toContain('CAT-01');
    expect(data.list).toHaveBeenCalledWith('categories');
  });

  it('filters the current list and exposes server-aware mutation affordances', () => {
    const input = fixture.nativeElement.querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'does-not-exist';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No records match this view');
    expect((fixture.nativeElement.querySelector('.button--primary') as HTMLButtonElement).disabled).toBe(false);
  });

  it('keeps Supplier detail inside master-data lifecycle and states Procurement confirmation is downstream', async () => {
    routeParams.next(routeMap('suppliers', supplier.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(data.get).toHaveBeenCalledWith('suppliers', supplier.id);
    expect(fixture.nativeElement.textContent).toContain('Procurement confirmation is a downstream workflow.');
    expect(fixture.nativeElement.textContent).not.toContain('Confirm supplier');
  });

  it('renders a Customer lifecycle state without adding a separate approval or delete path', async () => {
    routeParams.next(routeMap('customers', customer.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Inactive');
    expect(fixture.nativeElement.textContent).toContain('No Draft or delete path is exposed.');
    expect(fixture.nativeElement.textContent).not.toContain('Approve');
  });

  it('renders Payment Term history and configuration boundaries without Finance effects', async () => {
    routeParams.next(routeMap('payment-terms', paymentTerm.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(data.get).toHaveBeenCalledWith('payment-terms', paymentTerm.id);
    expect(fixture.nativeElement.textContent).toContain('Configuration version');
    expect(fixture.nativeElement.textContent).toContain('AP/AR aging, settlement, posting, and discount accounting are Finance scope.');
    expect(fixture.nativeElement.textContent).not.toContain('Post payment');
  });

  it('renders Exchange Rate pair history and resolves historical reference evidence', async () => {
    routeParams.next(routeMap('exchange-rates', exchangeRate.id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(data.get).toHaveBeenCalledWith('exchange-rates', exchangeRate.id);
    expect(data.list).toHaveBeenCalledWith('currencies');
    expect(fixture.nativeElement.textContent).toContain('USD');
    expect(fixture.nativeElement.textContent).toContain('SAR');
    expect(fixture.nativeElement.textContent).toContain('Historical reference evidence');
    expect(fixture.nativeElement.textContent).toContain('Treasury import');

    const date = fixture.nativeElement.querySelector('input[name="exchangeReferenceDate"]') as HTMLInputElement;
    date.value = '2026-02-01';
    date.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const resolveButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find((button) => button.textContent?.includes('Resolve rate')) as HTMLButtonElement;
    resolveButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(data.referenceExchangeRate).toHaveBeenCalledWith(exchangeRate.id, '2026-02-01');
    expect(fixture.nativeElement.textContent).toContain('USD->SAR;v1');
  });
});
