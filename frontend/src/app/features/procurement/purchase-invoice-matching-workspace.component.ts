import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService } from '../../core/i18n/language.service';
import { PurchaseInvoiceHandoffService } from './purchase-invoice-handoff.service';
import { PurchaseInvoiceHandoffResponse } from './purchase-invoice-handoff.model';
import { GoodsReceiptResponse, GoodsReceiptWarehouseOptionResponse } from './goods-receipt.model';
import { GoodsReceiptService } from './goods-receipt.service';
import { PurchaseOrderResponse } from './purchase-order.model';
import { PurchaseOrderService } from './purchase-order.service';
import { ExchangeRateRecord, MasterDataRecord } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import {
  PurchaseInvoiceMatchAuditResponse,
  PurchaseInvoiceMatchEvaluateRequest,
  PurchaseInvoiceMatchHistoryResponse,
  PurchaseInvoiceMatchListItemResponse,
  PurchaseInvoiceMatchResponse,
  PurchaseInvoiceMatchResult,
} from './purchase-invoice-matching.model';
import { PurchaseInvoiceMatchingService } from './purchase-invoice-matching.service';

type WorkspaceMode = 'list' | 'detail';

@Component({
  selector: 'app-purchase-invoice-matching-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, FormsModule, RouterLink],
  template: `
    @if (mode() === 'list') {
      <section class="ui-page matching-page" data-testid="invoice-matching-list">
        <app-page-header class="page-header">
          <div page-header-copy><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lede">{{ text('lead') }}</p></div>
          <div page-header-actions>
          <div class="control-mark" aria-hidden="true"><span>PO</span><i></i><span>GR</span><i></i><span>INV</span></div>
        
          </div>
        </app-page-header>{ text('resultFilter') }}</span><select [value]="resultFilter()" (change)="setResultFilter($any($event.target).value)"><option value="">{{ text('allResults') }}</option>@for (result of results; track result) {<option [value]="result">{{ resultLabel(result) }}</option>}</select></label>
            <p class="filter-note">{{ text('filterNote') }}</p>
          </div>
          @if (loading()) { <div class="state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ text('loading') }}</h2></div> }
          @else if (error(); as currentError) { <div class="state-card state-card--error" role="alert"><strong>{{ text('loadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadList()">{{ language.text('retry') }}</button></div> }
          @else if (records().length === 0) { <div class="empty-ledger"><span class="empty-glyph" aria-hidden="true">◎</span><h2>{{ text('emptyTitle') }}</h2><p>{{ text('emptyLead') }}</p></div> }
          @else {
            <app-data-grid [caption]="text('title')" [rows]="records()" [columns]="gridColumns()" [language]="language.language()" [pageSize]="records().length || 1" [showPager]="false" [rowActions]="gridActions()" [countLabel]="language.text('recordCount')" [rowActionsLabel]="text('actions')" (rowAction)="onGridAction($event)" />
          }
        </section>
      </section>
    }

    @if (mode() === 'detail') {
      <section class="ui-page matching-page" data-testid="invoice-matching-detail">
        @if (loading()) { <section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ text('loading') }}</h2></section> }
        @else if (error(); as currentError) { <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ text('loadFailed') }}</strong><p>{{ errorText(currentError) }}</p><a class="button button--secondary" routerLink="/app/procurement/invoice-matching">{{ text('back') }}</a></section> }
        @else if (match(); as currentMatch) {
           <app-page-header class="page-header"><div page-header-copy><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ resultLabel(currentMatch.result) }}</h1><p class="lede">{{ text('detailLead') }} · {{ orderReference() }} · {{ invoiceReference() }}</p></div>
          <div page-header-actions><span class="match-status match-status--hero" [class]="resultClass(currentMatch.result)"><span aria-hidden="true"></span>{{ resultLabel(currentMatch.result) }}</span>
          </div>
        </app-page-header>utton></div>
          <div class="evidence-spine" aria-label="Three-way evidence lineage">
             <article class="evidence-card"><div class="evidence-index">01</div><p class="section-kicker">{{ text('purchaseOrder') }}</p><h2>{{ orderReference() }}</h2><p>{{ supplierLabel() }} · {{ text('versionCaptured') }} <code>{{ shortRef(currentMatch.purchaseOrderVersion) }}</code></p></article>
            <div class="evidence-connector" aria-hidden="true">→</div>
             <article class="evidence-card"><div class="evidence-index">02</div><p class="section-kicker">{{ text('acceptedReceipt') }}</p><h2>{{ receiptReferences() }}</h2><p>{{ text('receiptBasis') }} · {{ text('handoffVersion') }} <code>{{ shortRef(currentMatch.handoffVersion) }}</code></p></article>
            <div class="evidence-connector" aria-hidden="true">→</div>
             <article class="evidence-card evidence-card--invoice"><div class="evidence-index">03</div><p class="section-kicker">{{ text('supplierInvoice') }}</p><h2>{{ invoiceReference() }}</h2><p>{{ supplierLabel() }} · {{ currencyLabel() }} · {{ currentMatch.declaredEvidenceVersion ? text('evidenceVersion') + ' ' + currentMatch.declaredEvidenceVersion : text('evidenceMissing') }}</p></article>
           </div>
           @if (handoff(); as currentHandoff) {
             <section class="ui-surface detail-card source-facts"><div class="card-heading"><div><p class="section-kicker">{{ text('sourceFacts') }}</p><h2>{{ text('declaredLines') }}</h2></div><span class="source-meta">{{ supplierLabel() }} · {{ currencyLabel() }}</span></div>
               @if (currentHandoff.declaredEvidence?.lines?.length) {<div class="ui-grid-shell"><table class="ui-grid"><caption class="sr-only">{{ text('declaredLines') }}</caption><thead><tr><th scope="col">{{ text('product') }}</th><th scope="col">{{ text('uom') }}</th><th scope="col" class="numeric">{{ text('quantity') }}</th><th scope="col" class="numeric">{{ text('unitPrice') }}</th><th scope="col">{{ text('warehouse') }}</th></tr></thead><tbody>@for (line of currentHandoff.declaredEvidence?.lines ?? []; track line.id) {<tr><td><strong>{{ productLabel(line.purchaseOrderLineId) }}</strong></td><td>{{ uomLabel(line.purchaseOrderLineId) }}</td><td class="numeric">{{ formatNumber(line.quantity) }}</td><td class="numeric">{{ formatNumber(line.unitPrice) }} {{ currencyLabel() }}</td><td>{{ warehouseLabel() }}</td></tr>}</tbody></table></div>} @else {<p class="empty-inline">{{ text('evidenceMissing') }}</p>}
             </section>
           }
          @if (isCrossCurrency()) {
            <section class="ui-surface detail-card fx-card" data-testid="matching-exchange-rate-selector" aria-labelledby="matching-fx-title">
              <div class="card-heading"><div><p class="section-kicker">{{ text('crossCurrency') }}</p><h2 id="matching-fx-title">{{ text('exchangeRateReference') }}</h2></div><span class="source-meta">{{ declaredCurrencyCode() }} → {{ purchaseOrderCurrencyCode() }}</span></div>
              <p class="detail-copy">{{ text('crossCurrencyLead') }}</p>
              @if (exchangeRateLoading()) { <p class="state-note" role="status">{{ text('exchangeRateLoading') }}</p> }
              @else if (eligibleExchangeRates().length === 0) { <p class="inline-alert" role="alert" data-testid="matching-exchange-rate-missing">{{ text('noExchangeRate') }}</p><p class="detail-copy">{{ invoiceDate() ? text('noExchangeRateLead') : text('invoiceDateMissing') }}</p> }
              @else {
                <label class="form-field fx-selector"><span>{{ text('selectExchangeRate') }} <em>*</em></span><select [value]="selectedExchangeRateId" (change)="selectExchangeRate($any($event.target).value)" aria-describedby="matching-fx-help"><option value="">{{ text('chooseExchangeRate') }}</option>@for (rate of eligibleExchangeRates(); track rate.id) {<option [value]="rate.id">{{ exchangeRateLabel(rate) }}</option>}</select></label>
                <p id="matching-fx-help" class="term-hint">{{ text('serverOwnedSnapshot') }}</p>
                <div class="fx-candidate-list" aria-label="{{ text('exchangeRateCandidates') }}">@for (rate of eligibleExchangeRates(); track rate.id) {<div class="fx-candidate"><strong>{{ exchangeRateLabel(rate) }}</strong><span>{{ exchangeRateWindows(rate) }}</span></div>}</div>
                @if (!selectedExchangeRateId) { <p class="inline-alert" role="status">{{ text('selectionRequiredForComparableCurrency') }}</p> }
              }
              @if (currentMatch.appliedExchangeRate; as appliedRate) { <div class="fx-applied" data-testid="matching-applied-exchange-rate"><strong>{{ text('appliedExchangeRate') }}</strong><span>{{ appliedRate.sourceCurrencyCode }} → {{ appliedRate.targetCurrencyCode }} · v{{ appliedRate.versionNumber }} · {{ appliedRate.effectiveOn }}</span><small>{{ appliedRate.provenance || text('missing') }} · {{ appliedRate.source || text('missing') }}</small></div> }
            </section>
          }
          <section class="match-layout">
             <section class="ui-surface detail-card policy-card"><p class="section-kicker">{{ text('policy') }}</p><h2>{{ currentMatch.policy.policyId }}</h2><div class="policy-grid"><span>{{ text('policyVersion') }} <strong>{{ currentMatch.policy.version }}</strong></span><span>{{ text('amountTolerance') }} <strong>{{ currentMatch.policy.amountAbsoluteTolerance }}</strong></span><span>{{ text('quantityTolerance') }} <strong>{{ currentMatch.policy.quantityAbsoluteTolerance }}</strong></span></div><p class="detail-copy">{{ text('policyNote') }}</p>@if (currentMatch.resolutionPolicy; as resolutionPolicy) {<p class="policy-evidence">{{ text('resolutionPolicy') }} · <strong>{{ resolutionPolicy.policyId }} v{{ resolutionPolicy.version }}</strong></p>}</section>
            <section class="ui-surface detail-card resolution-card"><p class="section-kicker">{{ text('resolution') }}</p><h2>{{ currentMatch.result === 'ExceptionHold' ? text('exceptionTitle') : text('decisionTitle') }}</h2><p class="detail-copy">{{ currentMatch.resolutionReason || text('resolutionNote') }}</p>@if (currentMatch.result === 'ExceptionHold') {<label class="field"><span class="field__label">{{ text('resolutionReason') }}</span><textarea rows="3" maxlength="2000" [(ngModel)]="resolutionReason" [placeholder]="text('resolutionPlaceholder')"></textarea></label><button class="button button--primary" type="button" [disabled]="resolving() || !resolutionReason.trim()" (click)="resolveException()">{{ resolving() ? text('resolving') : text('resolve') }}</button>} @else if (currentMatch.result === 'ResolvedException') {<p class="resolved-stamp">✓ {{ text('resolved') }}</p>}</section>
          </section>
          <section class="ui-surface detail-card variance-card"><div class="card-heading"><div><p class="section-kicker">{{ text('varianceLedger') }}</p><h2>{{ text('varianceTitle') }}</h2></div><span class="count-pill">{{ currentMatch.variances.length }}</span></div>@if (currentMatch.variances.length === 0) {<p class="empty-inline">{{ text('noVariances') }}</p>} @else {<div class="ui-grid-shell"><table class="ui-grid"><thead><tr><th scope="col">{{ text('classification') }}</th><th scope="col">{{ text('details') }}</th><th scope="col" class="numeric">{{ text('expected') }}</th><th scope="col" class="numeric">{{ text('actual') }}</th><th scope="col" class="numeric">{{ text('variance') }}</th></tr></thead><tbody>@for (variance of currentMatch.variances; track $index) {<tr><td><span class="variance-dot" [class.is-exception]="variance.variance !== null && variance.variance !== 0"></span>{{ variance.classification }}</td><td>{{ variance.details || '—' }}</td><td class="numeric">{{ formatNumber(variance.expectedValue) }}</td><td class="numeric">{{ formatNumber(variance.actualValue) }}</td><td class="numeric variance-value">{{ formatNumber(variance.variance) }}</td></tr>}</tbody></table></div>}</section>
          @if (history().length > 0 || audit().length > 0) {<section class="audit-strip"><div><span class="section-kicker">{{ text('history') }}</span><strong>{{ history().length }}</strong></div><div><span class="section-kicker">{{ text('audit') }}</span><strong>{{ audit().length }}</strong></div><div><span class="section-kicker">{{ text('fingerprint') }}</span><code>{{ shortRef(currentMatch.sourceFingerprint) }}</code></div></section>}
        }
      </section>
    }
  `,
  styles: [`
    :host { display: block; }
    .matching-page { --match-ink: #253340; --match-muted: #6d7b84; --match-line: #d7e0df; --match-accent: #c47a32; --match-cool: #2c7180; }
    
    .control-mark { display: flex; align-items: center; gap: .65rem; color: var(--match-cool); font: 700 .72rem/1 system-ui; letter-spacing: .1em; }
    .control-mark i { width: 2rem; height: 1px; background: var(--match-accent); display: block; }
    .matching-panel, .detail-card, .variance-card { padding: clamp(1rem, 2vw, 1.6rem); }
    .matching-grid td, .matching-grid th { white-space: nowrap; }
    .matching-grid small, .record-ref { display: block; color: var(--match-muted); font-size: .76rem; margin-top: .25rem; }
    .match-status { display: inline-flex; align-items: center; gap: .42rem; font-weight: 700; color: var(--match-ink); }
    .match-status > span { width: .52rem; height: .52rem; border-radius: 50%; background: var(--match-muted); }
    .match-status--exactmatch > span, .match-status--resolvedexception > span { background: #43866a; }
    .match-status--withintolerance > span { background: var(--match-accent); }
    .match-status--exceptionhold > span { background: #b84f45; box-shadow: 0 0 0 .22rem rgba(184,79,69,.12); }
    .match-status--notmatchready > span { background: #8a98a0; }
    .match-status--hero { padding: .7rem 1rem; border: 1px solid var(--match-line); background: #f7faf9; border-radius: 999px; }
    .evidence-spine { display: grid; grid-template-columns: 1fr auto 1fr auto 1fr; gap: .85rem; align-items: stretch; margin: 1.4rem 0; }
    .evidence-card { position: relative; min-height: 9.4rem; padding: 1.15rem; border: 1px solid var(--match-line); border-top: 3px solid var(--match-cool); background: linear-gradient(145deg, #fff, #f4f8f7); box-shadow: 0 9px 22px rgba(35,55,62,.06); }
    .evidence-card--invoice { border-top-color: var(--match-accent); background: linear-gradient(145deg, #fff, #fff8ed); }
    .evidence-index { color: var(--match-accent); font: 800 .68rem/1 system-ui; letter-spacing: .16em; }
    .evidence-card h2 { margin: .5rem 0; font-size: 1.05rem; color: var(--match-ink); }
    .evidence-card p:last-child { margin: 0; color: var(--match-muted); font-size: .82rem; }
    .evidence-connector { align-self: center; color: var(--match-accent); font-size: 1.35rem; }
    .match-layout { display: grid; grid-template-columns: minmax(0, 1fr) minmax(18rem, .75fr); gap: 1rem; margin-bottom: 1rem; }
    .policy-card { border-top: 3px solid var(--match-cool); }
    .resolution-card { border-top: 3px solid var(--match-accent); }
    .policy-grid { display: flex; flex-wrap: wrap; gap: .7rem 1.4rem; color: var(--match-muted); font-size: .82rem; }
    .policy-grid strong { color: var(--match-ink); margin-inline-start: .25rem; }
    .policy-evidence { color: var(--match-muted); font-size: .82rem; }
    .card-heading { display: flex; justify-content: space-between; align-items: start; }
    .count-pill { display: grid; place-items: center; min-width: 2rem; height: 2rem; border-radius: 50%; background: #eef3f2; color: var(--match-cool); font-weight: 800; }
    .variance-dot { display: inline-block; width: .52rem; height: .52rem; margin-inline-end: .5rem; border-radius: 50%; background: #43866a; }
    .variance-dot.is-exception { background: #b84f45; }
    .variance-value { font-weight: 800; color: #a3483f; }
    .resolved-stamp { color: #43866a; font-weight: 800; }
    .audit-strip { display: flex; flex-wrap: wrap; gap: 1rem 2rem; margin-top: 1rem; padding: .9rem 1rem; border-inline-start: 3px solid var(--match-accent); background: #f5f8f7; }
    .audit-strip div { display: grid; gap: .2rem; }
    .audit-strip strong { font-size: 1.15rem; color: var(--match-ink); }
    .source-facts { margin: 1rem 0; }
    .source-meta { color: var(--match-muted); font-size: .82rem; }
    .fx-card { margin: 1rem 0; border-top: 3px solid var(--match-accent); }
    .fx-selector { max-width: 42rem; margin: 1rem 0 .35rem; }
    .fx-candidate-list { display: grid; gap: .55rem; margin-top: .9rem; }
    .fx-candidate { display: flex; justify-content: space-between; gap: 1rem; flex-wrap: wrap; padding: .7rem .85rem; border: 1px solid var(--match-line); background: #fffdf8; color: var(--match-muted); font-size: .82rem; }
    .fx-candidate strong { color: var(--match-ink); }
    .fx-applied { display: grid; gap: .2rem; margin-top: 1rem; padding: .8rem .95rem; border-inline-start: 3px solid var(--match-cool); background: #f3f8f7; color: var(--match-ink); }
    .fx-applied span, .fx-applied small { color: var(--match-muted); }
    @media (max-width: 860px) { .evidence-spine { grid-template-columns: 1fr; } .evidence-connector { transform: rotate(90deg); justify-self: center; } .match-layout { grid-template-columns: 1fr; } .control-mark { display: none; } }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { scroll-behavior: auto !important; transition-duration: .01ms !important; animation-duration: .01ms !important; } }
  `],
})
export class PurchaseInvoiceMatchingWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly matchingService = inject(PurchaseInvoiceMatchingService);
  private readonly handoffService = inject(PurchaseInvoiceHandoffService);
  private readonly purchaseOrderService = inject(PurchaseOrderService);
  private readonly goodsReceiptService = inject(GoodsReceiptService);
  private readonly masterDataService = inject(MasterDataService);

  readonly mode = signal<WorkspaceMode>('list');
  readonly records = signal<PurchaseInvoiceMatchListItemResponse[]>([]);
  readonly match = signal<PurchaseInvoiceMatchResponse | null>(null);
  readonly history = signal<PurchaseInvoiceMatchHistoryResponse[]>([]);
  readonly audit = signal<PurchaseInvoiceMatchAuditResponse[]>([]);
  readonly handoff = signal<PurchaseInvoiceHandoffResponse | null>(null);
  readonly purchaseOrder = signal<PurchaseOrderResponse | null>(null);
  readonly receipts = signal<GoodsReceiptResponse[]>([]);
  readonly warehouses = signal<GoodsReceiptWarehouseOptionResponse[]>([]);
  readonly exchangeRates = signal<ExchangeRateRecord[]>([]);
  readonly exchangeRateLoading = signal(false);
  readonly exchangeRateError = signal(false);
  readonly loading = signal(false);
  readonly evaluating = signal(false);
  readonly resolving = signal(false);
  readonly error = signal<SafeUiError | null>(null);
  readonly resultFilter = signal('');
  resolutionReason = '';
  readonly results: PurchaseInvoiceMatchResult[] = ['NotMatchReady', 'ExactMatch', 'WithinTolerance', 'ExceptionHold', 'ResolvedException'];
  selectedExchangeRateId = '';

  gridColumns(): DataGridColumn<PurchaseInvoiceMatchListItemResponse>[] { return [
    { key: 'purchaseOrder', label: this.text('po'), value: (record) => this.shortRef(record.purchaseOrderId), secondaryText: (record) => record.lifecycle, link: (record) => `/app/procurement/invoice-matching/${record.id}`, filter: 'text' },
    { key: 'handoff', label: this.text('handoff'), value: (record) => this.shortRef(record.purchaseInvoiceHandoffId), filter: 'text' },
    { key: 'result', label: this.text('result'), value: (record) => record.result, display: (record) => this.resultLabel(record.result), badge: true, filter: 'select', filterOptions: this.results.map((result) => ({ value: result, label: this.resultLabel(result) })) },
    { key: 'variances', label: this.text('variances'), value: (record) => record.varianceCount, filter: 'number-range', align: 'end' },
    { key: 'evaluated', label: this.text('evaluated'), value: (record) => record.evaluatedAt, display: (record) => this.formatDateTime(record.evaluatedAt), filter: 'date-range' },
  ]; }

  gridActions(): readonly { key: string; label: string }[] { return [{ key: 'view', label: this.text('back') }]; }
  onGridAction(event: { row: PurchaseInvoiceMatchListItemResponse }): void { void this.router.navigate(['/app/procurement/invoice-matching', event.row.id]); }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = params.get('id');
      this.mode.set(id ? 'detail' : 'list');
      if (id) void this.loadDetail(id); else void this.loadList();
    });
  }

  text(key: string): string {
    const copy: Record<string, [string, string]> = {
      sourceFacts: ['Source facts', 'حقائق المصدر'], declaredLines: ['Supplier-declared invoice lines', 'بنود فاتورة المورد المعلنة'], product: ['Product', 'المنتج'], uom: ['UOM', 'وحدة القياس'], quantity: ['Quantity', 'الكمية'], unitPrice: ['Unit price', 'سعر الوحدة'], warehouse: ['Warehouse', 'المستودع'], supplier: ['Supplier', 'المورد'], goodsReceipt: ['Goods receipt', 'إيصال استلام'], authorizedWarehouse: ['Authorized warehouse', 'المستودع المصرح به'], resolutionPolicy: ['Resolution policy', 'سياسة الحل'],
      kicker: ['CONTROLLED RECONCILIATION', 'مطابقة مضبوطة'], title: ['Three-way matching', 'المطابقة الثلاثية'], lead: ['A source-bound control room for purchase order, accepted receipt, and supplier invoice evidence.', 'مساحة رقابية تربط أمر الشراء والاستلام المقبول ودليل فاتورة المورد.'], boundary: ['Evaluation is evidence-only. It does not post AP, GL, stock, tax, payment, or external submissions.', 'التقييم أدلة فقط ولا ينشئ قيوداً أو حركات مخزون أو ضرائب أو مدفوعات أو إرسالاً خارجياً.'], resultFilter: ['Decision', 'القرار'], allResults: ['All decisions', 'كل القرارات'], filterNote: ['Only current and superseded evaluations visible to this Tenant are listed.', 'تظهر فقط تقييمات هذا العميل، الحالية والسابقة.'], loading: ['Loading matching ledger', 'جار تحميل سجل المطابقة'], loadFailed: ['Matching ledger unavailable', 'تعذر تحميل سجل المطابقة'], emptyTitle: ['No evaluations yet', 'لا توجد تقييمات بعد'], emptyLead: ['Evaluate an invoice handoff once declared supplier invoice evidence is captured.', 'قيّم تسليم الفاتورة بعد تسجيل دليل فاتورة المورد.'], po: ['Purchase order', 'أمر الشراء'], handoff: ['Handoff', 'التسليم'], result: ['Decision', 'القرار'], variances: ['Variances', 'الفروقات'], evaluated: ['Evaluated', 'وقت التقييم'], back: ['Back to matching', 'العودة للمطابقة'], detailLead: ['Immutable source lineage', 'سلسلة مصادر غير قابلة للتغيير'], actions: ['Matching actions', 'إجراءات المطابقة'], evaluating: ['Evaluating…', 'جار التقييم…'], reevaluate: ['Evaluate current sources', 'تقييم المصادر الحالية'], purchaseOrder: ['Purchase order', 'أمر الشراء'], acceptedReceipt: ['Accepted goods receipt', 'الاستلام المقبول'], supplierInvoice: ['Supplier invoice evidence', 'دليل فاتورة المورد'], versionCaptured: ['Captured version', 'الإصدار المسجل'], receiptBasis: ['Accepted quantities', 'الكميات المقبولة'], handoffVersion: ['Handoff version', 'إصدار التسليم'], evidenceVersion: ['Evidence version', 'إصدار الدليل'], evidenceMissing: ['Evidence not captured', 'لم يسجل الدليل'], missing: ['Missing', 'مفقود'], policy: ['Tolerance policy', 'سياسة السماح'], policyVersion: ['Version', 'الإصدار'], amountTolerance: ['Amount tolerance', 'سماح المبلغ'], quantityTolerance: ['Quantity tolerance', 'سماح الكمية'], policyNote: ['The effective policy is stored with this evaluation. A later policy change cannot rewrite this decision.', 'تحفظ السياسة الفعالة مع التقييم ولا تغير السياسة اللاحقة القرار السابق.'], resolution: ['Exception resolution', 'حل الاستثناء'], exceptionTitle: ['Controlled exception hold', 'حجز استثناء مضبوط'], decisionTitle: ['Decision record', 'سجل القرار'], resolutionNote: ['A resolution never changes source documents; it records a separately authorized decision.', 'الحل لا يغير مستندات المصدر بل يسجل قراراً مصرحاً به بشكل منفصل.'], resolutionReason: ['Resolution reason', 'سبب الحل'], resolutionPlaceholder: ['Explain the controlled exception and supporting evidence…', 'اشرح الاستثناء المضبوط والدليل الداعم…'], resolving: ['Resolving…', 'جار الحل…'], resolve: ['Resolve exception', 'حل الاستثناء'], resolved: ['Resolved by authorized review', 'تم الحل بمراجعة مصرح بها'], varianceLedger: ['Variance ledger', 'سجل الفروقات'], varianceTitle: ['Line-by-line decision evidence', 'أدلة القرار بنداً بنداً'], noVariances: ['No variance recorded. The sources were exactly comparable.', 'لا توجد فروقات مسجلة. المصادر قابلة للمقارنة بدقة.'], classification: ['Check', 'الفحص'], details: ['Detail', 'التفصيل'], expected: ['Expected', 'المتوقع'], actual: ['Actual', 'الفعلي'], variance: ['Variance', 'الفرق'], history: ['History events', 'أحداث السجل'], audit: ['Audit events', 'أحداث التدقيق'], fingerprint: ['Source fingerprint', 'بصمة المصدر'], resolvedException: ['Resolved exception', 'استثناء محلول'], exceptionHold: ['Exception hold', 'حجز استثناء'], notMatchReady: ['Not match-ready', 'غير جاهز للمطابقة'], exactMatch: ['Exact match', 'مطابقة تامة'], withinTolerance: ['Within tolerance', 'ضمن السماح'], superseded: ['Superseded', 'مستبدل'], current: ['Current', 'حالي']
    };
    Object.assign(copy, {
      crossCurrency: ['CROSS-CURRENCY CONTROL', '\u062a\u062d\u0643\u0645 \u0627\u0644\u0639\u0645\u0644\u0627\u062a \u0627\u0644\u0645\u062a\u0639\u062f\u062f\u0629'],
      exchangeRateReference: ['Exchange Rate reference', '\u0645\u0631\u062c\u0639 \u0633\u0639\u0631 \u0627\u0644\u0635\u0631\u0641'],
      crossCurrencyLead: ['Select a compatible MESP-120 identity. The server selects the effective version from the immutable supplier invoice date.', '\u0627\u062e\u062a\u0631 \u0647\u0648\u064a\u0629 MESP-120 \u0627\u0644\u0645\u062a\u0648\u0627\u0641\u0642\u0629. \u064a\u062d\u062f\u062f \u0627\u0644\u062e\u0627\u062f\u0645 \u0627\u0644\u0646\u0633\u062e\u0629 \u0627\u0644\u0641\u0639\u0627\u0644\u0629 \u0645\u0646 \u062a\u0627\u0631\u064a\u062e \u0641\u0627\u062a\u0648\u0631\u0629 \u0627\u0644\u0645\u0648\u0631\u062f \u0627\u0644\u062b\u0627\u0628\u062a.'],
      exchangeRateLoading: ['Loading Exchange Rate identities', '\u062c\u0627\u0631\u064d \u062a\u062d\u0645\u064a\u0644 \u0647\u0648\u064a\u0627\u062a \u0623\u0633\u0639\u0627\u0631 \u0627\u0644\u0635\u0631\u0641'],
      selectExchangeRate: ['Select Exchange Rate', '\u0627\u062e\u062a\u0631 \u0633\u0639\u0631 \u0627\u0644\u0635\u0631\u0641'],
      chooseExchangeRate: ['Choose an Exchange Rate identity', '\u0627\u062e\u062a\u0631 \u0647\u0648\u064a\u0629 \u0633\u0639\u0631 \u0635\u0631\u0641'],
      noExchangeRate: ['Currency not comparable / missing Exchange Rate', '\u0627\u0644\u0639\u0645\u0644\u0629 \u063a\u064a\u0631 \u0642\u0627\u0628\u0644\u0629 \u0644\u0644\u0645\u0642\u0627\u0631\u0646\u0629 / \u0633\u0639\u0631 \u0635\u0631\u0641 \u0645\u0641\u0642\u0648\u062f'],
      noExchangeRateLead: ['No active MESP-120 identity has a valid version for this invoice date. Evaluation remains fail-closed.', '\u0644\u0627 \u062a\u0648\u062c\u062f \u0647\u0648\u064a\u0629 MESP-120 \u0645\u0641\u0639\u0644\u0629 \u0628\u0646\u0633\u062e\u0629 \u0635\u0627\u0644\u062d\u0629 \u0644\u062a\u0627\u0631\u064a\u062e \u0647\u0630\u0647 \u0627\u0644\u0641\u0627\u062a\u0648\u0631\u0629. \u064a\u0638\u0644 \u0627\u0644\u062a\u0642\u064a\u064a\u0645 \u0645\u0648\u0642\u0648\u0641\u0627\u064b \u0628\u0634\u0643\u0644 \u0622\u0645\u0646.'],
      invoiceDateMissing: ['The immutable invoice date is missing; cross-currency evaluation will remain Not match-ready.', '\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0641\u0627\u062a\u0648\u0631\u0629 \u0627\u0644\u062b\u0627\u0628\u062a \u0645\u0641\u0642\u0648\u062f؛ \u0633\u064a\u0628\u0642\u0649 \u0627\u0644\u062a\u0642\u064a\u064a\u0645 \u063a\u064a\u0631 \u062c\u0627\u0647\u0632 \u0644\u0644\u0645\u0637\u0627\u0628\u0642\u0629.'],
      serverOwnedSnapshot: ['Rate, scale, version, effective date, and provenance are read-only server evidence.', '\u0627\u0644\u0633\u0639\u0631 \u0648\u0627\u0644\u0645\u0642\u064a\u0627\u0633 \u0648\u0627\u0644\u0646\u0633\u062e\u0629 \u0648\u0627\u0644\u062a\u0627\u0631\u064a\u062e \u0648\u0627\u0644\u0645\u0635\u062f\u0631 \u0623\u062f\u0644\u0629 \u0645\u0646 \u0627\u0644\u062e\u0627\u062f\u0645 \u0641\u0642\u0637.'],
      exchangeRateCandidates: ['Eligible MESP-120 identities', '\u0647\u0648\u064a\u0627\u062a MESP-120 \u0627\u0644\u0645\u0624\u0647\u0644\u0629'],
      appliedExchangeRate: ['Applied server FX snapshot', '\u0644\u0642\u0637\u0629 FX \u0627\u0644\u0645\u0637\u0628\u0642\u0629 \u0645\u0646 \u0627\u0644\u062e\u0627\u062f\u0645'],
      selectionRequiredForComparableCurrency: ['Choose a compatible identity or evaluate to record Not match-ready.', '\u0627\u062e\u062a\u0631 \u0647\u0648\u064a\u0629 \u0645\u062a\u0648\u0627\u0641\u0642\u0629 \u0623\u0648 \u0642\u064a\u0651\u0645 \u0644\u062a\u0633\u062c\u064a\u0644 \u062d\u0627\u0644\u0629 \u063a\u064a\u0631 \u062c\u0627\u0647\u0632\u0629 \u0644\u0644\u0645\u0637\u0627\u0628\u0642\u0629.'],
    });
    const value = copy[key];
    if (!value) return key;
    return this.language.language() === 'ar' ? value[1] : value[0];
  }

  resultLabel(result: PurchaseInvoiceMatchResult): string {
    const key = result.charAt(0).toLowerCase() + result.slice(1);
    return this.text(key);
  }

  resultClass(result: PurchaseInvoiceMatchResult): string { return `match-status--${result.toLowerCase()}`; }
  shortRef(value: string): string { return value ? value.slice(0, 8).toUpperCase() : '—'; }
  orderReference(): string {
    return this.purchaseOrder()?.source.supplierQuotationReference || this.text('purchaseOrder');
  }
  invoiceReference(): string {
    return this.handoff()?.declaredEvidence?.supplierInvoiceReference
      || this.handoff()?.supplierInvoiceReference
      || this.text('missing');
  }
  supplierLabel(): string {
    const handoff = this.handoff();
    return handoff ? `${handoff.supplierName} (${handoff.supplierCode})` : this.text('supplier');
  }
  currencyLabel(): string {
    return this.handoff()?.declaredEvidence?.currencyCode || this.handoff()?.currencyCode || '—';
  }
  declaredCurrencyCode(): string { return this.handoff()?.declaredEvidence?.currencyCode || this.handoff()?.currencyCode || ''; }
  purchaseOrderCurrencyCode(): string { return this.purchaseOrder()?.source.currency.code || ''; }
  invoiceDate(): string | null { return this.handoff()?.declaredEvidence?.supplierInvoiceDate || this.handoff()?.supplierInvoiceDate || null; }
  isCrossCurrency(): boolean {
    const declared = this.declaredCurrencyCode();
    const purchaseOrder = this.purchaseOrderCurrencyCode();
    return Boolean(declared && purchaseOrder && declared.toUpperCase() !== purchaseOrder.toUpperCase());
  }
  eligibleExchangeRates(): ExchangeRateRecord[] { return this.exchangeRates(); }
  selectExchangeRate(exchangeRateId: string): void {
    this.selectedExchangeRateId = this.eligibleExchangeRates().some(rate => rate.id === exchangeRateId) ? exchangeRateId : '';
  }
  exchangeRateLabel(rate: ExchangeRateRecord): string { return `${rate.sourceCurrencyCode} → ${rate.targetCurrencyCode} · ${rate.lifecycleState}`; }
  exchangeRateWindows(rate: ExchangeRateRecord): string {
    return rate.versions
      .map(version => `v${version.versionNumber} · ${version.effectiveFrom}–${version.effectiveTo || 'current'} · ${version.provenance}${version.sourceNotes ? ` · ${version.sourceNotes}` : ''}`)
      .join(' | ');
  }

  receiptReferences(): string {
    const receipts = this.receipts();
    return receipts.length
      ? receipts.map((receipt, index) => receipt.referenceNote || `${this.text('goodsReceipt')} ${index + 1}`).join(', ')
      : this.text('receiptBasis');
  }
  productLabel(purchaseOrderLineId: string): string {
    const line = this.handoff()?.lines.find(item => item.purchaseOrderLineId === purchaseOrderLineId);
    return line ? `${line.productSku} · ${line.productName}` : this.text('product');
  }
  uomLabel(purchaseOrderLineId: string): string {
    return this.handoff()?.lines.find(item => item.purchaseOrderLineId === purchaseOrderLineId)?.unitOfMeasureCode || '—';
  }
  warehouseLabel(): string {
    const labels = [...new Set(this.receipts().map(receipt => {
      const option = this.warehouses().find(item => item.warehouseId === receipt.warehouseId);
      return option ? `${option.code} · ${option.name}` : null;
    }).filter((label): label is string => label !== null))];
    return labels.length ? labels.join(', ') : this.text('authorizedWarehouse');
  }
  setResultFilter(value: string): void { this.resultFilter.set(value); void this.loadList(); }

  async loadList(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try { this.records.set(await this.matchingService.list(undefined, this.resultFilter() || undefined)); } catch (error) { this.error.set(toSafeUiError(error)); } finally { this.loading.set(false); }
  }

  async loadDetail(id: string): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const [match, history, audit] = await Promise.all([this.matchingService.get(id), this.matchingService.history(id), this.matchingService.audit(id)]);
      this.match.set(match); this.history.set(history); this.audit.set(audit); this.resolutionReason = '';
      const handoff = await this.tryLoad(this.handoffService.get(match.purchaseInvoiceHandoffId));
      this.handoff.set(handoff);
      this.purchaseOrder.set(await this.tryLoad(this.purchaseOrderService.get(match.purchaseOrderId)));
      this.selectedExchangeRateId = match.appliedExchangeRate?.exchangeRateId || '';
      await this.refreshExchangeRateChoices();
      if (handoff) {
        const receiptIds = [...new Set(handoff.sources.map(source => source.goodsReceiptId))];
        const receipts = await Promise.all(receiptIds.map(receiptId => this.tryLoad(this.goodsReceiptService.get(receiptId))));
        this.receipts.set(receipts.filter((receipt): receipt is GoodsReceiptResponse => receipt !== null));
      } else {
        this.receipts.set([]);
      }
      const warehouses = await this.tryLoad(this.goodsReceiptService.warehouses());
      this.warehouses.set(warehouses ?? []);
    } catch (error) { this.error.set(toSafeUiError(error)); } finally { this.loading.set(false); }
  }

  private async refreshExchangeRateChoices(): Promise<void> {
    this.exchangeRates.set([]);
    this.exchangeRateError.set(false);
    if (!this.isCrossCurrency()) {
      this.selectedExchangeRateId = '';
      return;
    }

    this.exchangeRateLoading.set(true);
    try {
      const records = await firstValueFrom(this.masterDataService.list('exchange-rates'));
      const source = this.declaredCurrencyCode().toUpperCase();
      const target = this.purchaseOrderCurrencyCode().toUpperCase();
      const effectiveOn = this.invoiceDate();
      const eligible = records
        .filter((record): record is ExchangeRateRecord => this.isExchangeRateRecord(record))
        .filter(record => record.lifecycleState === 'Active'
          && record.sourceCurrencyCode.toUpperCase() === source
          && record.targetCurrencyCode.toUpperCase() === target
          && Boolean(effectiveOn)
          && record.versions.some(version => {
            return version.rate > 0
              && version.rateScale > 0
              && version.sourceCurrencyCode.toUpperCase() === source
              && version.targetCurrencyCode.toUpperCase() === target
              && version.effectiveFrom <= effectiveOn!
              && (!version.effectiveTo || effectiveOn! <= version.effectiveTo);
          }));
      this.exchangeRates.set(eligible);
      if (this.selectedExchangeRateId && !eligible.some(rate => rate.id === this.selectedExchangeRateId)) {
        this.selectedExchangeRateId = '';
      }
    } catch {
      this.exchangeRateError.set(true);
    } finally {
      this.exchangeRateLoading.set(false);
    }
  }

  private isExchangeRateRecord(record: MasterDataRecord): record is ExchangeRateRecord {
    return 'sourceCurrencyCode' in record && 'targetCurrencyCode' in record && 'versions' in record;
  }

  private async tryLoad<T>(request: import('rxjs').Observable<T>): Promise<T | null> {
    try { return await firstValueFrom(request); } catch { return null; }
  }

  async evaluateCurrent(): Promise<void> {
    const current = this.match(); if (!current) return;
    this.evaluating.set(true); this.error.set(null);
    try {
      const handoff = await firstValueFrom(this.handoffService.get(current.purchaseInvoiceHandoffId));
      this.handoff.set(handoff);
      await this.refreshExchangeRateChoices();
      const payload: PurchaseInvoiceMatchEvaluateRequest = this.isCrossCurrency() && this.selectedExchangeRateId
        ? { exchangeRateReference: { exchangeRateId: this.selectedExchangeRateId } }
        : {};
      const updated = await this.matchingService.evaluate(current.purchaseInvoiceHandoffId, handoff.version, payload);
      this.match.set(updated); this.history.set(await this.matchingService.history(updated.id)); this.audit.set(await this.matchingService.audit(updated.id));
    } catch (error) { this.error.set(toSafeUiError(error)); } finally { this.evaluating.set(false); }
  }

  async resolveException(): Promise<void> {
    const current = this.match(); if (!current || !this.resolutionReason.trim()) return;
    this.resolving.set(true); this.error.set(null);
    try { const updated = await this.matchingService.resolve(current.id, current.version, { reason: this.resolutionReason.trim() }); this.match.set(updated); this.resolutionReason = ''; this.history.set(await this.matchingService.history(updated.id)); this.audit.set(await this.matchingService.audit(updated.id)); }
    catch (error) { this.error.set(toSafeUiError(error)); } finally { this.resolving.set(false); }
  }

  formatNumber(value: number | null): string { return value === null || value === undefined ? '—' : value.toLocaleString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { maximumFractionDigits: 4 }); }
  formatDateTime(value: string): string { const date = new Date(value); return Number.isNaN(date.getTime()) ? value : date.toLocaleString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' }); }
  errorText(error: SafeUiError): string { return error.status === 403 ? this.language.text('accessDenied') : error.status === 409 ? this.language.text('prConcurrencyConflictError') : this.language.text('requestError'); }
}
