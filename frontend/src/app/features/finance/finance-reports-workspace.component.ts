import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../../core/i18n/language.service';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { TabsComponent } from '../../shared/ui/tabs.component';
import type { AppTab } from '../../shared/ui/tabs.component';
import { FinanceService } from './finance.service';
import { FinanceAgingReportRow, FinanceCloseReconciliation, FinanceCompany, FinanceGeneralLedgerLine, FinanceReconciliationView, FinanceStatementReport, FinanceStatementRow, FinanceTrialBalanceReport, FinanceTrialBalanceRow } from './finance.model';

type ReportTab = 'trial' | 'ledger' | 'ap' | 'ar' | 'pnl' | 'bs' | 'reconciliation';
type Bilingual = { en: string; ar: string };
const copy: Record<string, Bilingual> = {
  kicker: { en: 'Finance evidence / reports', ar: 'أدلة المالية / التقارير' },
  title: { en: 'Core reports from posted facts', ar: 'التقارير الأساسية من الحقائق المرحلة' },
  lead: { en: 'Trial balance, GL, aging, statements, and close reconciliation share the same tenant-scoped posted-journal evidence.', ar: 'يتشارك ميزان المراجعة والأستاذ العام وأعمار الديون والقوائم وتسوية الإقفال أدلة القيود المرحلة ضمن نطاق المستأجر.' },
  company: { en: 'Authorized Company', ar: 'الشركة المصرح بها' },
  asOf: { en: 'As of', ar: 'كما في' },
  from: { en: 'From', ar: 'من' },
  to: { en: 'To', ar: 'إلى' },
  run: { en: 'Run report', ar: 'تشغيل التقرير' },
  export: { en: 'CSV export', ar: 'تصدير CSV' },
  trial: { en: 'Trial balance', ar: 'ميزان المراجعة' },
  ledger: { en: 'General ledger', ar: 'الأستاذ العام' },
  ap: { en: 'AP aging', ar: 'أعمار الدائنين' },
  ar: { en: 'AR aging', ar: 'أعمار العملاء' },
  pnl: { en: 'Profit & loss', ar: 'الأرباح والخسائر' },
  bs: { en: 'Balance sheet', ar: 'الميزانية العمومية' },
  reconciliation: { en: 'Close reconciliation', ar: 'تسوية الإقفال' },
  empty: { en: 'Choose a company and run a report.', ar: 'اختر الشركة وشغّل التقرير.' },
  unavailable: { en: 'The Finance report is temporarily unavailable.', ar: 'تقرير المالية غير متاح مؤقتاً.' },
};

