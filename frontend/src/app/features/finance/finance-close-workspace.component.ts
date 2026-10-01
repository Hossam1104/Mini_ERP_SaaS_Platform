import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceCloseReadiness, FinanceCompany, FinanceFiscalCalendar, FinanceFiscalPeriod, FinanceFiscalYear, FinancePeriodCloseRun, FinancePeriodHistory, FinanceYearEndRun, FinanceCloseReconciliation } from './finance.model';

type Bilingual = { en: string; ar: string };
const copy: Record<string, Bilingual> = {
  kicker: { en: 'Finance control / close', ar: 'الرقابة المالية / الإقفال' },
  title: { en: 'Close periods with evidence', ar: 'إقفال الفترات مع الأدلة' },
  lead: { en: 'Evaluate the real posting, subledger, lineage, and monetary evidence before creating a durable close run.', ar: 'قيّم الترحيل الفعلي والأستاذ الفرعي وسلسلة المصدر والأدلة النقدية قبل إنشاء عملية إقفال دائمة.' },
  company: { en: 'Authorized Company', ar: 'الشركة المصرح بها' },
  year: { en: 'Fiscal year', ar: 'السنة المالية' },
  period: { en: 'Fiscal period', ar: 'الفترة المالية' },
  check: { en: 'Evaluate readiness', ar: 'تقييم الجاهزية' },
  close: { en: 'Close period', ar: 'إقفال الفترة' },
  reopen: { en: 'Reopen period', ar: 'إعادة فتح الفترة' },
  calculate: { en: 'Calculate year-end', ar: 'احتساب إقفال السنة' },
  post: { en: 'Post year-end', ar: 'ترحيل إقفال السنة' },
  reverse: { en: 'Reverse year-end', ar: 'عكس إقفال السنة' },
  reason: { en: 'Reason / evidence note', ar: 'السبب / ملاحظة الدليل' },
  readiness: { en: 'Readiness', ar: 'الجاهزية' },
  history: { en: 'Close history', ar: 'سجل الإقفال' },
  reconciliation: { en: 'Reconciliation', ar: 'التسوية' },
  yearEnd: { en: 'Year-end runs', ar: 'عمليات إقفال السنة' },
  empty: { en: 'Select a company, year, and period to inspect close evidence.', ar: 'اختر الشركة والسنة والفترة لفحص أدلة الإقفال.' },
  unavailable: { en: 'Finance close evidence is temporarily unavailable.', ar: 'أدلة الإقفال المالي غير متاحة مؤقتاً.' },
};

