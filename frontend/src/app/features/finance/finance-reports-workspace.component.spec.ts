import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceReportsWorkspaceComponent } from './finance-reports-workspace.component';

describe('FinanceReportsWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceReportsWorkspaceComponent>;

  const companies = [{
    tenantId: 'tenant-a',
    companyId: 'company-a',
    companyName: 'Alpha Company',
    functionalCurrencyCode: 'SAR',
    branchId: null,
    isActive: true,
  }];

  const trialBalance = {
    companyId: 'company-a', asOfDate: '2026-08-15', fromDate: null, toDate: null,
    rows: [{ accountId: 'account-a', accountCode: '1000', accountName: 'Cash', accountNameArabic: null, accountType: 'Asset', openingBalance: 100, periodDebit: 50, periodCredit: 20, closingBalance: 130 }],
    totalDebit: 50, totalCredit: 20, totalClosingBalance: 130, functionalCurrencyCode: 'SAR', reportingCurrencyCode: null, reportingEvidenceStatus: 'Reconciled',
  };

  const statement = {
    kind: 'ProfitAndLoss', companyId: 'company-a', fromDate: '2026-01-01', toDate: '2026-08-15',
    rows: [{ accountId: 'account-rev', accountCode: '4000', accountName: 'Revenue', accountType: 'Revenue', openingBalance: 0, debit: 0, credit: 500, closingBalance: 500, functionalCurrencyCode: 'SAR' }],
    totalDebit: 0, totalCredit: 500, totalClosingBalance: 500, functionalCurrencyCode: 'SAR', finding: null,
  };

  const closeReconciliation = {
    companyId: 'company-a', periodId: null, asOfDate: '2026-08-15', overallStatus: 'Reconciled',
    items: [{ companyId: 'company-a', asOfDate: '2026-08-15', scope: 'Cash', status: 'Reconciled', expectedAmount: 100, actualAmount: 100, difference: 0, sourceReference: 'CASH-1', detail: 'Matched', hasDurableEvidence: true }],
    closeHistory: [], yearEndRuns: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FinanceReportsWorkspaceComponent],
      providers: [
        provideRouter([]),
        {
          provide: FinanceService,
          useValue: {
            companies: () => of(companies),
            trialBalance: vi.fn(() => of(trialBalance)),
            generalLedger: vi.fn(() => of([])),
            reportAging: vi.fn(() => of([])),
            statement: vi.fn(() => of(statement)),
            closeReconciliation: vi.fn(() => of(closeReconciliation)),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(FinanceReportsWorkspaceComponent);
    fixture.detectChanges();
  });

  it('runs the trial balance report for the first authorized Company on load', () => {
    const service = TestBed.inject(FinanceService);
    expect(service.trialBalance).toHaveBeenCalledWith('company-a', expect.any(String));
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Cash');
  });

  it('exposes a trial-balance export link scoped by Company and as-of date', () => {
    const component = fixture.componentInstance;
    const url = component.exportUrl('trial-balance');
    expect(url).toContain('/api/v1/finance/reports/trial-balance/export');
    expect(url).toContain('companyId=company-a');
    expect(url).toContain('asOfDate=');
  });

  it('exposes profit-loss and balance-sheet export links scoped by Company and date range, not as-of date', () => {
    const component = fixture.componentInstance;
    const pnlUrl = component.exportUrl('profit-loss');
    expect(pnlUrl).toContain('/api/v1/finance/reports/profit-loss/export');
    expect(pnlUrl).toContain('fromDate=');
    expect(pnlUrl).toContain('toDate=');
    expect(pnlUrl).not.toContain('asOfDate=');

    const bsUrl = component.exportUrl('balance-sheet');
    expect(bsUrl).toContain('/api/v1/finance/reports/balance-sheet/export');
    expect(bsUrl).toContain('fromDate=');
    expect(bsUrl).toContain('toDate=');
  });

  it('exposes a close reconciliation export link against the reconciliation route, not the reports route', () => {
    const component = fixture.componentInstance;
    const url = component.exportUrl('reconciliation');
    expect(url).toBe(`/api/v1/finance/reconciliation/close/export?companyId=company-a&asOfDate=${component.asOfDate}`);
  });

  it('renders an export action for the profit and loss and balance sheet statement panels', async () => {
    const component = fixture.componentInstance;
    component.active.set('pnl');
    component.run();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const link = element.querySelector('.panel-head a.button') as HTMLAnchorElement | null;
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toContain('/reports/profit-loss/export');
  });

  it('renders an export action for the close reconciliation panel', async () => {
    const component = fixture.componentInstance;
    component.active.set('reconciliation');
    component.run();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const link = element.querySelector('.panel-head a.button') as HTMLAnchorElement | null;
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toContain('/reconciliation/close/export');
  });

  it('keeps report navigation and copy RTL-safe for Arabic', () => {
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(language.language()).toBe('ar');
    language.setLanguage('en');
  });
});
