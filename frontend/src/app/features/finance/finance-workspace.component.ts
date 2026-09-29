import { CommonModule } from '@angular/common';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { FinanceService } from './finance.service';
import { FinanceAccount, FinanceAccountWriteRequest, FinanceCompany, FinanceFiscalCalendar, FinanceFiscalPeriod, FinanceFiscalYear, FinanceGlLine, FinanceHandoff, FinanceJournal, FinanceJournalWriteRequest, FinancePostingRule, FinancePostingRuleWriteRequest } from './finance.model';

type FinanceTab = 'overview' | 'accounts' | 'periods' | 'journals' | 'rules' | 'handoffs' | 'gl';
type Bilingual = { en: string; ar: string };

const copy: Record<string, Bilingual> = {
  kicker: { en: 'Finance foundation / GL', ar: 'أساس المالية / الأستاذ العام' },
  title: { en: 'Company books, controlled posting', ar: 'دفاتر الشركة وترحيل منضبط' },
  lead: { en: 'A bounded operational view of the reusable Finance foundation. Posting remains explicit, balanced, period-controlled, and traceable to source evidence.', ar: 'واجهة تشغيلية محددة لأساس المالية القابل لإعادة الاستخدام. يظل الترحيل صريحاً ومتوازناً ومقيداً بالفترة وقابلاً للتتبع إلى دليل المصدر.' },
  company: { en: 'Authorized Company', ar: 'الشركة المصرح بها' },
  chooseCompany: { en: 'Choose an authorized Company', ar: 'اختر شركة مصرحاً بها' },
  refresh: { en: 'Refresh evidence', ar: 'تحديث الأدلة' },
  overview: { en: 'Control room', ar: 'لوحة التحكم' },
  accounts: { en: 'Chart of accounts', ar: 'دليل الحسابات' },
  periods: { en: 'Fiscal periods', ar: 'الفترات المالية' },
  journals: { en: 'Journals', ar: 'القيود' },
  rules: { en: 'Posting rules', ar: 'قواعد الترحيل' },
  handoffs: { en: 'Inventory handoff', ar: 'تسليم المخزون' },
  gl: { en: 'GL inquiry', ar: 'استعلام الأستاذ العام' },
  currency: { en: 'Functional currency', ar: 'العملة الوظيفية' },
  accountsLead: { en: 'Company-owned hierarchy with immutable posted snapshots.', ar: 'هيكل مملوك للشركة مع لقطات غير قابلة للتغيير للقيود المرحلة.' },
  periodsLead: { en: 'Every posting date must resolve to exactly one Open period.', ar: 'يجب أن تتطابق كل تاريخ ترحيل مع فترة مفتوحة واحدة بالضبط.' },
  journalsLead: { en: 'Draft → Submitted → Approved → Posted. Reversal creates a new linked journal.', ar: 'مسودة ← مقدم ← معتمد ← مرحل. ينشئ العكس قيداً جديداً مرتبطاً.' },
  rulesLead: { en: 'Effective-dated source mappings are versioned and never guessed at posting time.', ar: 'تتم إدارة ربط المصادر المؤرخ والنسخ دون تخمين وقت الترحيل.' },
  handoffsLead: { en: 'Inventory valuation facts are consumed only when ReadyForFinance; Inventory tables are never mutated here.', ar: 'تُستهلك حقائق تقييم المخزون فقط عند جاهزيتها للمالية، ولا تعدل جداول المخزون هنا.' },
  glLead: { en: 'Posted immutable facts with account, period, source, and reversal lineage.', ar: 'حقائق مرحلة غير قابلة للتغيير مع الحساب والفترة والمصدر وتتبع العكس.' },
  loading: { en: 'Reading Finance evidence…', ar: 'جارٍ قراءة أدلة المالية…' },
  empty: { en: 'No records are configured for this Company yet.', ar: 'لا توجد سجلات معدة لهذه الشركة بعد.' },
  unavailable: { en: 'Finance evidence is unavailable right now. Try again shortly.', ar: 'أدلة المالية غير متاحة حالياً. حاول مرة أخرى بعد قليل.' },
  status: { en: 'Status', ar: 'الحالة' },
  code: { en: 'Code', ar: 'الرمز' },
  name: { en: 'Name', ar: 'الاسم' },
  type: { en: 'Type', ar: 'النوع' },
  posting: { en: 'Posting', ar: 'الترحيل' },
  sequence: { en: 'Sequence', ar: 'التسلسل' },
  source: { en: 'Source', ar: 'المصدر' },
  debit: { en: 'Debit', ar: 'مدين' },
  credit: { en: 'Credit', ar: 'دائن' },
  evidence: { en: 'Evidence', ar: 'الدليل' },
  ready: { en: 'Ready for Finance', ar: 'جاهز للمالية' },
  noCompany: { en: 'No authorized Company context is available.', ar: 'لا يوجد سياق شركة مصرح به.' },
  createAccount: { en: 'Create account', ar: 'إنشاء حساب' },
  accountCode: { en: 'Account code', ar: 'رمز الحساب' },
  accountName: { en: 'English name', ar: 'الاسم بالإنجليزية' },
  accountType: { en: 'Account type', ar: 'نوع الحساب' },
  postingAccount: { en: 'Posting account', ar: 'حساب ترحيل' },
  save: { en: 'Save', ar: 'حفظ' },
  journalDate: { en: 'Posting date', ar: 'تاريخ الترحيل' },
  debitAccount: { en: 'Debit account', ar: 'الحساب المدين' },
  creditAccount: { en: 'Credit account', ar: 'الحساب الدائن' },
  amount: { en: 'Amount', ar: 'المبلغ' },
  createJournal: { en: 'Create draft journal', ar: 'إنشاء قيد مسودة' },
  submit: { en: 'Submit', ar: 'إرسال' },
  approve: { en: 'Approve', ar: 'اعتماد' },
  post: { en: 'Post', ar: 'ترحيل' },
  createRule: { en: 'Create posting rule', ar: 'إنشاء قاعدة ترحيل' },
  sourceEvent: { en: 'Source event', ar: 'حدث المصدر' },
  process: { en: 'Process', ar: 'معالجة' },
};