@Component({
  selector: 'app-finance-close-workspace',
  standalone: true,
  imports: [PageHeaderComponent, CommonModule, FormsModule],
  template: `
    <section class="close-page" [attr.dir]="language.language() === 'ar' ? 'rtl' : 'ltr'">
      <app-page-header class="page-header"><div page-header-copy><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lead">{{ text('lead') }}</p></div>
          <div page-header-actions><a class="button" href="/app/finance/reports">{{ text('reconciliation') }}</a>
          </div>
        </app-page-header>
      <section class="controls">
        <label><span>{{ text('company') }}</span><select [ngModel]="companyId()" (ngModelChange)="selectCompany($event)"><option value="">—</option>@for (company of companies(); track company.companyId) { <option [value]="company.companyId">{{ company.companyName }} · {{ company.functionalCurrencyCode }}</option> }</select></label>
        <label><span>{{ text('year') }}</span><select [ngModel]="yearId()" (ngModelChange)="selectYear($event)"><option value="">—</option>@for (year of years(); track year.id) { <option [value]="year.id">{{ year.yearNumber }} · {{ year.state }}</option> }</select></label>
        <label><span>{{ text('period') }}</span><select [ngModel]="periodId()" (ngModelChange)="selectPeriod($event)"><option value="">—</option>@for (period of periods(); track period.id) { <option [value]="period.id">#{{ period.sequence }} · {{ period.code }} · {{ period.state }}</option> }</select></label>
      </section>
      @if (error()) { <p class="error">{{ error() }}</p> }
      @if (!companyId() || !periodId()) { <section class="empty"><strong>{{ text('empty') }}</strong></section> }
      @if (periodId()) {
        <section class="actions"><button class="button button--primary" type="button" (click)="evaluate()" [disabled]="busy()">{{ text('check') }}</button><button class="button" type="button" (click)="close()" [disabled]="busy() || !readiness() || readiness()?.status === 'Blocked' || selectedPeriod()?.state === 'Closed'">{{ text('close') }}</button><button class="button" type="button" (click)="reopen()" [disabled]="busy() || selectedPeriod()?.state !== 'Closed'">{{ text('reopen') }}</button><input class="reason" [(ngModel)]="reason" [placeholder]="text('reason')" maxlength="2048" /></section>
        <section class="grid">
          <article class="panel"><div class="panel-head"><div><p class="eyebrow">{{ text('readiness') }}</p><h2>{{ readiness()?.status || '—' }}</h2></div><span class="fingerprint">{{ readiness()?.snapshotFingerprint?.slice(0, 12) || '—' }}</span></div>@if (!readiness()) { <p class="muted">{{ text('empty') }}</p> } @else { @for (check of readiness()!.checks; track check.code) { <div class="check"><span [class]="'dot ' + check.status.toLowerCase()"></span><div><strong>{{ check.code }}</strong><small>{{ check.message }}</small></div></div> } }</article>
          <article class="panel"><div class="panel-head"><div><p class="eyebrow">{{ text('history') }}</p><h2>{{ closeRuns().length }}</h2></div></div>@if (closeRuns().length === 0) { <p class="muted">—</p> } @else { @for (run of closeRuns(); track run.id) { <div class="history-row"><strong>#{{ run.sequence }} · {{ run.status }}</strong><small>{{ run.createdAt | date:'medium' }} · {{ run.reason }}</small></div> } }</article>
          <article class="panel"><div class="panel-head"><div><p class="eyebrow">{{ text('reconciliation') }}</p><h2>{{ reconciliation()?.overallStatus || '—' }}</h2></div></div>@for (item of reconciliation()?.items ?? []; track item.scope + item.sourceReference) { <div class="history-row"><strong>{{ item.scope }} · {{ item.status }}</strong><small>{{ item.detail }}</small></div> } @if (!(reconciliation()?.items?.length)) { <p class="muted">—</p> }</article>
        </section>
        @if (yearId()) { <section class="panel year-end"><div class="panel-head"><div><p class="eyebrow">{{ text('yearEnd') }}</p><h2>{{ yearEndRuns().length }}</h2></div><button class="button" type="button" (click)="calculateYearEnd()" [disabled]="busy() || !selectedYear()">{{ text('calculate') }}</button></div>@for (run of yearEndRuns(); track run.id) { <div class="history-row"><strong>{{ run.status }} · {{ run.retainedEarningsAccountCode || '—' }}</strong><small>{{ run.asOfDate }} · {{ run.lines.length }} lines</small>@if (run.status === 'Calculated') { <button class="button button--small" type="button" (click)="postYearEnd(run)" [disabled]="busy()">{{ text('post') }}</button> } @if (run.status === 'Posted') { <button class="button button--small" type="button" (click)="reverseYearEnd(run)" [disabled]="busy()">{{ text('reverse') }}</button> }</div> }</section> }
      }
    </section>
  `,
  styles: [`:host{display:block}.close-page{display:grid;gap:1.1rem}.eyebrow{margin:0;color:var(--teal);font-size:.72rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}.lead{max-width:760px;color:var(--muted);line-height:1.6}.controls,.actions{display:flex;gap:.85rem;align-items:end;flex-wrap:wrap;padding:1rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}label{display:grid;gap:.35rem;min-width:190px;flex:1}label span{color:var(--muted);font-size:.72rem;font-weight:800;text-transform:uppercase;letter-spacing:.06em}.reason{min-width:260px;flex:2}.grid{display:grid;grid-template-columns:1.35fr 1fr 1fr;gap:1rem}.panel,.empty{padding:1.15rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}.empty{min-height:180px;display:grid;place-items:center;text-align:center;color:var(--muted)}.panel-head{display:flex;justify-content:space-between;gap:.7rem;align-items:start;margin-bottom:1rem}.panel h2{margin:.3rem 0 0;font-size:1.35rem}.fingerprint{font:12px ui-monospace;color:var(--muted)}.check,.history-row{display:flex;gap:.65rem;align-items:start;padding:.7rem 0;border-bottom:1px solid var(--line)}.check:last-child,.history-row:last-child{border-bottom:0}.check small,.history-row small{display:block;margin-top:.22rem;color:var(--muted);line-height:1.45}.dot{width:.62rem;height:.62rem;margin-top:.35rem;border-radius:50%;background:#9b8060;flex:0 0 auto}.dot.ready{background:#25866c}.dot.warning{background:#c58b31}.dot.blocked{background:#be4e4e}.history-row{flex-wrap:wrap}.history-row button{margin-inline-start:auto}.muted{color:var(--muted)}.error{padding:.75rem 1rem;border:1px solid #d98c8c;border-radius:9px;color:#9d3f3f;background:#fff4f4}.year-end{display:grid;gap:.1rem}@media(max-width:900px){.grid{grid-template-columns:1fr}}`],
})
export class FinanceCloseWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(FinanceService);
  readonly companies = signal<FinanceCompany[]>([]);
  readonly calendars = signal<FinanceFiscalCalendar[]>([]);
  readonly years = signal<FinanceFiscalYear[]>([]);
  readonly periods = signal<FinanceFiscalPeriod[]>([]);
  readonly closeRuns = signal<FinancePeriodCloseRun[]>([]);
  readonly history = signal<FinancePeriodHistory[]>([]);
  readonly yearEndRuns = signal<FinanceYearEndRun[]>([]);
  readonly reconciliation = signal<FinanceCloseReconciliation | null>(null);
  readonly readiness = signal<FinanceCloseReadiness | null>(null);
  readonly companyId = signal('');
  readonly yearId = signal('');
  readonly periodId = signal('');
  readonly selectedCompany = computed(() => this.companies().find((item) => item.companyId === this.companyId()) ?? null);
  readonly selectedYear = computed(() => this.years().find((item) => item.id === this.yearId()) ?? null);
  readonly selectedPeriod = computed(() => this.periods().find((item) => item.id === this.periodId()) ?? null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  reason = '';

  ngOnInit(): void { this.service.companies().subscribe({ next: (items) => { this.companies.set(items); if (items[0]) this.selectCompany(items[0].companyId); }, error: () => this.error.set(this.text('unavailable')) }); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  selectCompany(id: string): void { this.companyId.set(id); this.yearId.set(''); this.periodId.set(''); this.readiness.set(null); this.service.calendars(id).subscribe({ next: (calendars) => { this.calendars.set(calendars); if (calendars[0]) this.service.years(calendars[0].id).subscribe({ next: (years) => { this.years.set(years); if (years[0]) this.selectYear(years[0].id); } }); } }); }
  selectYear(id: string): void { this.yearId.set(id); this.periodId.set(''); this.readiness.set(null); this.service.periods(id).subscribe({ next: (periods) => { this.periods.set(periods); if (periods[0]) this.selectPeriod(periods[0].id); } }); }
  selectPeriod(id: string): void { this.periodId.set(id); this.evaluate(); this.loadEvidence(); }
  evaluate(): void { if (!this.companyId() || !this.periodId()) return; this.service.closeReadiness(this.companyId(), this.periodId()).subscribe({ next: (item) => this.readiness.set(item), error: (error) => this.error.set(this.errorMessage(error)) }); }
  loadEvidence(): void { if (!this.companyId() || !this.periodId()) return; const date = this.selectedPeriod()?.endDate ?? this.today(); this.service.closeRuns(this.companyId(), this.periodId()).subscribe({ next: (items) => this.closeRuns.set(items) }); this.service.closeHistory(this.companyId(), this.periodId()).subscribe({ next: (items) => this.history.set(items) }); this.service.closeReconciliation(this.companyId(), date, this.periodId()).subscribe({ next: (item) => this.reconciliation.set(item) }); if (this.yearId()) this.service.yearEndRuns(this.companyId(), this.yearId()).subscribe({ next: (items) => this.yearEndRuns.set(items) }); }
  async close(): Promise<void> { const period = this.selectedPeriod(); if (!period) return; await this.run(() => this.service.closePeriod(this.companyId(), period.id, period.version, this.reason || 'Period close approved after readiness evaluation')); }
  async reopen(): Promise<void> { const period = this.selectedPeriod(); if (!period) return; await this.run(() => this.service.reopenPeriod(this.companyId(), period.id, period.version, this.reason || 'Period reopen approved for controlled correction')); }
  async calculateYearEnd(): Promise<void> { const year = this.selectedYear(); if (!year) return; await this.run(() => this.service.calculateYearEnd({ companyId: this.companyId(), fiscalYearId: year.id, asOfDate: year.endDate, reason: this.reason || 'Year-end calculation approved after all periods closed' })); }
  async postYearEnd(run: FinanceYearEndRun): Promise<void> { await this.run(() => this.service.postYearEnd(this.companyId(), run.id, run.version, this.reason || 'Year-end posting approved')); }
  async reverseYearEnd(run: FinanceYearEndRun): Promise<void> { await this.run(() => this.service.reverseYearEnd(this.companyId(), run.id, run.version, this.reason || 'Year-end reversal approved')); }
  private async run(action: () => Promise<unknown>): Promise<void> { this.busy.set(true); this.error.set(null); try { await action(); this.reason = ''; this.refreshAfterMutation(); } catch (error) { this.error.set(this.errorMessage(error)); } finally { this.busy.set(false); } }
  private refreshAfterMutation(): void { const yearId = this.yearId(); if (!yearId) { this.evaluate(); this.loadEvidence(); return; } this.service.periods(yearId).subscribe({ next: (items) => { this.periods.set(items); this.evaluate(); this.loadEvidence(); }, error: (error) => this.error.set(this.errorMessage(error)) }); }
  private errorMessage(error: unknown): string { const value = (error as { error?: { code?: string } })?.error?.code; return value || this.text('unavailable'); }
  private today(): string { return new Date().toISOString().slice(0, 10); }
}
