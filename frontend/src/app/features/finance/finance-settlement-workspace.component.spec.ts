import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataService } from '../master-data/master-data.service';
import { FinanceService } from './finance.service';
import { FinanceSettlementWorkspaceComponent } from './finance-settlement-workspace.component';

describe('FinanceSettlementWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceSettlementWorkspaceComponent>;

  const companies = [{
    tenantId: 'tenant-a',
    companyId: 'company-a',
    companyName: 'Alpha Company',
    functionalCurrencyCode: 'SAR',
    branchId: null,
    isActive: true,
  }];

  const exchangeRate = {
    id: 'rate-usd-sar', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==',
    sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR', currentVersionNumber: 3,
    versions: [{ id: 'rate-version-3', versionNumber: 3, effectiveFrom: '2026-08-01', effectiveTo: null, rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 test rate', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR' }],
  };
  const exchangeRateReference = {
    id: 'rate-usd-sar', tenantId: 'tenant-a', sourceCurrencyId: 'currency-usd', targetCurrencyId: 'currency-sar', sourceCurrencyCode: 'USD', targetCurrencyCode: 'SAR', lifecycleState: 'Active', versionNumber: 3, versionId: 'rate-version-3', effectiveOn: '2026-08-01', effectiveFrom: '2026-08-01', effectiveTo: null, rate: 3.75, rateScale: 6, provenance: 'Configured', sourceNotes: 'MESP-120 test rate', referenceValue: '3.750000', version: 'AQ==',
  };

  const openItem = {
    id: 'open-item-a',
    companyId: 'company-a',
    kind: 'Payable',
    supplierId: 'supplier-a',
    customerId: null,
    partyId: 'supplier-a',
    sourceEvidenceId: 'source-a',
    reference: 'PI-1001',
    documentDate: '2026-08-01',
    dueDate: '2026-08-31',
    currencyCode: 'SAR',
    originalAmount: 1250,
    allocatedAmount: 0,
    outstandingAmount: 1250,
    sourceContract: 'procurement-supplier-invoice.v1',
    sourceIdentity: 'match-a',
    recognitionState: 'Recognized',
    status: 'Open',
    recognitionJournalId: 'journal-a',
    paymentTerm: { code: 'NET30', versionNumber: 1, dueDate: '2026-08-31' },
    version: 'AQ==',
  };

  const term = {
    id: 'term-a',
    code: 'NET30',
    lifecycleState: 'Active',
    currentVersionNumber: 1,
    versions: [{ effectiveFrom: '2026-08-01', effectiveTo: null, baseDateRule: 'DocumentDate', scheduleMode: 'Offset', dueOffsetDays: 30, dueOffsetMonths: 0, installments: [] }],
  };

  const postedPayment = {
    id: 'payment-a', companyId: 'company-a', status: 'Posted', direction: 'Payment', supplierId: 'supplier-a', customerId: null,
    cashAccountId: 'cash-a', paymentMethodId: 'method-a', documentDate: '2026-08-10', currencyCode: 'SAR', amount: 500,
    functionalCurrencyCode: 'SAR', functionalAmount: 500, externalReference: 'PAY-1', postedJournalId: 'journal-payment',
    unallocatedAmount: 500, allocatedAmount: 0, version: 'AQ==', approvalRequirement: 'NotRequired',
  };

  const allocation = {
    id: 'allocation-a', companyId: 'company-a', settlementDocumentId: 'payment-a', openItemId: 'open-item-a', amount: 250,
    currencyCode: 'SAR', functionalAmount: 250, allocationDate: '2026-08-11', status: 'Active', reversalOfAllocationId: null,
    journalId: 'allocation-journal', version: 'AQ==',
  };

  beforeEach(() => {
    const empty = () => of([]);
    TestBed.configureTestingModule({
      imports: [FinanceSettlementWorkspaceComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { url: [{ path: 'ap' }] } } },
        {
          provide: MasterDataService,
          useValue: {
            list: vi.fn((resource: string) => of(resource === 'currencies' ? [{ id: 'currency-usd', tenantId: 'tenant-a', lifecycleState: 'Active', version: 'AQ==', code: 'USD', englishName: 'US Dollar', arabicName: null, revision: 1 }] : resource === 'exchange-rates' ? [exchangeRate] : [])),
            referenceExchangeRate: vi.fn((_id: string, effectiveOn: string) => of({ ...exchangeRateReference, effectiveOn })),
          },
        },
        {
          provide: FinanceService,
          useValue: {
            companies: () => of(companies),
            apOpenItems: () => of([openItem]),
            apSourceReady: () => of([{ sourceEvidenceId: 'source-a', companyId: 'company-a', supplierId: 'supplier-a', supplierCode: 'SUP-1', supplierName: 'Supplier One', supplierInvoiceReference: 'PI-READY-1', invoiceDate: '2026-08-01', currencyCode: 'SAR', amount: 1250, dueDate: '2026-08-31', paymentTerm: { code: 'NET30', englishName: 'Net 30', arabicName: null, versionNumber: 1, dueDate: '2026-08-31' }, matchResult: 'ExactMatch', alreadyRecognized: false, sourceEvidenceVersion: 1 }]),
            recognizeAp: vi.fn().mockResolvedValue(openItem),
            apAging: () => of([{ openItemId: 'open-item-a', reference: 'PI-1001', dueDate: '2026-08-31', daysOverdue: 0, outstandingAmount: 1250, currencyCode: 'SAR', status: 'Open' }]),
            arOpenItems: empty,
            arAging: empty,
            customers: () => of([{ id: 'customer-a', code: 'CUST-1', lifecycleState: 'Active' }]),
            suppliers: () => of([{ id: 'supplier-a', code: 'SUP-1', lifecycleState: 'Active' }]),
            paymentTerms: () => of([term]),
            paymentMethods: () => of([{ id: 'method-a', companyId: 'company-a', code: 'MANUAL', englishName: 'Manual', arabicName: null, direction: 'Both', lifecycle: 'Active', isManual: true, requiresReference: true, version: 'AQ==' }]),
            cashAccounts: () => of([{ id: 'cash-a', companyId: 'company-a', code: 'CASH', englishName: 'Cash', arabicName: null, kind: 'Cash', currencyCode: 'SAR', linkedAccountId: 'account-a', linkedAccountCode: '1000', lifecycle: 'Active', version: 'AQ==' }]),
            payments: empty,
            receipts: empty,
            allocations: empty,
            reconciliation: empty,
            createManualReceivable: vi.fn().mockResolvedValue(openItem),
            createPayment: vi.fn().mockResolvedValue(postedPayment),
            createReceipt: vi.fn().mockResolvedValue({ ...postedPayment, id: 'receipt-a', direction: 'Receipt', supplierId: null, customerId: 'customer-a' }),
            settlementAction: vi.fn().mockResolvedValue({}),
            postSettlement: vi.fn().mockResolvedValue({}),
            reverseSettlement: vi.fn().mockResolvedValue({}),
            createAllocation: vi.fn().mockResolvedValue(allocation),
            reverseAllocation: vi.fn().mockResolvedValue({ ...allocation, status: 'Reversed' }),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(FinanceSettlementWorkspaceComponent);
    fixture.detectChanges();
  });

  it('renders the Company-scoped AP open item and aging evidence', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="finance-settlement-workspace"]')).not.toBeNull();
    expect(element.querySelector('select')?.textContent).toContain('Alpha Company');
    expect(element.querySelector('h1')?.textContent).toContain('Accounts Payable');
    expect(element.textContent).toContain('PI-1001');
    expect(element.textContent).toContain('procurement-supplier-invoice.v1');
    expect(element.textContent).toContain('Aging');
  });

  it('keeps settlement navigation and copy RTL-safe for Arabic', () => {
    const englishTitle = fixture.nativeElement.querySelector('h1')?.textContent;
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(language.language()).toBe('ar');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).not.toBe(englishTitle);
    language.setLanguage('en');
  });

  it('renders an eligible AP source-ready candidate with a recognition action', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="ap-source-ready"]')).not.toBeNull();
    expect(element.textContent).toContain('PI-READY-1');
    (element.querySelector('[data-testid="ap-source-ready"] .grid-row-menu-trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="ap-source-ready"] [role="menuitem"]')?.textContent).toContain('Recognize payable');
    expect(element.querySelectorAll('input[type="text"]').length).toBe(0);
  });

  it('renders manual AR with selector-based customer and Payment Term fields', async () => {
    const component = fixture.componentInstance;
    component.view.set('ar');
    component.load();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="ar-customer-select"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="ar-payment-term-select"]')).not.toBeNull();
    expect(element.querySelector('input[readonly]')).not.toBeNull();
  });

  it('recognizes an AP source-ready candidate and refreshes without optimistic success', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    await component.recognize(fixture.componentInstance.sourceReady()[0]);
    expect(service.recognizeAp).toHaveBeenCalledWith('source-a');
    expect(component.actionError()).toBeNull();
  });

  it('shows the deterministic AP blocked state when recognition fails', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    vi.mocked(service.recognizeAp).mockRejectedValueOnce(new HttpErrorResponse({ status: 400, error: { code: 'payment_term_not_configured' } }));
    await component.recognize(component.sourceReady()[0]);
    expect(component.actionError()).toContain('Payment Term is required');
  });

  it('creates manual AR only with a selected Payment Term and server-derived due date', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    component.view.set('ar');
    component.load();
    await fixture.whenStable();
    component.setAr('customerId', 'customer-a');
    component.setAr('paymentTermId', 'term-a');
    component.setAr('documentDate', '2026-08-01');
    component.setAr('amount', 1250);
    expect(component.canCreateAr()).toBe(true);
    expect(component.derivedArDueDate()).toBe('2026-08-31');
    await component.createReceivable();
    expect(service.createManualReceivable).toHaveBeenCalledWith(expect.objectContaining({ customerId: 'customer-a', paymentTermId: 'term-a', dueDate: '2026-08-31', amount: 1250 }));
  });

  it('creates Payment and Receipt through configured selector-backed flows', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    component.view.set('settlements');
    component.load();
    await fixture.whenStable();
    component.setSettlement('partyId', 'supplier-a');
    component.setSettlement('cashAccountId', 'cash-a');
    component.setSettlement('paymentMethodId', 'method-a');
    component.setSettlement('amount', 500);
    component.setSettlement('externalReference', 'PAY-1');
    expect(component.canCreateSettlement()).toBe(true);
    await component.createSettlement();
    expect(service.createPayment).toHaveBeenCalledWith(expect.objectContaining({ partyId: 'supplier-a', cashAccountId: 'cash-a', paymentMethodId: 'method-a', externalReference: 'PAY-1' }));
    component.setSettlement('direction', 'Receipt');
    component.setSettlement('partyId', 'customer-a');
    component.setSettlement('externalReference', 'REC-1');
    await component.createSettlement();
    expect(service.createReceipt).toHaveBeenCalledWith(expect.objectContaining({ partyId: 'customer-a', externalReference: 'REC-1' }));
  });

  it('renders lifecycle actions according to Required, NotRequired, Approved, and Posted states', () => {
    const component = fixture.componentInstance;
    component.view.set('settlements');
    component.loading.set(false);
    component.payments.set([
      { ...postedPayment, id: 'submitted-required', status: 'Submitted', approvalRequirement: 'Required' },
      { ...postedPayment, id: 'submitted-direct', status: 'Submitted', approvalRequirement: 'NotRequired' },
      { ...postedPayment, id: 'approved', status: 'Approved', approvalRequirement: 'Required' },
      postedPayment,
    ]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const menuItems = (id: string): string[] => {
      const row = element.querySelector(`tr[data-row-id="${id}"]`);
      (row?.querySelector('.grid-row-menu-trigger') as HTMLButtonElement).click();
      fixture.detectChanges();
      return Array.from(row?.querySelectorAll('[role="menuitem"]') ?? []).map(item => item.textContent?.trim() ?? '');
    };
    expect(menuItems('submitted-required')).toContain('Approve');
    expect(menuItems('submitted-direct')).toContain('Post');
    expect(menuItems('approved')).toContain('Post');
    expect(menuItems('payment-a')).toContain('Reverse');
  });

  it('creates a partial compatible allocation and exposes explicit allocation reversal', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    component.view.set('settlements');
    component.loading.set(false);
    component.payments.set([postedPayment]);
    component.apItems.set([openItem]);
    component.setAllocation('documentId', 'payment-a');
    component.setAllocation('itemId', 'open-item-a');
    component.setAllocation('amount', 250);
    expect(component.compatibleAllocationItems()).toHaveLength(1);
    await component.createAllocation();
    expect(service.createAllocation).toHaveBeenCalledWith(expect.objectContaining({ settlementDocumentId: 'payment-a', openItemId: 'open-item-a', amount: 250 }));
    await component.reverseAllocation(allocation);
    expect(service.reverseAllocation).toHaveBeenCalledWith('allocation-a', 'AQ==', expect.any(String));
  });

  it('maps a control-account posting failure to a human-readable error', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    vi.mocked(service.postSettlement).mockRejectedValueOnce(new HttpErrorResponse({ status: 400, error: { code: 'posting_rule_control_account_mismatch' } }));
    await component.settlementAction(postedPayment, 'post');
    expect(component.actionError()).toContain('historical control account');
  });

  it('derives the initial and reset transaction currency from the selected Company', () => {
    const component = fixture.componentInstance;
    expect(component.arDraft.currencyCode).toBe('SAR');
    expect(component.settlementDraft.currencyCode).toBe('SAR');
    component.setAr('currencyCode', 'USD');
    component.setSettlement('currencyCode', 'USD');
    component.selectCompany('company-a');
    expect(component.arDraft.currencyCode).toBe('SAR');
    expect(component.settlementDraft.currencyCode).toBe('SAR');
    expect(component.requiresArFx()).toBe(false);
    expect(component.requiresSettlementFx()).toBe(false);
  });

  it('resolves exact MESP-120 evidence for non-functional Manual AR and submits all fields', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    component.view.set('ar');
    component.load();
    await fixture.whenStable();
    component.setAr('customerId', 'customer-a');
    component.setAr('paymentTermId', 'term-a');
    component.setAr('documentDate', '2026-08-01');
    component.setAr('currencyCode', 'USD');
    component.setAr('amount', 100);
    expect(component.exchangeRateOptions('ar')).toHaveLength(1);
    component.selectExchangeRate('ar', 'rate-usd-sar');
    await fixture.whenStable();
    expect(component.arExchangeRateReference()?.versionId).toBe('rate-version-3');
    expect(component.canCreateAr()).toBe(true);
    await component.createReceivable();
    expect(service.createManualReceivable).toHaveBeenCalledWith(expect.objectContaining({
      currencyCode: 'USD', exchangeRate: 3.75, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3,
    }));
  });

  it('refreshes stale evidence on document-date change and submits it for Payment and Receipt', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    const masterData = TestBed.inject(MasterDataService) as unknown as { referenceExchangeRate: ReturnType<typeof vi.fn> };
    component.view.set('settlements');
    component.load();
    await fixture.whenStable();
    component.setSettlement('partyId', 'supplier-a');
    component.setSettlement('cashAccountId', 'cash-a');
    component.setSettlement('paymentMethodId', 'method-a');
    component.setSettlement('currencyCode', 'USD');
    component.setSettlement('amount', 100);
    component.setSettlement('externalReference', 'PAY-FX-1');
    component.selectExchangeRate('settlement', 'rate-usd-sar');
    await fixture.whenStable();
    component.setSettlement('documentDate', '2026-08-02');
    await fixture.whenStable();
    expect(masterData.referenceExchangeRate).toHaveBeenLastCalledWith('rate-usd-sar', '2026-08-02');
    expect(component.settlementExchangeRateReference()?.effectiveOn).toBe('2026-08-02');
    expect(component.canCreateSettlement()).toBe(true);
    await component.createSettlement();
    expect(service.createPayment).toHaveBeenCalledWith(expect.objectContaining({ exchangeRate: 3.75, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3 }));

    component.setSettlement('direction', 'Receipt');
    component.setSettlement('partyId', 'customer-a');
    component.setSettlement('externalReference', 'REC-FX-1');
    await component.createSettlement();
    expect(service.createReceipt).toHaveBeenCalledWith(expect.objectContaining({ exchangeRate: 3.75, exchangeRateId: 'rate-usd-sar', exchangeRateVersionId: 'rate-version-3', exchangeRateVersionNumber: 3 }));
  });

  it('clears stale rate evidence when currency changes and explains missing FX evidence bilingually', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    component.view.set('settlements');
    component.load();
    await fixture.whenStable();
    component.setSettlement('partyId', 'supplier-a');
    component.setSettlement('cashAccountId', 'cash-a');
    component.setSettlement('paymentMethodId', 'method-a');
    component.setSettlement('currencyCode', 'USD');
    component.selectExchangeRate('settlement', 'rate-usd-sar');
    await fixture.whenStable();
    expect(component.settlementExchangeRateReference()).not.toBeNull();
    component.setSettlement('currencyCode', 'SAR');
    expect(component.settlementExchangeRateReference()).toBeNull();
    expect(component.settlementExchangeRateId()).toBe('');

    component.setSettlement('amount', 100);
    component.setSettlement('externalReference', 'FX-FUNCTIONAL');
    vi.mocked(service.createPayment).mockRejectedValueOnce(new HttpErrorResponse({ status: 400, error: { code: 'fx_settlement_not_configured' } }));
    await component.createSettlement();
    expect(component.actionError()).toContain('Allocation across different functional values');
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    expect(component['fxErrorMessage'](new HttpErrorResponse({ status: 400, error: { code: 'exact_exchange_rate_evidence_required' } }))).toContain('اختر');
    language.setLanguage('en');
  });
});