@Component({
  selector: 'app-finance-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, CommonModule, FormsModule, RouterLink],
  template: `
    <section class="finance-page" data-testid="finance-workspace">
      <app-page-header class="page-header">
        <div page-header-copy><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lede">{{ text('lead') }}</p></div>
          <div page-header-actions>
        <div class="header-actions"><a class="button button--secondary" routerLink="/app/inventory/valuation">{{ text('handoffs') }}</a><a class="button button--secondary" routerLink="/app/finance/tax-fx">{{ language.language() === 'ar' ? 'الضريبة / العملات' : 'Tax / FX' }}</a><button class="button button--primary" type="button" (click)="load()" [disabled]="loading()">{{ text('refresh') }}</button></div>
          </div>
        </app-page-header>

      @if (companies().length === 0 && !loading()) { <section class="finance-empty" data-testid="finance-no-company"><h2>{{ text('noCompany') }}</h2><p>{{ text('lead') }}</p></section> }
      @else {
        <section class="finance-controlbar"><label><span>{{ text('company') }}</span><select [ngModel]="companyId()" (ngModelChange)="selectCompany($event)" data-testid="finance-company-select"><option value="">{{ text('chooseCompany') }}</option>@for (company of companies(); track company.companyId + (company.branchId ?? '')) { <option [value]="company.companyId">{{ company.companyName }} · {{ company.functionalCurrencyCode }}</option> }</select></label><div class="currency-chip"><span>{{ text('currency') }}</span><strong>{{ selectedCompany()?.functionalCurrencyCode ?? '—' }}</strong></div></section>
        @if (loading()) { <section class="finance-empty" aria-live="polite"><span class="spinner"></span><h2>{{ text('loading') }}</h2></section> }
        @else if (error()) { <section class="finance-empty finance-empty--error" role="alert"><h2>{{ text('unavailable') }}</h2><p>{{ error() }}</p><button class="button button--secondary" type="button" (click)="load()">{{ text('refresh') }}</button></section> }
        @else if (companyId()) {
          <nav class="finance-tabs" role="tablist" aria-label="Finance views">@for (tab of tabs; track tab) { <button type="button" role="tab" [attr.aria-selected]="activeTab() === tab" [class.is-active]="activeTab() === tab" (click)="activeTab.set(tab)">{{ text(tab) }}</button> }</nav>
          @if (activeTab() === 'overview') { <section class="finance-cards"><article><span>{{ text('accounts') }}</span><strong>{{ accounts().length }}</strong><small>{{ text('accountsLead') }}</small></article><article><span>{{ text('journals') }}</span><strong>{{ journals().length }}</strong><small>{{ text('journalsLead') }}</small></article><article><span>{{ text('rules') }}</span><strong>{{ rules().length }}</strong><small>{{ text('rulesLead') }}</small></article><article class="accent"><span>{{ text('handoffs') }}</span><strong>{{ readyHandoffs().length }}</strong><small>{{ text('handoffsLead') }}</small></article></section> }
          @if (activeTab() === 'accounts') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('accounts') }}</p><h2>{{ text('accountsLead') }}</h2></div><span class="count">{{ accounts().length }}</span></div>@if (accounts().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="accounts()" [columns]="accountColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('accounts')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="accounts().length || 1" [showPager]="false" /> }</section> }
          @if (activeTab() === 'periods') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('periods') }}</p><h2>{{ text('periodsLead') }}</h2></div><span class="count">{{ periods().length }}</span></div>@if (periods().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="periods()" [columns]="periodColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('periods')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="periods().length || 1" [showPager]="false" /> }</section> }
          @if (activeTab() === 'journals') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('journals') }}</p><h2>{{ text('journalsLead') }}</h2></div><span class="count">{{ journals().length }}</span></div>@if (journals().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="journals()" [columns]="journalColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('journals')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="journals().length || 1" [showPager]="false" /> }</section> }
          @if (activeTab() === 'rules') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('rules') }}</p><h2>{{ text('rulesLead') }}</h2></div><span class="count">{{ rules().length }}</span></div>@if (rules().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="rules()" [columns]="ruleColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('rules')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="rules().length || 1" [showPager]="false" /> }</section> }
          @if (activeTab() === 'handoffs') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('handoffs') }}</p><h2>{{ text('handoffsLead') }}</h2></div><span class="count">{{ handoffs().length }}</span></div>@if (handoffs().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="handoffs()" [columns]="handoffColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('handoffs')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="handoffs().length || 1" [showPager]="false" /> }</section> }
          @if (activeTab() === 'gl') { <section class="finance-panel"><div class="panel-heading"><div><p class="eyebrow">{{ text('gl') }}</p><h2>{{ text('glLead') }}</h2></div><span class="count">{{ gl().length }}</span></div>@if (gl().length === 0) { <p class="empty-copy">{{ text('empty') }}</p> } @else { <app-data-grid [rows]="gl()" [columns]="glColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('gl')" [countLabel]="language.language() === 'ar' ? '\u0633\u062c\u0644\u0627\u062a' : 'records'" [filterInputLabel]="language.language() === 'ar' ? '\u062a\u0635\u0641\u064a\u0629' : 'Filter'" [allValuesLabel]="language.language() === 'ar' ? '\u0627\u0644\u0643\u0644' : 'All'" [clearFiltersLabel]="language.language() === 'ar' ? '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' : 'Clear filters'" [pageSize]="gl().length || 1" [showPager]="false" /> }</section> }
           @if (activeTab() === 'accounts' && companyId()) { <section class="finance-panel finance-editor"><div class="panel-heading"><div><p class="eyebrow">{{ text('createAccount') }}</p><h2>{{ text('accountsLead') }}</h2></div></div><form (ngSubmit)="createAccount()" class="finance-form"><label><span>{{ text('accountCode') }}</span><input name="accountCode" [(ngModel)]="accountDraft.code" required maxlength="64" /></label><label><span>{{ text('accountName') }}</span><input name="accountName" [(ngModel)]="accountDraft.englishName" required maxlength="256" /></label><label><span>{{ text('accountType') }}</span><select name="accountType" [(ngModel)]="accountDraft.accountType">@for (type of accountTypes; track type) { <option [value]="type">{{ type }}</option> }</select></label><label class="check-field"><input type="checkbox" name="postingAccount" [(ngModel)]="accountDraft.isPostingAccount" /> <span>{{ text('postingAccount') }}</span></label><button class="button button--primary" type="submit" [disabled]="operationBusy()">{{ text('save') }}</button></form></section> }
           @if (activeTab() === 'journals' && companyId()) { <section class="finance-panel finance-editor"><div class="panel-heading"><div><p class="eyebrow">{{ text('createJournal') }}</p><h2>{{ text('journalsLead') }}</h2></div></div><form (ngSubmit)="createJournal()" class="finance-form"><label><span>{{ text('debitAccount') }}</span><select name="debitAccount" [(ngModel)]="journalDraft.lines[0].accountId" required>@for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.code }} · {{ account.englishName }}</option> }</select></label><label><span>{{ text('creditAccount') }}</span><select name="creditAccount" [(ngModel)]="journalDraft.lines[1].accountId" required>@for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.code }} · {{ account.englishName }}</option> }</select></label><label><span>{{ text('amount') }}</span><input name="journalAmount" type="number" min="0.01" step="0.01" [(ngModel)]="journalDraft.lines[0].debit" required /></label><label><span>{{ text('journalDate') }}</span><input name="journalDate" type="date" [(ngModel)]="journalDraft.postingDate" required /></label><div class="balance-preview"><span>{{ text('debit') }} / {{ text('credit') }}</span><strong>{{ journalDraft.lines[0].debit | number:'1.2-2' }} / {{ journalDraft.lines[0].debit | number:'1.2-2' }}</strong></div><button class="button button--primary" type="submit" [disabled]="operationBusy() || accounts().length < 2">{{ text('createJournal') }}</button></form></section> }
           @if (activeTab() === 'rules' && companyId()) { <section class="finance-panel finance-editor"><div class="panel-heading"><div><p class="eyebrow">{{ text('createRule') }}</p><h2>{{ text('rulesLead') }}</h2></div></div><form (ngSubmit)="createRule()" class="finance-form"><label><span>{{ text('source') }}</span><input name="sourceContract" [(ngModel)]="ruleDraft.sourceContract" required /></label><label><span>{{ text('sourceEvent') }}</span><input name="sourceEvent" [(ngModel)]="ruleDraft.sourceEvent" required /></label><label><span>{{ text('debitAccount') }}</span><select name="ruleDebit" [(ngModel)]="ruleDraft.debitAccountId" required>@for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.code }}</option> }</select></label><label><span>{{ text('creditAccount') }}</span><select name="ruleCredit" [(ngModel)]="ruleDraft.creditAccountId" required>@for (account of accounts(); track account.id) { <option [value]="account.id">{{ account.code }}</option> }</select></label><button class="button button--primary" type="submit" [disabled]="operationBusy() || accounts().length < 2">{{ text('save') }}</button></form></section> }
         }
      }
    </section>
  `,
  styles: [`
    :host { display: block; } .finance-page { display: grid; gap: 1.25rem; }  h1 { max-width: 760px; margin: .35rem 0 .6rem; } .lede { max-width: 760px; } .header-actions { display: flex; gap: .65rem; flex-wrap: wrap; } .finance-controlbar { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding: 1rem 1.15rem; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); box-shadow: var(--shadow-sm); } label { display: grid; gap: .4rem; min-width: min(100%, 360px); } label span, .currency-chip span { color: var(--muted); font-size: .73rem; font-weight: 750; letter-spacing: .08em; text-transform: uppercase; } input, select { min-height: 2.65rem; padding: .55rem .7rem; border: 1px solid var(--line-strong); border-radius: 9px; background: var(--surface); color: var(--ink); font: inherit; } .currency-chip { display: grid; gap: .25rem; padding: .65rem .9rem; border-radius: 10px; background: var(--mint); color: var(--ink); } .finance-tabs { display: flex; gap: .35rem; overflow-x: auto; border-bottom: 1px solid var(--line); } .finance-tabs button { padding: .8rem .9rem; border: 0; border-bottom: 3px solid transparent; background: transparent; color: var(--muted); font: inherit; font-weight: 700; white-space: nowrap; cursor: pointer; } .finance-tabs button.is-active { border-bottom-color: var(--teal); color: var(--ink); } .finance-cards { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .9rem; } .finance-cards article { display: grid; gap: .5rem; min-height: 145px; padding: 1.1rem; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); box-shadow: var(--shadow-sm); } .finance-cards article.accent { background: var(--ink); color: #f2f8f4; } .finance-cards span { font-size: .76rem; font-weight: 800; text-transform: uppercase; letter-spacing: .07em; color: var(--muted); } .finance-cards .accent span, .finance-cards .accent small { color: #b4c9c0; } .finance-cards strong { font-size: 2.25rem; } .finance-cards small { line-height: 1.45; color: var(--muted); } .finance-panel, .finance-empty { padding: 1.35rem; border: 1px solid var(--line); border-radius: 14px; background: var(--surface); box-shadow: var(--shadow-sm); } .finance-empty { min-height: 220px; display: grid; place-content: center; text-align: center; gap: .5rem; } .finance-empty--error { border-color: color-mix(in srgb, var(--danger) 45%, var(--line)); } .panel-heading { display: flex; justify-content: space-between; align-items: start; gap: 1rem; margin-bottom: 1rem; } .panel-heading h2 { margin: .25rem 0 0; max-width: 720px; font-size: 1.15rem; } .finance-editor { background: linear-gradient(135deg, var(--surface), color-mix(in srgb, var(--mint) 35%, var(--surface))); } .finance-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .85rem; align-items: end; } .finance-form .button { justify-self: start; } .check-field { display: flex; align-items: center; min-height: 2.65rem; } .check-field input { min-height: auto; } .balance-preview { display: grid; gap: .2rem; padding: .75rem; border-radius: 10px; background: var(--ink); color: #f2f8f4; } .count { min-width: 2rem; padding: .3rem .55rem; border-radius: 999px; background: var(--mint); color: var(--teal); font-weight: 800; text-align: center; } .table-wrap { overflow-x: auto; } table { width: 100%; border-collapse: collapse; } th, td { padding: .8rem .65rem; border-bottom: 1px solid var(--line); text-align: start; vertical-align: top; } th { color: var(--muted); font-size: .72rem; text-transform: uppercase; letter-spacing: .06em; } td small { display: block; margin-top: .2rem; color: var(--muted); } .numeric { text-align: end; font-variant-numeric: tabular-nums; } .status { display: inline-block; padding: .22rem .52rem; border-radius: 999px; background: #f3eee5; color: var(--muted); font-size: .78rem; font-weight: 750; } .status.active { background: var(--mint); color: var(--teal); } .empty-copy { color: var(--muted); } @media (max-width: 900px) { .finance-header, .finance-controlbar { align-items: stretch; flex-direction: column; } .finance-cards { grid-template-columns: repeat(2, minmax(0, 1fr)); } .finance-form { grid-template-columns: 1fr; } } @media (max-width: 560px) { .finance-cards { grid-template-columns: 1fr; } .finance-panel, .finance-empty { padding: 1rem; } }
  `],
})
export class FinanceWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(FinanceService);
  readonly tabs: FinanceTab[] = ['overview', 'accounts', 'periods', 'journals', 'rules', 'handoffs', 'gl'];
  readonly activeTab = signal<FinanceTab>('overview');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly companies = signal<FinanceCompany[]>([]);
  readonly companyId = signal('');
  readonly accounts = signal<FinanceAccount[]>([]);
  readonly calendars = signal<FinanceFiscalCalendar[]>([]);
  readonly periods = signal<FinanceFiscalPeriod[]>([]);
  readonly journals = signal<FinanceJournal[]>([]);
  readonly rules = signal<FinancePostingRule[]>([]);
  readonly handoffs = signal<FinanceHandoff[]>([]);
  readonly gl = signal<FinanceGlLine[]>([]);
  readonly accountTypes = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'];
  readonly operationBusy = signal(false);
  accountDraft: FinanceAccountWriteRequest = this.newAccountDraft();
  journalDraft: FinanceJournalWriteRequest = this.newJournalDraft();
  ruleDraft: FinancePostingRuleWriteRequest = this.newRuleDraft();
  readonly selectedCompany = computed(() => this.companies().find((company) => company.companyId === this.companyId()) ?? null);
  readonly readyHandoffs = computed(() => this.handoffs().filter((handoff) => handoff.status === 'Ready' || handoff.status === 'Posted'));
  readonly accountColumns: DataGridColumn<FinanceAccount>[] = [
    { key: 'code', label: this.text('code'), value: row => row.code, filter: 'text' },
    { key: 'name', label: this.text('name'), value: row => row.englishName, filter: 'text' },
    { key: 'type', label: this.text('type'), value: row => row.accountType, filter: 'text' },
    { key: 'posting', label: this.text('posting'), value: row => row.isPostingAccount ? 'Yes' : 'Group', filter: 'select', filterOptions: [{ value: 'Yes', label: 'Yes' }, { value: 'Group', label: 'Group' }] },
    { key: 'status', label: this.text('status'), value: row => row.lifecycle, badge: true, filter: 'text' },
  ];
  readonly periodColumns: DataGridColumn<FinanceFiscalPeriod>[] = [
    { key: 'sequence', label: this.text('sequence'), value: row => row.sequence, align: 'end' },
    { key: 'code', label: this.text('code'), value: row => row.code, filter: 'text' },
    { key: 'status', label: this.text('status'), value: row => row.state, badge: true, filter: 'text' },
    { key: 'from', label: 'From', value: row => row.startDate, filter: 'date-range' },
    { key: 'to', label: 'To', value: row => row.endDate, filter: 'date-range' },
  ];
  readonly journalColumns: DataGridColumn<FinanceJournal>[] = [
    { key: 'number', label: 'Number', value: row => row.journalNumber, filter: 'text' },
    { key: 'date', label: 'Date', value: row => row.postingDate, filter: 'date-range' },
    { key: 'status', label: this.text('status'), value: row => row.status, badge: true, filter: 'text' },
    { key: 'source', label: this.text('source'), value: row => row.sourceContract, filter: 'text' },
    { key: 'debit', label: this.text('debit'), value: row => this.totalDebit(row), display: row => this.totalDebit(row).toLocaleString(this.language.language(), { minimumFractionDigits: 2 }), align: 'end', filter: 'number-range' },
    { key: 'credit', label: this.text('credit'), value: row => this.totalCredit(row), display: row => this.totalCredit(row).toLocaleString(this.language.language(), { minimumFractionDigits: 2 }), align: 'end', filter: 'number-range' },
  ];
  readonly ruleColumns: DataGridColumn<FinancePostingRule>[] = [
    { key: 'source', label: this.text('source'), value: row => row.sourceContract, secondaryText: row => row.sourceEvent, filter: 'text' },
    { key: 'version', label: 'Version', value: row => row.versionNumber, align: 'end' },
    { key: 'debit', label: this.text('debit'), value: row => row.debitAccountCode, filter: 'text' },
    { key: 'credit', label: this.text('credit'), value: row => row.creditAccountCode, filter: 'text' },
    { key: 'from', label: 'From', value: row => row.effectiveFrom, filter: 'date-range' },
  ];
  readonly handoffColumns: DataGridColumn<FinanceHandoff>[] = [
    { key: 'sequence', label: this.text('sequence'), value: row => row.ledgerSequence, align: 'end' },
    { key: 'source', label: this.text('source'), value: row => row.sourceType, filter: 'text' },
    { key: 'amount', label: 'Amount', value: row => row.signedBaseAmount, display: row => `${row.signedBaseAmount.toLocaleString(this.language.language(), { minimumFractionDigits: 2 })} ${row.functionalCurrencyCode}`, align: 'end', filter: 'number-range' },
    { key: 'status', label: this.text('status'), value: row => row.status, badge: true, filter: 'text' },
    { key: 'evidence', label: this.text('evidence'), value: row => `${row.valuationEvidenceVersion} ? ${row.contractVersion}` },
  ];
  readonly glColumns: DataGridColumn<FinanceGlLine>[] = [
    { key: 'number', label: 'Number', value: row => row.journalNumber, filter: 'text' },
    { key: 'date', label: 'Date', value: row => row.postingDate, filter: 'date-range' },
    { key: 'account', label: this.text('accounts'), value: row => row.accountCode, secondaryText: row => row.accountName, filter: 'text' },
    { key: 'debit', label: this.text('debit'), value: row => row.functionalDebit, display: row => row.functionalDebit.toLocaleString(this.language.language(), { minimumFractionDigits: 2 }), align: 'end', filter: 'number-range' },
    { key: 'credit', label: this.text('credit'), value: row => row.functionalCredit, display: row => row.functionalCredit.toLocaleString(this.language.language(), { minimumFractionDigits: 2 }), align: 'end', filter: 'number-range' },
    { key: 'source', label: this.text('source'), value: row => row.sourceContract, filter: 'text' },
  ];


  ngOnInit(): void { this.load(); }
  text(key: string): string { const value = copy[key]; return value?.[this.language.language()] ?? value?.en ?? key; }
  selectCompany(companyId: string): void { this.companyId.set(companyId); this.resetDrafts(); this.loadCompany(); }
  totalDebit(journal: FinanceJournal): number { return journal.lines.reduce((sum, line) => sum + line.functionalDebit, 0); }
  totalCredit(journal: FinanceJournal): number { return journal.lines.reduce((sum, line) => sum + line.functionalCredit, 0); }

  load(): void { this.loading.set(true); this.error.set(null); this.service.companies().subscribe({ next: (companies) => { this.companies.set(companies); if (!this.companyId() || !companies.some((company) => company.companyId === this.companyId())) this.companyId.set(companies[0]?.companyId ?? ''); this.loadCompany(); }, error: () => { this.loading.set(false); this.error.set(this.text('unavailable')); } }); }
  private loadCompany(): void { const companyId = this.companyId(); if (!companyId) { this.loading.set(false); return; } this.loading.set(true); forkJoin({ accounts: this.service.accounts(companyId), calendars: this.service.calendars(companyId), rules: this.service.rules(companyId), journals: this.service.journals(companyId), gl: this.service.gl(companyId), handoffs: this.service.handoffs(companyId) }).subscribe({ next: (result) => { this.accounts.set(result.accounts); this.calendars.set(result.calendars); this.rules.set(result.rules); this.journals.set(result.journals); this.gl.set(result.gl); this.handoffs.set(result.handoffs); this.resetDrafts(); this.loading.set(false); this.loadPeriods(result.calendars); }, error: () => { this.loading.set(false); this.error.set(this.text('unavailable')); } }); }
  private loadPeriods(calendars: FinanceFiscalCalendar[]): void { this.periods.set([]); if (!calendars.length) return; this.service.years(calendars[0].id).subscribe({ next: (years) => { if (!years.length) return; this.service.periods(years[0].id).subscribe({ next: (periods) => this.periods.set(periods), error: () => undefined }); }, error: () => undefined }); }

  async createAccount(): Promise<void> {
    const companyId = this.companyId();
    if (!companyId) return;
    this.operationBusy.set(true);
    try {
      await this.service.createAccount({ ...this.accountDraft, companyId });
      this.accountDraft = this.newAccountDraft();
      this.loadCompany();
    } catch (error) {
      this.error.set(this.operationError(error));
    } finally {
      this.operationBusy.set(false);
    }
  }

  async createJournal(): Promise<void> {
    const companyId = this.companyId();
    if (!companyId || !this.journalDraft.lines[0]?.accountId || !this.journalDraft.lines[1]?.accountId) return;
    this.operationBusy.set(true);
    try {
      const amount = Math.max(0, Number(this.journalDraft.lines[0].debit) || 0);
      await this.service.createJournal({ ...this.journalDraft, companyId, description: this.journalDraft.description || 'Manual journal', lines: [
        { ...this.journalDraft.lines[0], debit: amount, credit: 0 },
        { ...this.journalDraft.lines[1], debit: 0, credit: amount },
      ] });
      this.journalDraft = this.newJournalDraft();
      this.loadCompany();
    } catch (error) {
      this.error.set(this.operationError(error));
    } finally {
      this.operationBusy.set(false);
    }
  }

  async createRule(): Promise<void> {
    const companyId = this.companyId();
    if (!companyId || !this.ruleDraft.debitAccountId || !this.ruleDraft.creditAccountId) return;
    this.operationBusy.set(true);
    try {
      await this.service.createPostingRule({ ...this.ruleDraft, companyId });
      this.ruleDraft = this.newRuleDraft();
      this.loadCompany();
    } catch (error) {
      this.error.set(this.operationError(error));
    } finally {
      this.operationBusy.set(false);
    }
  }

  private resetDrafts(): void {
    this.accountDraft = this.newAccountDraft();
    this.journalDraft = this.newJournalDraft();
    this.ruleDraft = this.newRuleDraft();
  }

  private newAccountDraft(): FinanceAccountWriteRequest {
    return { companyId: '', code: '', englishName: '', arabicName: null, parentAccountId: null, accountType: 'Asset', isPostingAccount: true, currencyBehavior: 'FunctionalOnly', effectiveFrom: this.today(), effectiveTo: null };
  }

  private newJournalDraft(): FinanceJournalWriteRequest {
    return { companyId: '', journalDate: this.today(), postingDate: this.today(), transactionCurrencyCode: null, exchangeRate: null, description: '', lines: [{ accountId: '', debit: 0, credit: 0, costCenterId: null, description: null }, { accountId: '', debit: 0, credit: 0, costCenterId: null, description: null }] };
  }

  private newRuleDraft(): FinancePostingRuleWriteRequest {
    return { companyId: '', sourceContract: 'inventory-valuation-finance.v1', sourceEvent: 'Inbound', debitAccountId: '', creditAccountId: '', costCenterRequired: false, effectiveFrom: this.today(), effectiveTo: null };
  }

  private today(): string { return new Date().toISOString().slice(0, 10); }
  private operationError(error: unknown): string { return error instanceof Error ? error.message : this.text('unavailable'); }
}