@Component({
  selector: 'app-finance-reports-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, TabsComponent, CommonModule, FormsModule],
  template: `
    <section class="reports-page" [attr.dir]="language.language() === 'ar' ? 'rtl' : 'ltr'">
      <app-page-header class="page-header"><div page-header-copy><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lead">{{ text('lead') }}</p></div>
          <div page-header-actions><a class="button" href="/app/finance/close">{{ text('reconciliation') }}</a>
          </div>
        </app-page-header>
      <section class="toolbar"><label><span>{{ text('company') }}</span><select [ngModel]="companyId()" (ngModelChange)="companyId.set($event)"><option value="">—</option>@for (company of companies(); track company.companyId) { <option [value]="company.companyId">{{ company.companyName }} · {{ company.functionalCurrencyCode }}</option> }</select></label><label><span>{{ text('asOf') }}</span><input type="date" [(ngModel)]="asOfDate" /></label><label><span>{{ text('from') }}</span><input type="date" [(ngModel)]="fromDate" /></label><label><span>{{ text('to') }}</span><input type="date" [(ngModel)]="toDate" /></label><button class="button button--primary" type="button" (click)="run()" [disabled]="busy()">{{ text('run') }}</button></section>
      <app-tabs [tabs]="reportTabs()" [selected]="active()" ariaLabel="Finance reports" (selectedChange)="selectTab($event)" />
      @if (error()) { <p class="error">{{ error() }}</p> }
      @if (!companyId()) { <section class="empty">{{ text('empty') }}</section> }
      @if (active() === 'trial' && trial()) { <section class="panel"><div class="panel-head"><h2>{{ text('trial') }}</h2><a class="button button--small" [href]="exportUrl('trial-balance')">{{ text('export') }}</a></div><p class="summary">{{ trial()!.functionalCurrencyCode }} · Debit {{ trial()!.totalDebit | number:'1.2-2' }} · Credit {{ trial()!.totalCredit | number:'1.2-2' }}</p><app-data-grid [rows]="trial()!.rows" [columns]="trialColumns" [clientPaging]="false" [showPager]="false" caption="Trial balance" /></section> }
      @if (active() === 'ledger' && ledger()) { <section class="panel"><div class="panel-head"><h2>{{ text('ledger') }}</h2><a class="button button--small" [href]="exportUrl('general-ledger')">{{ text('export') }}</a></div><app-data-grid [rows]="ledger()!" [columns]="ledgerColumns" [clientPaging]="false" [showPager]="false" caption="General ledger" /></section> }
      @if ((active() === 'ap' || active() === 'ar') && aging()) { <section class="panel"><div class="panel-head"><h2>{{ text(active()) }}</h2><a class="button button--small" [href]="exportUrl(active() === 'ap' ? 'ap-aging' : 'ar-aging')">{{ text('export') }}</a></div><app-data-grid [rows]="aging()!" [columns]="agingColumns" [clientPaging]="false" [showPager]="false" caption="Aging report" /></section> }
      @if ((active() === 'pnl' || active() === 'bs') && statement()) { <section class="panel"><div class="panel-head"><h2>{{ text(active()) }}</h2><a class="button button--small" [href]="exportUrl(active() === 'pnl' ? 'profit-loss' : 'balance-sheet')">{{ text('export') }}</a></div><p class="summary">{{ statement()!.functionalCurrencyCode }} · Debit {{ statement()!.totalDebit | number:'1.2-2' }} · Credit {{ statement()!.totalCredit | number:'1.2-2' }}</p><app-data-grid [rows]="statement()!.rows" [columns]="statementColumns" [clientPaging]="false" [showPager]="false" caption="Financial statement" /></section> }
      @if (active() === 'reconciliation' && reconciliation()) { <section class="panel"><div class="panel-head"><h2>{{ reconciliation()!.overallStatus }}</h2><a class="button button--small" [href]="exportUrl('reconciliation')">{{ text('export') }}</a></div><app-data-grid [rows]="reconciliation()!.items" [columns]="reconciliationColumns" [clientPaging]="false" [showPager]="false" caption="Close reconciliation" /></section> }
      @if (companyId() && !resultAvailable()) { <section class="empty">{{ text('empty') }}</section> }
    </section>
  `,
  styles: [`:host{display:block}.reports-page{display:grid;gap:1.1rem}.eyebrow{margin:0;color:var(--teal);font-size:.72rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}.lead{max-width:760px;color:var(--muted);line-height:1.6}.toolbar{display:flex;align-items:end;gap:.75rem;flex-wrap:wrap;padding:1rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}label{display:grid;gap:.35rem;min-width:170px;flex:1}label span{color:var(--muted);font-size:.72rem;font-weight:800;letter-spacing:.06em;text-transform:uppercase}.panel,.empty{padding:1.15rem;border:1px solid var(--line);border-radius:14px;background:var(--surface);box-shadow:var(--shadow-sm)}.empty{min-height:180px;display:grid;place-items:center;color:var(--muted)}.panel-head{display:flex;justify-content:space-between;align-items:center;gap:1rem;margin-bottom:.8rem}.panel h2{margin:0}.summary{color:var(--muted)}.table-wrap{overflow-x:auto}table{width:100%;border-collapse:collapse}th,td{padding:.75rem .6rem;border-bottom:1px solid var(--line);text-align:start;vertical-align:top}th{color:var(--muted);font-size:.7rem;text-transform:uppercase;letter-spacing:.06em}td small{display:block;margin-top:.18rem;color:var(--muted)}.num{text-align:end;font-variant-numeric:tabular-nums}.recon-row{display:grid;grid-template-columns:1fr auto;gap:.25rem .8rem;padding:.75rem 0;border-bottom:1px solid var(--line)}.recon-row small{grid-column:1/-1;color:var(--muted)}.muted{color:var(--muted)}.error{padding:.75rem 1rem;border:1px solid #d98c8c;border-radius:9px;color:#9d3f3f;background:#fff4f4}@media(max-width:800px){.toolbar{align-items:stretch;flex-direction:column}label{width:100%}}`],
})
export class FinanceReportsWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(FinanceService);
  readonly tabs: ReportTab[] = ['trial', 'ledger', 'ap', 'ar', 'pnl', 'bs', 'reconciliation'];
  readonly companies = signal<FinanceCompany[]>([]);
  readonly companyId = signal('');
  readonly active = signal<ReportTab>('trial');
  reportTabs(): AppTab[] { return this.tabs.map(id => ({ id, label: this.text(id) })); }
  selectTab(id: string): void { this.active.set(id as ReportTab); this.run(); }
  readonly trial = signal<FinanceTrialBalanceReport | null>(null);
  readonly ledger = signal<FinanceGeneralLedgerLine[] | null>(null);
  readonly aging = signal<FinanceAgingReportRow[] | null>(null);
  readonly statement = signal<FinanceStatementReport | null>(null);
  readonly reconciliation = signal<FinanceCloseReconciliation | null>(null);
  readonly trialColumns: DataGridColumn<FinanceTrialBalanceRow>[] = [
    { key: 'accountCode', label: 'Account', value: row => row.accountCode, secondaryText: row => row.accountName, filter: 'text' },
    { key: 'accountType', label: 'Type', value: row => row.accountType, filter: 'select' },
    { key: 'openingBalance', label: 'Opening', value: row => row.openingBalance, filter: 'number-range', align: 'end' },
    { key: 'periodDebit', label: 'Debit', value: row => row.periodDebit, filter: 'number-range', align: 'end' },
    { key: 'periodCredit', label: 'Credit', value: row => row.periodCredit, filter: 'number-range', align: 'end' },
    { key: 'closingBalance', label: 'Closing', value: row => row.closingBalance, filter: 'number-range', align: 'end' },
  ];
  readonly statementColumns: DataGridColumn<FinanceStatementRow>[] = [
    { key: 'accountCode', label: 'Account', value: row => row.accountCode, secondaryText: row => row.accountName, filter: 'text' },
    { key: 'accountType', label: 'Type', value: row => row.accountType, filter: 'select' },
    { key: 'openingBalance', label: 'Opening', value: row => row.openingBalance, filter: 'number-range', align: 'end' },
    { key: 'debit', label: 'Debit', value: row => row.debit, filter: 'number-range', align: 'end' },
    { key: 'credit', label: 'Credit', value: row => row.credit, filter: 'number-range', align: 'end' },
    { key: 'closingBalance', label: 'Closing', value: row => row.closingBalance, filter: 'number-range', align: 'end' },
  ];
  readonly ledgerColumns: DataGridColumn<FinanceGeneralLedgerLine>[] = [
    { key: 'postingDate', label: 'Date', value: row => row.postingDate, filter: 'date-range' },
    { key: 'journalNumber', label: 'Journal', value: row => row.journalNumber, secondaryText: row => row.sourceContract, filter: 'text' },
    { key: 'accountCode', label: 'Account', value: row => row.accountCode, secondaryText: row => row.accountName, filter: 'text' },
    { key: 'functionalDebit', label: 'Debit', value: row => row.functionalDebit, filter: 'number-range', align: 'end' },
    { key: 'functionalCredit', label: 'Credit', value: row => row.functionalCredit, filter: 'number-range', align: 'end' },
    { key: 'runningBalance', label: 'Running', value: row => row.runningBalance, filter: 'number-range', align: 'end' },
  ];
  readonly agingColumns: DataGridColumn<FinanceAgingReportRow>[] = [
    { key: 'sourceReference', label: 'Reference', value: row => row.sourceReference, filter: 'text' },
    { key: 'dueDate', label: 'Due', value: row => row.dueDate, filter: 'date-range' },
    { key: 'agingBucket', label: 'Bucket', value: row => row.agingBucket, filter: 'select' },
    { key: 'currencyCode', label: 'Currency', value: row => row.currencyCode, filter: 'select' },
    { key: 'outstandingAmount', label: 'Outstanding', value: row => row.outstandingAmount, filter: 'number-range', align: 'end' },
  ];
  readonly reconciliationColumns: DataGridColumn<FinanceReconciliationView>[] = [
    { key: 'scope', label: 'Scope', value: row => row.scope, filter: 'text' },
    { key: 'status', label: 'Status', value: row => row.status, filter: 'select', badge: true },
    { key: 'difference', label: 'Difference', value: row => row.difference, filter: 'number-range', align: 'end' },
    { key: 'sourceReference', label: 'Source', value: row => row.sourceReference, filter: 'text' },
    { key: 'detail', label: 'Details', value: row => row.detail, filter: 'text' },
  ];
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  asOfDate = this.today();
  fromDate = `${new Date().getUTCFullYear()}-01-01`;
  toDate = this.today();

  ngOnInit(): void { this.service.companies().subscribe({ next: (items) => { this.companies.set(items); if (items[0]) { this.companyId.set(items[0].companyId); this.run(); } }, error: () => this.error.set(this.text('unavailable')) }); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  run(): void { const companyId = this.companyId(); if (!companyId) return; this.busy.set(true); this.error.set(null); const tab = this.active(); const done = () => this.busy.set(false); if (tab === 'trial') this.service.trialBalance(companyId, this.asOfDate).subscribe({ next: (item) => { this.trial.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'ledger') this.service.generalLedger(companyId, this.fromDate, this.toDate).subscribe({ next: (item) => { this.ledger.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'ap' || tab === 'ar') this.service.reportAging(companyId, this.asOfDate, tab === 'ap' ? 'Payable' : 'Receivable').subscribe({ next: (item) => { this.aging.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else if (tab === 'pnl' || tab === 'bs') this.service.statement(companyId, this.fromDate, this.toDate, tab === 'pnl' ? 'profit-loss' : 'balance-sheet').subscribe({ next: (item) => { this.statement.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); else this.service.closeReconciliation(companyId, this.asOfDate).subscribe({ next: (item) => { this.reconciliation.set(item); done(); }, error: () => { this.error.set(this.text('unavailable')); done(); } }); }
  exportUrl(report: string): string { const companyId = this.companyId(); if (report === 'reconciliation') { const params = new URLSearchParams({ companyId, asOfDate: this.asOfDate }); return `/api/v1/finance/reconciliation/close/export?${params.toString()}`; } const params = new URLSearchParams({ companyId }); if (report === 'general-ledger' || report === 'profit-loss' || report === 'balance-sheet') { params.set('fromDate', this.fromDate); params.set('toDate', this.toDate); } else { params.set('asOfDate', this.asOfDate); } return `/api/v1/finance/reports/${report}/export?${params.toString()}`; }
  resultAvailable(): boolean { return this.active() === 'trial' ? !!this.trial() : this.active() === 'ledger' ? !!this.ledger() : this.active() === 'ap' || this.active() === 'ar' ? !!this.aging() : this.active() === 'reconciliation' ? !!this.reconciliation() : !!this.statement(); }
  private today(): string { return new Date().toISOString().slice(0, 10); }
}
