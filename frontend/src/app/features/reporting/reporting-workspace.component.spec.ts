import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ReportingService } from './reporting.service';
import { ReportingWorkspaceComponent } from './reporting-workspace.component';

describe('ReportingWorkspaceComponent', () => {
  let fixture: ComponentFixture<ReportingWorkspaceComponent>;

  const definitions = [
    { code: 'finance.trial-balance', name: 'Trial balance', arabicName: '\u0645\u064a\u0632\u0627\u0646 \u0627\u0644\u0645\u0631\u0627\u062c\u0639\u0629', domain: 'Finance', definitionVersion: '1.0', sourceOwnership: 'Finance owns posted truth.', reconciliationPath: 'Finance reconciliation', allowedFilters: ['company'], exportEnabled: true, schedulingEnabled: true, requiresCompany: true, pendingDecision: false },
    { code: 'finance.cash-movement', name: 'Cash movement', arabicName: '\u062d\u0631\u0643\u0629 \u0627\u0644\u0646\u0642\u062f', domain: 'Finance', definitionVersion: '1.0', sourceOwnership: 'Decision pending.', reconciliationPath: 'Finance evidence', allowedFilters: ['company'], exportEnabled: true, schedulingEnabled: false, requiresCompany: true, pendingDecision: true, pendingDecisionCode: 'FIN-OD-09' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReportingWorkspaceComponent],
      providers: [
        { provide: ContextService, useValue: { currentOperationalContext: () => null, entry: () => null } },
        {
        provide: ReportingService,
        useValue: {
          catalogue: vi.fn(() => of(definitions)),
          schedules: vi.fn(() => of([])),
          execute: vi.fn(() => of({ rows: [], totalRows: 0, metadata: {}, columns: [], definition: definitions[0] })),
          artifactUrl: vi.fn(() => '/artifact'),
        },
        },
      ],
    });
    fixture = TestBed.createComponent(ReportingWorkspaceComponent);
    fixture.detectChanges();
  });

  it('renders the source catalogue and explicit pending decision branch', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Trial balance');
    expect(element.textContent).toContain('Cash movement');
    expect(element.textContent).toContain('source signal');
  });

  it('leaves Company and Branch selection to the server-owned operational context', () => {
    fixture.componentInstance.run();
    const service = TestBed.inject(ReportingService) as unknown as { execute: ReturnType<typeof vi.fn> };
    expect(service.execute).toHaveBeenCalledWith('finance.trial-balance', expect.not.objectContaining({ companyId: expect.anything() }));
  });

  it('renders the Arabic RTL journey through the shared language service', () => {
    TestBed.inject(LanguageService).setLanguage('ar');
    fixture.detectChanges();
    const section = fixture.nativeElement.querySelector('.reporting-page') as HTMLElement;
    expect(section.getAttribute('dir')).toBe('rtl');
    expect(section.textContent).toContain('خط واضح يعود إلى المصدر.');
  });
});
