import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceWorkspaceComponent } from './finance-workspace.component';

describe('FinanceWorkspaceComponent', () => {
  let fixture: ComponentFixture<FinanceWorkspaceComponent>;

  beforeEach(() => {
    const empty = () => of([]);
    TestBed.configureTestingModule({
      imports: [FinanceWorkspaceComponent],
      providers: [provideRouter([]), {
        provide: FinanceService,
        useValue: {
          companies: () => of([{ tenantId: 'tenant-a', companyId: 'company-a', companyName: 'Alpha Company', functionalCurrencyCode: 'SAR', branchId: null, isActive: true }]),
          accounts: empty,
          calendars: () => of([{ id: 'calendar-a', companyId: 'company-a', name: 'FY 2026', functionalCurrencyCode: 'SAR', lifecycle: 'Active', version: 'AQ==' }]),
          years: empty,
          periods: empty,
          rules: empty,
          journals: empty,
          gl: empty,
          handoffs: empty,
        },
      }],
    });
    fixture = TestBed.createComponent(FinanceWorkspaceComponent);
    fixture.detectChanges();
  });

  it('renders the server-authorized Company context and GL foundation tabs', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="finance-workspace"]')).not.toBeNull();
    expect(element.querySelector('select')?.textContent).toContain('Alpha Company');
    expect(element.querySelector('h1')?.textContent).toContain('Company books');
    expect(element.querySelector('app-tabs [role="tablist"]')?.textContent).toContain('Chart of accounts');
    expect(element.querySelector('app-tabs [role="tablist"]')?.textContent).toContain('GL inquiry');
  });

  it('keeps the Finance view RTL-safe when Arabic is selected', () => {
    const language = TestBed.inject(LanguageService);
    language.setLanguage('ar');
    fixture.detectChanges();
    expect(document.documentElement.dir).toBe('rtl');
    expect(language.language()).toBe('ar');
    language.setLanguage('en');
  });
});
