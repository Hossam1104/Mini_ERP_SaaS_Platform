import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceCloseWorkspaceComponent } from './finance-close-workspace.component';

describe('FinanceCloseWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceCloseWorkspaceComponent>;

  const companies = [{
    tenantId: 'tenant-a',
    companyId: 'company-a',
    companyName: 'Alpha Company',
    functionalCurrencyCode: 'SAR',
    branchId: null,
    isActive: true,
  }];

  const calendar = { id: 'calendar-a', companyId: 'company-a', name: 'Fiscal Calendar', functionalCurrencyCode: 'SAR', lifecycle: 'Active', version: 'AQ==' };
  const year = { id: 'year-a', calendarId: 'calendar-a', yearNumber: 2026, startDate: '2026-01-01', endDate: '2026-12-31', state: 'Open' };
  const period = { id: 'period-a', fiscalYearId: 'year-a', sequence: 8, code: '2026-08', englishName: 'August', arabicName: null, startDate: '2026-08-01', endDate: '2026-08-31', state: 'Open', version: 'AQ==' };

  const readiness = {
    periodId: 'period-a', fiscalYearId: 'year-a', companyId: 'company-a', status: 'Ready',
    checks: [{ code: 'revaluation_policy', status: 'Ready', message: 'All foreign exposure is revalued.' }],
    snapshotFingerprint: 'fingerprint-abc', evaluatedAt: '2026-08-27T00:00:00Z', periodVersion: 'AQ==',
  };

  const closeReconciliation = {
    companyId: 'company-a', periodId: 'period-a', asOfDate: '2026-08-31', overallStatus: 'Reconciled',
    items: [{ companyId: 'company-a', asOfDate: '2026-08-31', scope: 'Cash', status: 'Reconciled', expectedAmount: 100, actualAmount: 100, difference: 0, sourceReference: 'CASH-1', detail: 'Matched', hasDurableEvidence: true }],
    closeHistory: [], yearEndRuns: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FinanceCloseWorkspaceComponent],
      providers: [
        provideRouter([]),
        {
          provide: FinanceService,
          useValue: {
            companies: () => of(companies),
            calendars: vi.fn(() => of([calendar])),
            years: vi.fn(() => of([year])),
            periods: vi.fn(() => of([period])),
            closeReadiness: vi.fn(() => of(readiness)),
            closeRuns: vi.fn(() => of([])),
            closeHistory: vi.fn(() => of([])),
            closeReconciliation: vi.fn(() => of(closeReconciliation)),
            yearEndRuns: vi.fn(() => of([])),
            closePeriod: vi.fn().mockResolvedValue({}),
            reopenPeriod: vi.fn().mockResolvedValue({}),
            calculateYearEnd: vi.fn().mockResolvedValue({}),
            postYearEnd: vi.fn().mockResolvedValue({}),
            reverseYearEnd: vi.fn().mockResolvedValue({}),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(FinanceCloseWorkspaceComponent);
    fixture.detectChanges();
  });

  it('auto-selects the first Company, calendar, year, and period and evaluates readiness', async () => {
    await fixture.whenStable();
    fixture.detectChanges();
    const service = TestBed.inject(FinanceService);
    expect(service.closeReadiness).toHaveBeenCalledWith('company-a', 'period-a');
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Ready');
    expect(element.textContent).toContain('revaluation_policy');
  });

  it('loads close reconciliation evidence scoped to the selected period end date', async () => {
    await fixture.whenStable();
    fixture.detectChanges();
    const service = TestBed.inject(FinanceService);
    expect(service.closeReconciliation).toHaveBeenCalledWith('company-a', '2026-08-31', 'period-a');
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Reconciled');
  });

  it('disables Close when readiness is Blocked and enables it when Ready', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService) as unknown as { closeReadiness: ReturnType<typeof vi.fn> };
    await fixture.whenStable();
    fixture.detectChanges();
    let closeButton = (fixture.nativeElement as HTMLElement).querySelectorAll('.actions button')[1] as HTMLButtonElement;
    expect(closeButton.disabled).toBe(false);

    service.closeReadiness.mockReturnValueOnce(of({ ...readiness, status: 'Blocked', checks: [{ code: 'revaluation_policy', status: 'Blocked', message: 'Foreign exposure outstanding.' }] }));
    component.evaluate();
    await fixture.whenStable();
    fixture.detectChanges();
    closeButton = (fixture.nativeElement as HTMLElement).querySelectorAll('.actions button')[1] as HTMLButtonElement;
    expect(closeButton.disabled).toBe(true);
  });

  it('closes the selected period with the current version and a durable reason', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService);
    await fixture.whenStable();
    component.reason = 'August close approved after readiness evaluation';
    await component.close();
    expect(service.closePeriod).toHaveBeenCalledWith('company-a', 'period-a', 'AQ==', 'August close approved after readiness evaluation');
  });

  it('surfaces a deterministic error code when a close mutation fails', async () => {
    const component = fixture.componentInstance;
    const service = TestBed.inject(FinanceService) as unknown as { closePeriod: ReturnType<typeof vi.fn> };
    await fixture.whenStable();
    service.closePeriod.mockRejectedValueOnce({ error: { code: 'close_readiness_blocked' } });
    await component.close();
    expect(component.error()).toBe('close_readiness_blocked');
  });

  it('keeps close navigation and copy RTL-safe for Arabic', async () => {
    await fixture.whenStable();
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(language.language()).toBe('ar');
    language.setLanguage('en');
  });
});
