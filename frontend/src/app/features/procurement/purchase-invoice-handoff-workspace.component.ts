import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CurrencyAmountComponent } from '../../shared/ui/currency-amount.component';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService } from '../../core/i18n/language.service';
import {
  PurchaseInvoiceHandoffAuditResponse,
  PurchaseInvoiceHandoffCreateRequest,
  PurchaseInvoiceHandoffEligibleSourceResponse,
  PurchaseInvoiceHandoffHistoryResponse,
  PurchaseInvoiceHandoffListItemResponse,
  PurchaseInvoiceHandoffLineResponse,
  PurchaseInvoiceHandoffResponse,
  PurchaseInvoiceHandoffSourceResponse,
  PurchaseInvoiceHandoffStatus,
} from './purchase-invoice-handoff.model';
import { PurchaseInvoiceHandoffService } from './purchase-invoice-handoff.service';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { TabsComponent } from '../../shared/ui/tabs.component';
import type { AppTab } from '../../shared/ui/tabs.component';

type WorkspaceMode = 'list' | 'create' | 'detail';
type DetailTab = 'summary' | 'lines' | 'sources' | 'history' | 'audit';

interface CreateHandoffLineDraft {
  goodsReceiptId: string;
  goodsReceiptLineId: string;
  purchaseOrderLineId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  receivedDate: string;
  acceptedQuantity: number;
  alreadyHandedOffQuantity: number;
  remainingHandoffQuantity: number;
  unitPrice: number;
  taxRatePercentage: number | null;
  handoffQuantity: number;
}

@Component({
  selector: 'app-purchase-invoice-handoff-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink, DataGridComponent, PageHeaderComponent, CurrencyAmountComponent, TabsComponent],
  template: `
    @if (mode() === 'list') {
      <section class="ui-page invoice-handoff-page" data-testid="invoice-handoff-list">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ pihText('invoiceHandoffKicker') }}</p>
            <h1>{{ pihText('invoiceHandoffs') }}</h1>
            <p>{{ pihText('invoiceHandoffsLead') }}</p>
          </div>
          <div page-header-actions><a class="button button--primary" routerLink="/app/procurement/invoice-handoffs/new" data-testid="new-invoice-handoff">＋ {{ pihText('newInvoiceHandoff') }}</a></div>
        </app-page-header>
        <div class="boundary-note" role="note"><span aria-hidden="true">◇</span><span>{{ pihText('invoiceHandoffBoundary') }}</span></div>

        @if (loading()) {
          <section class="ui-surface state-card" aria-live="polite"><span class="spinner" aria-hidden="true"></span><h2>{{ pihText('loadingInvoiceHandoffs') }}</h2></section>
        } @else if (error(); as currentError) {
          <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ pihText('invoiceHandoffListLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadList()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface ledger-panel">
            <div class="filter-toolbar">
              <label class="filter-search"><span aria-hidden="true">⌕</span><input type="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="pihText('invoiceHandoffSearch')" /><span class="sr-only">{{ pihText('invoiceHandoffSearch') }}</span></label>
              <label class="filter-field"><span>{{ pihText('invoiceHandoffStatusFilter') }}</span><select [value]="statusFilter()" (change)="setStatusFilter($any($event.target).value)"><option value="">{{ pihText('invoiceHandoffAllStatuses') }}</option>@for (status of statuses; track status) {<option [value]="status">{{ statusLabel(status) }}</option>}</select></label>
              <p class="filter-note">{{ pihText('invoiceHandoffFilterNote') }}</p>
            </div>
            @if (filteredRecords().length === 0) {
              <div class="empty-ledger"><span aria-hidden="true">◌</span><h2>{{ pihText('noInvoiceHandoffs') }}</h2><p>{{ pihText('noInvoiceHandoffsLead') }}</p></div>
            } @else {
              <ng-template #handoffAmount let-row>
                <app-currency-amount [amount]="row.totalHandoffAmount" [currencyCode]="row.currencyCode" [locale]="language.language()" />
              </ng-template>
              <app-data-grid [rows]="filteredRecords()" [columns]="handoffColumns(handoffAmount)" [language]="language.language()" [clientPaging]="true" [showPager]="true" [scopeLabel]="gridScopeLabel()" [caption]="pihText('invoiceHandoffs')" [countLabel]="pihText('invoiceHandoffs')" [filterInputLabel]="pihText('invoiceHandoffSearch')" [previousPageLabel]="language.text('previous')" [nextPageLabel]="language.text('next')" />
            }
          </section>
        }
      </section>
    }

    @if (mode() === 'create') {
      <section class="ui-page invoice-handoff-page" data-testid="invoice-handoff-create">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ pihText('invoiceHandoffKicker') }}</p>
            <h1>{{ pihText('createInvoiceHandoff') }}</h1>
            <p>{{ pihText('invoiceHandoffCreateLead') }}</p>
          </div>
          <div page-header-actions><a class="button button--secondary" routerLink="/app/procurement/invoice-handoffs">{{ pihText('backToInvoiceHandoffs') }}</a></div>
        </app-page-header>
        <div class="boundary-note" role="note"><span aria-hidden="true">◇</span><span>{{ pihText('invoiceHandoffFinanceRule') }}</span></div>

        @if (loading()) {
          <section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ pihText('loadingInvoiceHandoffSources') }}</h2></section>
        } @else if (error(); as currentError) {
          <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ pihText('invoiceHandoffSourceLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadCreatePrerequisites()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface form-card">
            <div class="create-meta-grid">
              <label class="field">
                <span class="field__label">{{ pihText('invoiceHandoffSourceField') }} *</span>
                <select [ngModel]="selectedSourceId()" (ngModelChange)="onSelectSource($event)" data-testid="invoice-handoff-source">
                  <option value="">{{ pihText('selectInvoiceHandoffSource') }}</option>
                  @for (source of eligibleSources(); track source.purchaseOrderId) {
                    <option [value]="source.purchaseOrderId">{{ source.supplierName }} ({{ source.supplierCode }}) · PO {{ source.purchaseOrderId.substring(0, 8) }} · {{ source.currencyCode }}</option>
                  }
                </select>
              </label>

              <label class="field">
                <span class="field__label">{{ pihText('supplierInvoiceReference') }} *</span>
                <input type="text" maxlength="256" [(ngModel)]="createInvoiceRef" [placeholder]="pihText('supplierInvoiceRefPlaceholder')" data-testid="invoice-handoff-ref" />
              </label>

              <label class="field">
                <span class="field__label">{{ pihText('supplierInvoiceDate') }} *</span>
                <input type="date" [(ngModel)]="createInvoiceDate" data-testid="invoice-handoff-date" />
              </label>
            </div>

            <label class="field">
              <span class="field__label">{{ pihText('invoiceHandoffNotes') }}</span>
              <textarea [(ngModel)]="createNotes" rows="2" [placeholder]="pihText('invoiceHandoffNotesPlaceholder')"></textarea>
            </label>

            @if (createLines.length > 0) {
              <section class="source-lines-section">
                <p class="section-kicker">{{ pihText('invoiceHandoffLines') }}</p>
                <h2>{{ pihText('invoiceHandoffLineEntryTitle') }}</h2>
                <p class="detail-copy">{{ pihText('invoiceHandoffLineEntryLead') }}</p>

                <ng-template #createAmount let-line let-column="column">
                  @if (column.key === 'unitPrice') {
                    <app-currency-amount [amount]="line.unitPrice" [currencyCode]="selectedCurrencyCode()" [locale]="language.language()" />
                  } @else {
                    <app-currency-amount [amount]="calculateLineTotal(line)" [currencyCode]="selectedCurrencyCode()" [locale]="language.language()" />
                  }
                </ng-template>
                <app-data-grid [rows]="createLines" [columns]="createLineColumns(createAmount)" [rowActionsTemplate]="createLineActions" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="pihText('invoiceHandoffLineEntryTitle')" [countLabel]="pihText('invoiceHandoffLines')" [rowActionsLabel]="pihText('handoffQty')">
                  <ng-template #createLineActions let-line>
                    <label class="handoff-quantity-editor"><span>{{ pihText('handoffQty') }} *</span><input class="table-input numeric" type="number" min="0" [max]="line.remainingHandoffQuantity" step="0.000001" [(ngModel)]="line.handoffQuantity" (ngModelChange)="onLineQuantityChange()" [ngModelOptions]="{standalone: true}" [attr.aria-label]="pihText('handoffQty')" /></label>
                  </ng-template>
                </app-data-grid>

                <div class="summary-box">
                  <div class="summary-row"><span>{{ pihText('subtotal') }}:</span><strong><app-currency-amount [amount]="computedSubtotal()" [currencyCode]="selectedCurrencyCode()" [locale]="language.language()" /></strong></div>
                  <div class="summary-row"><span>{{ pihText('taxTotal') }}:</span><strong><app-currency-amount [amount]="computedTaxTotal()" [currencyCode]="selectedCurrencyCode()" [locale]="language.language()" /></strong></div>
                  <div class="summary-row summary-row--grand"><span>{{ pihText('grandTotal') }}:</span><strong><app-currency-amount [amount]="computedGrandTotal()" [currencyCode]="selectedCurrencyCode()" [locale]="language.language()" /></strong></div>
                </div>

                @if (validationError()) {
                  <div class="inline-error" role="alert">{{ validationError() }}</div>
                }

                <div class="form-actions">
                  <button class="button button--primary" type="button" [disabled]="saving() || !canSubmitCreate()" (click)="createHandoff()" data-testid="submit-invoice-handoff">
                    {{ saving() ? pihText('saving') : pihText('recordInvoiceHandoff') }}
                  </button>
                </div>
              </section>
            } @else {
              <div class="empty-inline">{{ pihText('invoiceHandoffSelectSourceLead') }}</div>
            }
          </section>
        }
      </section>
    }

    @if (mode() === 'detail' && handoff(); as currentHandoff) {
      <section class="ui-page invoice-handoff-page" data-testid="invoice-handoff-detail">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ pihText('invoiceHandoffKicker') }}</p>
            <h1>{{ currentHandoff.supplierInvoiceReference }}</h1>
            <p>{{ currentHandoff.supplierName }} ({{ currentHandoff.supplierCode }}) · {{ formatDate(currentHandoff.supplierInvoiceDate) }} · {{ currentHandoff.currencyCode }}</p>
          </div>
          <div page-header-actions>
            <span class="status-badge" [class]="statusClass(currentHandoff.status)"><span aria-hidden="true"></span>{{ statusLabel(currentHandoff.status) }}</span>
            <a class="button button--secondary" routerLink="/app/procurement/invoice-handoffs">{{ pihText('backToInvoiceHandoffs') }}</a>
            @if (currentHandoff.canCancel) { <button class="button button--danger" type="button" (click)="openCancelDialog()" data-testid="cancel-invoice-handoff">{{ pihText('cancelInvoiceHandoff') }}</button> }
          </div>
        </app-page-header>

        @if (currentHandoff.status === 'Cancelled') {
          <section class="boundary-note terminal-recovery-note" role="note">
            <strong>{{ pihText('invoiceHandoffCancelledNotice') }}</strong>
            <span>{{ currentHandoff.cancellationReason || pihText('noCancellationReason') }} ({{ formatDateTime(currentHandoff.cancelledAt ?? '') }})</span>
          </section>
        }

        @if (error(); as currentError) {
          <div class="inline-error" role="alert">{{ errorText(currentError) }}</div>
        }

        <app-tabs [tabs]="detailTabItems()" [selected]="activeTab()" [ariaLabel]="pihText('invoiceHandoffSections')" (selectedChange)="selectTab($event)" />

        @if (activeTab() === 'summary') {
          <section class="detail-layout" role="tabpanel" [attr.aria-labelledby]="tabId('summary')">
            <section class="ui-surface detail-card">
              <p class="section-kicker">{{ pihText('handoffDetails') }}</p>
              <h2>{{ pihText('invoiceHandoffSummaryTitle') }}</h2>
              <dl class="fact-grid">
                <div><dt>{{ pihText('invoiceHandoffSupplierColumn') }}</dt><dd>{{ currentHandoff.supplierName }} · {{ currentHandoff.supplierCode }}</dd></div>
                <div><dt>{{ pihText('supplierInvoiceReference') }}</dt><dd>{{ currentHandoff.supplierInvoiceReference }}</dd></div>
                <div><dt>{{ pihText('supplierInvoiceDate') }}</dt><dd>{{ formatDate(currentHandoff.supplierInvoiceDate) }}</dd></div>
                <div><dt>{{ pihText('invoiceHandoffPurchaseOrder') }}</dt><dd><code>{{ currentHandoff.purchaseOrderId }}</code></dd></div>
                <div><dt>{{ pihText('invoiceHandoffCurrencyColumn') }}</dt><dd>{{ currentHandoff.currencyCode }}</dd></div>
                <div><dt>{{ pihText('invoiceHandoffCreatedAt') }}</dt><dd>{{ formatDateTime(currentHandoff.createdAt) }}</dd></div>
                <div><dt>{{ pihText('invoiceHandoffVersion') }}</dt><dd><code>{{ currentHandoff.version }}</code></dd></div>
              </dl>
              @if (currentHandoff.notes) {
                <p class="detail-copy">{{ currentHandoff.notes }}</p>
              }
            </section>
          </section>
        } @else if (activeTab() === 'lines') {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('lines')">
            <p class="section-kicker">{{ pihText('invoiceHandoffLines') }}</p>
            <h2>{{ pihText('invoiceHandoffLinesTitle') }}</h2>
            <ng-template #detailAmount let-line let-column="column">
              @switch (column.key) {
                @case ('unitPrice') { <app-currency-amount [amount]="line.unitPrice" [currencyCode]="currentHandoff.currencyCode" [locale]="language.language()" /> }
                @case ('taxAmount') { @if (line.taxAmount !== null) { <app-currency-amount [amount]="line.taxAmount" [currencyCode]="currentHandoff.currencyCode" [locale]="language.language()" /> } @else { — } }
                @case ('lineAmount') { <app-currency-amount [amount]="line.lineAmount" [currencyCode]="currentHandoff.currencyCode" [locale]="language.language()" /> }
              }
            </ng-template>
            <app-data-grid [rows]="currentHandoff.lines" [columns]="detailLineColumns(currentHandoff.currencyCode, detailAmount)" [language]="language.language()" [clientPaging]="true" [showPager]="true" [caption]="pihText('invoiceHandoffLinesTitle')" [countLabel]="pihText('invoiceHandoffLines')" />
          </section>
        } @else if (activeTab() === 'sources') {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('sources')">
            <p class="section-kicker">{{ pihText('sourceReceipts') }}</p>
            <h2>{{ pihText('sourceReceiptLineageTitle') }}</h2>
            <app-data-grid [rows]="currentHandoff.sources" [columns]="sourceColumns" [language]="language.language()" [clientPaging]="true" [showPager]="true" [caption]="pihText('sourceReceiptLineageTitle')" [countLabel]="pihText('invoiceHandoffLines')" />
          </section>
        } @else if (activeTab() === 'history') {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('history')">
            <p class="section-kicker">{{ pihText('lifecycleHistory') }}</p>
            <h2>{{ pihText('lifecycleHistory') }}</h2>
            @if (history().length === 0) {
              <div class="empty-inline">{{ pihText('noHistory') }}</div>
            } @else {
              <ol class="timeline">
                @for (entry of history(); track entry.evidenceId) {
                  <li>
                    <strong>{{ statusLabel(entry.fromStatus) }} → {{ statusLabel(entry.toStatus) }}</strong>
                    <small>{{ entry.action }} · {{ formatDateTime(entry.occurredAt) }}</small>
                    <p>{{ entry.reason || pihText('notAvailable') }}</p>
                  </li>
                }
              </ol>
            }
          </section>
        } @else {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('audit')">
            <p class="section-kicker">{{ pihText('auditEvidence') }}</p>
            <h2>{{ pihText('auditEvidence') }}</h2>
            @if (audit().length === 0) {
              <div class="empty-inline">{{ pihText('noAudit') }}</div>
            } @else {
              <div class="audit-list">
                @for (entry of audit(); track entry.evidenceId) {
                  <article>
                    <strong>{{ entry.operationId }}</strong>
                    <small>{{ entry.decision }} · {{ formatDateTime(entry.occurredAt) }}</small>
                    <p>{{ entry.reason || entry.afterSummary || pihText('notAvailable') }}</p>
                    <code>{{ entry.correlationId }}</code>
                  </article>
                }
              </div>
            }
          </section>
        }
      </section>
    }

    @if (showCancelDialog()) {
      <div class="dialog-backdrop" role="presentation" (click)="closeCancelDialog()">
        <section class="action-dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-handoff-title" tabindex="-1" (click)="$event.stopPropagation()">
          <p class="section-kicker">{{ pihText('invoiceHandoffKicker') }}</p>
          <h2 id="cancel-handoff-title">{{ pihText('cancelInvoiceHandoffTitle') }}</h2>
          <p>{{ pihText('cancelInvoiceHandoffLead') }}</p>
          <label class="field">
            <span class="field__label">{{ pihText('cancellationReason') }} *</span>
            <textarea [(ngModel)]="cancelReason" rows="3" [placeholder]="pihText('cancellationReasonHint')"></textarea>
          </label>
          <div class="dialog-actions">
            <button class="button button--secondary" type="button" (click)="closeCancelDialog()">{{ language.text('cancel') }}</button>
            <button class="button button--danger" type="button" [disabled]="saving() || !cancelReason.trim()" (click)="confirmCancel()">
              {{ saving() ? pihText('saving') : pihText('confirmCancel') }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
  styles: `:host { display: block; }
    .boundary-note { display: flex; align-items: flex-start; gap: .6rem; border-inline-start: 3px solid var(--support); padding: .72rem .9rem; color: var(--ink-muted); background: var(--support-soft); font-size: .76rem; line-height: 1.5; }
    .boundary-note > span:first-child { color: var(--support); font-size: 1rem; }
    .state-card { display: grid; justify-items: start; align-content: center; gap: .55rem; min-height: 12rem; }
    .state-card h2, .state-card p { margin: 0; }
    .state-card p, .detail-copy, .empty-inline { color: var(--ink-muted); font-size: .8rem; line-height: 1.5; }
    .state-card--error, .inline-error { border-color: color-mix(in srgb, var(--danger) 32%, var(--line)); }
    .inline-error { margin-block: .9rem; border: 1px solid; border-radius: var(--radius-sm); padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .78rem; }
    .spinner { width: 2rem; height: 2rem; border: 3px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: pih-spin 1s linear infinite; }
    @keyframes pih-spin { to { transform: rotate(360deg); } }
    .ledger-panel { padding: 0; overflow: hidden; }
    .filter-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: .7rem; padding: .85rem 1rem; border-bottom: 1px solid var(--line); background: color-mix(in srgb, var(--accent-soft) 70%, var(--surface-raised)); }
    .filter-field { display: grid; gap: .25rem; min-width: 12rem; color: var(--ink-muted); font-size: .64rem; font-weight: 900; letter-spacing: .06em; text-transform: uppercase; }
    .filter-field select { text-transform: none; letter-spacing: normal; }
    .filter-note { flex: 1 1 100%; margin: 0; color: var(--ink-muted); font-size: .66rem; }
    .currency-badge { display: inline-flex; border: 1px solid color-mix(in srgb, var(--accent-strong) 28%, var(--line)); border-radius: 99px; padding: .24rem .45rem; color: var(--accent-strong); background: var(--accent-soft); font-size: .63rem; font-weight: 900; }
    .status-badge { display: inline-flex; align-items: center; gap: .35rem; border: 1px solid var(--line); border-radius: 99px; padding: .28rem .5rem; color: var(--ink-muted); background: var(--surface); font-size: .63rem; font-weight: 900; white-space: nowrap; }
    .status-badge > span { width: .4rem; height: .4rem; border-radius: 50%; background: currentColor; }
    .status-badge--recorded { color: var(--success); background: var(--accent-soft); }
    .status-badge--cancelled { color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .numeric { font-variant-numeric: tabular-nums; }
    .money { color: var(--ink-strong); font-weight: 800; }
    .empty-ledger { display: grid; justify-items: start; align-content: center; gap: .45rem; min-height: 17rem; padding: 2rem; }
    .empty-ledger > span { color: var(--accent-strong); font-size: 2.3rem; }
    .empty-ledger h2, .empty-ledger p { margin: 0; }
    .empty-ledger p { max-width: 34rem; }
    .form-card, .detail-card { display: grid; gap: 1rem; }
    .create-meta-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); gap: .8rem; }
    .field { display: grid; gap: .35rem; color: var(--ink); font-size: .78rem; }
    .field__label { color: var(--ink-muted); font-size: .68rem; font-weight: 900; letter-spacing: .05em; text-transform: uppercase; }
    .field input, .field select, .field textarea, .table-input { width: 100%; box-sizing: border-box; }
    .field textarea { resize: vertical; }
    .form-actions, .dialog-actions { display: flex; flex-wrap: wrap; align-items: center; gap: .55rem; }
    .form-actions { justify-content: flex-end; }
    .source-lines-section { display: grid; gap: 1rem; border: 1px solid var(--line); border-radius: var(--radius-sm); padding: 1rem; background: var(--surface); }
    .section-kicker { color: var(--ink-muted); font-size: .66rem; font-weight: 900; letter-spacing: .1em; text-transform: uppercase; margin: 0; }
    .eyebrow { color: var(--primary); font-size: 12px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; margin: 0; }
    .fact-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .8rem; margin: 0; }
    .fact-grid div { display: grid; gap: .2rem; }
    .fact-grid dt { color: var(--ink-muted); font-size: .66rem; font-weight: 800; }
    .fact-grid dd { margin: 0; color: var(--ink); font-size: .78rem; font-weight: 800; overflow-wrap: anywhere; }
    .table-input { min-width: 6rem; padding: .42rem .45rem; font-size: .72rem; }
    .handoff-quantity-editor { display: grid; min-width: 8rem; gap: .2rem; color: var(--ink-muted); font-size: .62rem; font-weight: 800; }
    .remaining-highlight { color: var(--accent-strong); font-weight: 800; }
    .summary-box { display: grid; justify-content: end; gap: .4rem; border-top: 1px solid var(--line); padding-top: .8rem; }
    .summary-row { display: flex; justify-content: space-between; gap: 2rem; font-size: .8rem; }
    .summary-row--grand { font-size: .95rem; font-weight: 900; color: var(--ink-strong); }
    .timeline, .audit-list { display: grid; gap: .7rem; margin: 0; padding: 0; list-style: none; }
    .timeline li, .audit-list article { border-inline-start: 3px solid var(--accent); padding: .65rem .8rem; background: var(--surface); }
    .timeline strong, .timeline small, .timeline p, .audit-list strong, .audit-list small, .audit-list p { display: block; margin: 0; }
    .timeline small, .audit-list small { margin-top: .25rem; color: var(--ink-muted); font-size: .68rem; }
    .timeline p, .audit-list p { margin-top: .42rem; color: var(--ink-muted); font-size: .75rem; line-height: 1.45; }
    .audit-list code { display: block; margin-top: .45rem; color: var(--ink-muted); font-size: .64rem; overflow-wrap: anywhere; }
    .dialog-backdrop { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 1rem; background: rgb(15 30 28 / .55); }
    .action-dialog { display: grid; gap: .8rem; width: min(100%, 32rem); border: 1px solid var(--line); border-radius: var(--radius-md); padding: 1.2rem; background: var(--surface-raised); box-shadow: 0 1rem 3rem rgb(0 0 0 / .22); }
    .action-dialog h2, .action-dialog p { margin: 0; }
    .action-dialog > p:not(.section-kicker) { color: var(--ink-muted); font-size: .8rem; line-height: 1.5; }
    .dialog-actions { justify-content: flex-end; }
    code { font-family: var(--font-mono); font-size: .66rem; }
  `,
})
export class PurchaseInvoiceHandoffWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(PurchaseInvoiceHandoffService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly mode = signal<WorkspaceMode>('list');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<SafeUiError | null>(null);
  readonly validationError = signal<string | null>(null);
  readonly records = signal<PurchaseInvoiceHandoffListItemResponse[]>([]);
  readonly eligibleSources = signal<PurchaseInvoiceHandoffEligibleSourceResponse[]>([]);
  readonly handoff = signal<PurchaseInvoiceHandoffResponse | null>(null);
  readonly history = signal<PurchaseInvoiceHandoffHistoryResponse[]>([]);
  readonly audit = signal<PurchaseInvoiceHandoffAuditResponse[]>([]);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly selectedSourceId = signal('');
  readonly activeTab = signal<DetailTab>('summary');
  readonly showCancelDialog = signal(false);

  createInvoiceRef = '';
  createInvoiceDate = new Date().toISOString().slice(0, 10);
  createNotes = '';
  createLines: CreateHandoffLineDraft[] = [];
  cancelReason = '';

  readonly statuses: PurchaseInvoiceHandoffStatus[] = ['Recorded', 'Cancelled'];
  readonly tabs: DetailTab[] = ['summary', 'lines', 'sources', 'history', 'audit'];
  detailTabItems(): AppTab[] { return this.tabs.map(tab => ({ id: tab, label: this.tabLabel(tab), tabId: this.tabId(tab) })); }

  readonly filteredRecords = computed(() => {
    const query = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    return this.records().filter((record) => {
      const matchStatus = !status || record.status === status;
      const matchQuery = !query || `${record.supplierName} ${record.supplierCode} ${record.supplierInvoiceReference} ${record.id}`.toLowerCase().includes(query);
      return matchStatus && matchQuery;
    });
  });
  handoffColumns(amountCell: DataGridColumn<PurchaseInvoiceHandoffListItemResponse>['cellTemplate']): DataGridColumn<PurchaseInvoiceHandoffListItemResponse>[] {
    return [
    { key: 'supplierInvoiceReference', label: this.pihText('invoiceHandoffRefColumn'), value: row => row.supplierInvoiceReference, link: row => `/app/procurement/invoice-handoffs/${row.id}`, secondaryText: row => `${row.lineCount} ${this.pihText('invoiceHandoffLines')}`, filter: 'text' },
    { key: 'supplierName', label: this.pihText('invoiceHandoffSupplierColumn'), value: row => row.supplierName, secondaryText: row => row.supplierCode, filter: 'text' },
    { key: 'status', label: this.pihText('invoiceHandoffStatusColumn'), value: row => this.statusLabel(row.status), badge: true, filter: 'select' },
    { key: 'supplierInvoiceDate', label: this.pihText('invoiceHandoffDateColumn'), value: row => row.supplierInvoiceDate, display: row => this.formatDate(row.supplierInvoiceDate), filter: 'date-range' },
    { key: 'currencyCode', label: this.pihText('invoiceHandoffCurrencyColumn'), value: row => row.currencyCode, filter: 'select' },
    { key: 'totalHandoffQuantity', label: this.pihText('invoiceHandoffQtyColumn'), value: row => row.totalHandoffQuantity, display: row => this.formatQuantity(row.totalHandoffQuantity), filter: 'number-range', align: 'end' },
    { key: 'totalHandoffAmount', label: this.pihText('invoiceHandoffAmountColumn'), value: row => row.totalHandoffAmount, display: row => this.formatMoney(row.totalHandoffAmount, row.currencyCode), cellTemplate: amountCell, filter: 'number-range', align: 'end' },
    { key: 'updatedAt', label: this.pihText('invoiceHandoffUpdatedColumn'), value: row => row.updatedAt, display: row => this.formatDateTime(row.updatedAt), filter: 'date-range' },
    ];
  }
  createLineColumns(amountCell: DataGridColumn<CreateHandoffLineDraft>['cellTemplate']): DataGridColumn<CreateHandoffLineDraft>[] {
    return [
    { key: 'productSku', label: this.pihText('invoiceHandoffProductColumn'), value: row => row.productSku, secondaryText: row => `${row.productName} · ${row.unitOfMeasureCode} · GR ${row.goodsReceiptId.substring(0, 8)}`, filter: 'text' },
    { key: 'receivedDate', label: this.pihText('receiptDate'), value: row => row.receivedDate, display: row => this.formatDate(row.receivedDate), filter: 'date-range' },
    { key: 'acceptedQuantity', label: this.pihText('acceptedQty'), value: row => row.acceptedQuantity, display: row => this.formatQuantity(row.acceptedQuantity), filter: 'number-range', align: 'end' },
    { key: 'alreadyHandedOffQuantity', label: this.pihText('alreadyHandedOff'), value: row => row.alreadyHandedOffQuantity, display: row => this.formatQuantity(row.alreadyHandedOffQuantity), filter: 'number-range', align: 'end' },
    { key: 'remainingHandoffQuantity', label: this.pihText('remainingHandoffQty'), value: row => row.remainingHandoffQuantity, display: row => this.formatQuantity(row.remainingHandoffQuantity), filter: 'number-range', align: 'end' },
    { key: 'unitPrice', label: this.pihText('unitPrice'), value: row => row.unitPrice, display: row => this.formatMoney(row.unitPrice, this.selectedCurrencyCode()), cellTemplate: amountCell, filter: 'number-range', align: 'end' },
    { key: 'taxRatePercentage', label: this.pihText('taxRate'), value: row => row.taxRatePercentage, display: row => row.taxRatePercentage !== null ? `${row.taxRatePercentage}%` : '—', filter: 'number-range', align: 'end' },
    { key: 'handoffQuantity', label: this.pihText('handoffQty'), value: row => row.handoffQuantity, display: row => this.formatQuantity(row.handoffQuantity), filter: 'number-range', align: 'end' },
    { key: 'lineTotal', label: this.pihText('lineTotal'), value: row => this.calculateLineTotal(row), display: row => this.formatMoney(this.calculateLineTotal(row), this.selectedCurrencyCode()), cellTemplate: amountCell, filter: 'number-range', align: 'end' },
    ];
  }
  get sourceColumns(): DataGridColumn<PurchaseInvoiceHandoffSourceResponse>[] {
    return [
    { key: 'goodsReceiptId', label: this.pihText('goodsReceiptId'), value: row => row.goodsReceiptId, filter: 'text' },
    { key: 'goodsReceiptLineId', label: this.pihText('goodsReceiptLineId'), value: row => row.goodsReceiptLineId, filter: 'text' },
    { key: 'purchaseOrderLineId', label: this.pihText('purchaseOrderLineId'), value: row => row.purchaseOrderLineId, filter: 'text' },
    { key: 'quantity', label: this.pihText('handedOffQuantity'), value: row => row.quantity, display: row => this.formatQuantity(row.quantity), filter: 'number-range', align: 'end' },
    ];
  }

  detailLineColumns(currencyCode: string, amountCell: DataGridColumn<PurchaseInvoiceHandoffLineResponse>['cellTemplate']): DataGridColumn<PurchaseInvoiceHandoffLineResponse>[] {
    return [
      { key: 'productSku', label: this.pihText('invoiceHandoffProductColumn'), value: row => row.productSku, secondaryText: row => `${row.productName} · ${row.unitOfMeasureCode}`, filter: 'text' },
      { key: 'handoffQuantity', label: this.pihText('handoffQty'), value: row => row.handoffQuantity, display: row => this.formatQuantity(row.handoffQuantity), filter: 'number-range', align: 'end' },
      { key: 'unitPrice', label: this.pihText('unitPrice'), value: row => row.unitPrice, display: row => this.formatMoney(row.unitPrice, currencyCode), cellTemplate: amountCell, filter: 'number-range', align: 'end' },
      { key: 'taxRatePercentage', label: this.pihText('taxRate'), value: row => row.taxRatePercentage, display: row => row.taxRatePercentage !== null ? `${row.taxRatePercentage}%` : '—', filter: 'number-range', align: 'end' },
      { key: 'taxAmount', label: this.pihText('taxAmount'), value: row => row.taxAmount, display: row => row.taxAmount !== null ? this.formatMoney(row.taxAmount, currencyCode) : '—', cellTemplate: amountCell, filter: 'number-range', align: 'end' },
      { key: 'lineAmount', label: this.pihText('lineTotal'), value: row => row.lineAmount, display: row => this.formatMoney(row.lineAmount, currencyCode), cellTemplate: amountCell, filter: 'number-range', align: 'end' },
    ];
  }

  gridScopeLabel(): string {
    return this.language.language() === 'ar' ? 'تطبق تصفية الشبكة وترتيبها على الإحالات المحملة فقط' : 'Grid filters and sorting apply to loaded handoffs only';
  }

  readonly selectedCurrencyCode = computed(() => {
    const source = this.eligibleSources().find(s => s.purchaseOrderId === this.selectedSourceId());
    return source?.currencyCode ?? 'USD';
  });

  readonly computedSubtotal = computed(() => {
    return this.createLines.reduce((sum, l) => sum + (l.handoffQuantity * l.unitPrice), 0);
  });

  readonly computedTaxTotal = computed(() => {
    return this.createLines.reduce((sum, l) => {
      const subtotal = l.handoffQuantity * l.unitPrice;
      const tax = l.taxRatePercentage !== null ? Math.round(subtotal * l.taxRatePercentage) / 100 : 0;
      return sum + tax;
    }, 0);
  });

  readonly computedGrandTotal = computed(() => {
    return this.computedSubtotal() + this.computedTaxTotal();
  });

  private readonly copy: Record<'en' | 'ar', Record<string, string>> = {
    en: {
      invoiceHandoffKicker: 'Procurement / Invoice Verification',
      invoiceHandoffs: 'Purchase Invoice Handoffs',
      invoiceHandoff: 'Purchase Invoice Handoff',
      invoiceHandoffsLead: 'Record supplier invoice handoff against verified Goods Receipts with exact pro-rata tax recalculation for Finance AP posting.',
      newInvoiceHandoff: 'New Invoice Handoff',
      invoiceHandoffBoundary: 'FIN-OD-01 Boundary: This module creates operational invoice handoffs referencing Goods Receipts. Finance owns journal posting, AP subledger, period validation, and payment settlement.',
      loadingInvoiceHandoffs: 'Loading Invoice Handoffs…',
      invoiceHandoffListLoadFailed: 'The Invoice Handoff list could not be loaded safely.',
      invoiceHandoffSearch: 'Search supplier, invoice ref, or ID',
      invoiceHandoffStatusFilter: 'Status filter',
      invoiceHandoffAllStatuses: 'All statuses',
      invoiceHandoffFilterNote: 'Results are Tenant- and server-authorized Company/Branch scoped.',
      noInvoiceHandoffs: 'No Invoice Handoffs yet',
      noInvoiceHandoffsLead: 'Create the first invoice handoff against an eligible accepted Goods Receipt.',
      invoiceHandoffRefColumn: 'Supplier Invoice Ref',
      invoiceHandoffSupplierColumn: 'Supplier',
      invoiceHandoffStatusColumn: 'Status',
      invoiceHandoffDateColumn: 'Invoice Date',
      invoiceHandoffCurrencyColumn: 'Currency',
      invoiceHandoffQtyColumn: 'Handoff Qty',
      invoiceHandoffAmountColumn: 'Total Amount',
      invoiceHandoffUpdatedColumn: 'Updated',
      invoiceHandoffLines: 'lines',
      createInvoiceHandoff: 'Create Purchase Invoice Handoff',
      invoiceHandoffCreateLead: 'Select an eligible Purchase Order with recorded Goods Receipts to hand off quantities against the supplier invoice.',
      backToInvoiceHandoffs: 'Back to Invoice Handoffs',
      invoiceHandoffFinanceRule: 'Handoff quantities cannot exceed remaining un-invoiced accepted quantities from recorded Goods Receipts.',
      loadingInvoiceHandoffSources: 'Loading eligible receipt sources…',
      invoiceHandoffSourceLoadFailed: 'Eligible receipt sources could not be loaded safely.',
      invoiceHandoffSourceField: 'Purchase Order & Receipt Sources',
      selectInvoiceHandoffSource: 'Select an eligible Purchase Order',
      supplierInvoiceReference: 'Supplier Invoice Reference',
      supplierInvoiceRefPlaceholder: 'e.g. INV-2026-0889',
      supplierInvoiceDate: 'Supplier Invoice Date',
      invoiceHandoffNotes: 'Handoff Notes (optional)',
      invoiceHandoffNotesPlaceholder: 'Optional receiving reconciliation notes',
      invoiceHandoffLineEntryTitle: 'Goods Receipt Line Handoff',
      invoiceHandoffLineEntryLead: 'Enter quantities to hand off against each received line. Tax is automatically recalculated pro-rata against the handoff subtotal.',
      invoiceHandoffProductColumn: 'Product',
      receiptDate: 'Receipt Date',
      acceptedQty: 'Accepted',
      alreadyHandedOff: 'Already Invoiced',
      remainingHandoffQty: 'Remaining Receivable',
      unitPrice: 'Unit Price',
      taxRate: 'Tax %',
      handoffQty: 'Handoff Qty',
      lineTotal: 'Line Total',
      subtotal: 'Subtotal',
      taxTotal: 'Tax Total',
      grandTotal: 'Invoice Grand Total',
      invoiceHandoffSelectSourceLead: 'Select an eligible Purchase Order above to view accepted Goods Receipt lines.',
      saving: 'Recording…',
      recordInvoiceHandoff: 'Record Invoice Handoff',
      handoffDetails: 'Handoff Details',
      invoiceHandoffSummaryTitle: 'Invoice Handoff Summary',
      invoiceHandoffPurchaseOrder: 'Purchase Order ID',
      invoiceHandoffCreatedAt: 'Recorded At',
      invoiceHandoffVersion: 'Version',
      invoiceHandoffLinesTitle: 'Invoiced Line Items',
      taxAmount: 'Tax Amount',
      sourceReceipts: 'Receipt Sources',
      sourceReceiptLineageTitle: 'Source Goods Receipt Lineage',
      goodsReceiptId: 'Goods Receipt ID',
      goodsReceiptLineId: 'Receipt Line ID',
      purchaseOrderLineId: 'PO Line ID',
      handedOffQuantity: 'Handed-Off Qty',
      lifecycleHistory: 'Lifecycle History',
      auditEvidence: 'Audit Evidence',
      noHistory: 'No lifecycle history recorded.',
      noAudit: 'No audit evidence recorded.',
      notAvailable: '—',
      cancelInvoiceHandoff: 'Cancel Invoice Handoff',
      cancelInvoiceHandoffTitle: 'Cancel Invoice Handoff?',
      cancelInvoiceHandoffLead: 'Cancelling releases the handed-off quantity back to eligible Goods Receipt balance. Source Goods Receipts remain unaffected.',
      cancellationReason: 'Cancellation Reason',
      cancellationReasonHint: 'Explain why this invoice handoff is being cancelled',
      confirmCancel: 'Confirm Cancellation',
      invoiceHandoffActions: 'Invoice Handoff Actions',
      invoiceHandoffSections: 'Invoice Handoff Sections',
      invoiceHandoffCancelledNotice: 'This Purchase Invoice Handoff has been cancelled.',
      noCancellationReason: 'No reason provided',
      missingInvoiceRefError: 'Please provide the supplier invoice reference number.',
      noPositiveHandoffError: 'At least one line must have a handoff quantity greater than zero.',
      overHandoffError: 'Handoff quantity cannot exceed remaining receivable quantity.',
      statusRecorded: 'Recorded',
      statusCancelled: 'Cancelled',
      tabSummary: 'Summary',
      tabLines: 'Lines',
      tabSources: 'Sources',
      tabHistory: 'History',
      tabAudit: 'Audit',
    },
    ar: {
      invoiceHandoffKicker: 'المشتريات / مطابقة الفواتير',
      invoiceHandoffs: 'تسليم فواتير المشتريات',
      invoiceHandoff: 'تسليم فاتورة مشتريات',
      invoiceHandoffsLead: 'تسجيل تسليم فاتورة المورد مقابل سندات الاستلام الفعلية مع إعادة حساب الضريبة تناسبياً للمالية.',
      newInvoiceHandoff: 'تسليم فاتورة جديد',
      invoiceHandoffBoundary: 'ميثاق FIN-OD-01: ينشئ هذا الموديل مستندات تسليم الفواتير للمالية. تتولى المالية قيود اليومية وحسابات الموردين والتسوية.',
      loadingInvoiceHandoffs: 'جارٍ تحميل فواتير التسليم…',
      invoiceHandoffListLoadFailed: 'تعذّر تحميل قائمة فواتير التسليم بأمان.',
      invoiceHandoffSearch: 'البحث بالمورد أو رقم الفاتورة أو المعرف',
      invoiceHandoffStatusFilter: 'مرشح الحالة',
      invoiceHandoffAllStatuses: 'كل الحالات',
      invoiceHandoffFilterNote: 'النتائج مقيدة بسياق العميل والشركة/الفرع المصرح بهما.',
      noInvoiceHandoffs: 'لا توجد فواتير تسليم بعد',
      noInvoiceHandoffsLead: 'أنشئ أول تسليم فاتورة مقابل سند استلام مقبول.',
      invoiceHandoffRefColumn: 'رقم فاتورة المورد',
      invoiceHandoffSupplierColumn: 'المورد',
      invoiceHandoffStatusColumn: 'الحالة',
      invoiceHandoffDateColumn: 'تاريخ الفاتورة',
      invoiceHandoffCurrencyColumn: 'العملة',
      invoiceHandoffQtyColumn: 'الكمية المسلمة',
      invoiceHandoffAmountColumn: 'إجمالي المبلغ',
      invoiceHandoffUpdatedColumn: 'آخر تحديث',
      invoiceHandoffLines: 'بنود',
      createInvoiceHandoff: 'تسجيل تسليم فاتورة مشتريات',
      invoiceHandoffCreateLead: 'اختر أمر شراء يحتوي على سندات استلام مقبولة لتسليم الفاتورة للمالية.',
      backToInvoiceHandoffs: 'العودة إلى فواتير التسليم',
      invoiceHandoffFinanceRule: 'لا يمكن أن تتجاوز الكميات المسلمة الرصيد المتبقي غير المفوتر من سندات الاستلام.',
      loadingInvoiceHandoffSources: 'جارٍ تحميل سندات الاستلام المؤهلة…',
      invoiceHandoffSourceLoadFailed: 'تعذّر تحميل مصادر الاستلام المؤهلة.',
      invoiceHandoffSourceField: 'أمر الشراء ومصادر الاستلام',
      selectInvoiceHandoffSource: 'اختر أمر شراء مؤهلاً',
      supplierInvoiceReference: 'رقم فاتورة المورد',
      supplierInvoiceRefPlaceholder: 'مثال: INV-2026-0889',
      supplierInvoiceDate: 'تاريخ فاتورة المورد',
      invoiceHandoffNotes: 'ملاحظات التسليم (اختياري)',
      invoiceHandoffNotesPlaceholder: 'أي ملاحظات تسوية فواتير',
      invoiceHandoffLineEntryTitle: 'تسليم بنود سندات الاستلام',
      invoiceHandoffLineEntryLead: 'أدخل الكميات المراد تسليمها لكل بند مستلم. يتم احتساب الضريبة تناسبياً تلقائياً.',
      invoiceHandoffProductColumn: 'المنتج',
      receiptDate: 'تاريخ الاستلام',
      acceptedQty: 'المقبول',
      alreadyHandedOff: 'المفوتر سابقاً',
      remainingHandoffQty: 'المتبقي للفوترة',
      unitPrice: 'سعر الوحدة',
      taxRate: 'نسبة الضريبة ٪',
      handoffQty: 'الكمية المسلمة',
      lineTotal: 'إجمالي البند',
      subtotal: 'المجموع قبل الضريبة',
      taxTotal: 'إجمالي الضريبة',
      grandTotal: 'إجمالي الفاتورة النهائي',
      invoiceHandoffSelectSourceLead: 'اختر أمر شراء أعلاه لعرض البنود المقبولة من سندات الاستلام.',
      saving: 'جارٍ التسجيل…',
      recordInvoiceHandoff: 'حفظ تسليم الفاتورة',
      handoffDetails: 'تفاصيل التسليم',
      invoiceHandoffSummaryTitle: 'بيانات تسليم الفاتورة',
      invoiceHandoffPurchaseOrder: 'معرف أمر الشراء',
      invoiceHandoffCreatedAt: 'تاريخ التسجيل',
      invoiceHandoffVersion: 'النسخة',
      invoiceHandoffLinesTitle: 'بنود الفاتورة المسلمة',
      taxAmount: 'مبلغ الضريبة',
      sourceReceipts: 'سندات الاستلام المصدر',
      sourceReceiptLineageTitle: 'تتبع سندات الاستلام الأصلية',
      goodsReceiptId: 'معرف سند الاستلام',
      goodsReceiptLineId: 'معرف بند الاستلام',
      purchaseOrderLineId: 'معرف بند أمر الشراء',
      handedOffQuantity: 'الكمية المسلمة',
      lifecycleHistory: 'سجل دورة الحياة',
      auditEvidence: 'دليل التدقيق',
      noHistory: 'لا يوجد سجل دورة حياة.',
      noAudit: 'لا توجد أدلة تدقيق.',
      notAvailable: '—',
      cancelInvoiceHandoff: 'إلغاء تسليم الفاتورة',
      cancelInvoiceHandoffTitle: 'إلغاء تسليم الفاتورة؟',
      cancelInvoiceHandoffLead: 'يؤدي الإلغاء إلى إعادة الكميات المسلمة لرصيد سند الاستلام دون المساس بسند الاستلام نفسه.',
      cancellationReason: 'سبب الإلغاء',
      cancellationReasonHint: 'وضح سبب إلغاء تسليم هذه الفاتورة',
      confirmCancel: 'تأكيد الإلغاء',
      invoiceHandoffActions: 'إجراءات تسليم الفاتورة',
      invoiceHandoffSections: 'أقسام تسليم الفاتورة',
      invoiceHandoffCancelledNotice: 'تم إلغاء تسليم الفاتورة هذا.',
      noCancellationReason: 'لم يُقدَّم سبب',
      missingInvoiceRefError: 'يرجى إدخال رقم مرجع فاتورة المورد.',
      noPositiveHandoffError: 'يجب إدخال كمية أكبر من الصفر لبند واحد على الأقل.',
      overHandoffError: 'لا يمكن أن تتجاوز الكمية المسلمة الرصيد المتبقي للفوترة.',
      statusRecorded: 'مسجل',
      statusCancelled: 'ملغي',
      tabSummary: 'الملخص',
      tabLines: 'البنود',
      tabSources: 'المصادر',
      tabHistory: 'السجل',
      tabAudit: 'التدقيق',
    },
  };

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = params.get('id');
      const url = this.router.url;
      if (url.endsWith('/new')) {
        this.mode.set('create');
        this.loadCreatePrerequisites();
      } else if (id) {
        this.mode.set('detail');
        this.loadDetail(id);
      } else {
        this.mode.set('list');
        this.loadList();
      }
    });
  }

  pihText(key: string): string {
    const lang = this.language.language() as 'en' | 'ar';
    return this.copy[lang]?.[key] ?? this.copy.en[key] ?? key;
  }

  statusLabel(status: PurchaseInvoiceHandoffStatus): string {
    return status === 'Recorded' ? this.pihText('statusRecorded') : status === 'Cancelled' ? this.pihText('statusCancelled') : status;
  }

  statusClass(status: PurchaseInvoiceHandoffStatus): string {
    return status === 'Recorded' ? 'status-badge--recorded' : status === 'Cancelled' ? 'status-badge--cancelled' : '';
  }

  tabLabel(tab: DetailTab): string {
    switch (tab) {
      case 'summary': return this.pihText('tabSummary');
      case 'lines': return this.pihText('tabLines');
      case 'sources': return this.pihText('tabSources');
      case 'history': return this.pihText('tabHistory');
      case 'audit': return this.pihText('tabAudit');
    }
  }

  tabId(tab: DetailTab): string { return `pih-tab-${tab}`; }
  setTab(tab: DetailTab): void { this.activeTab.set(tab); }
  selectTab(tab: string): void { this.setTab(tab as DetailTab); }
  setStatusFilter(value: string): void { this.statusFilter.set(value); }

  async loadList(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const records = await firstValueFrom(this.service.list());
      this.records.set(records);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async loadCreatePrerequisites(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.validationError.set(null);
    try {
      const sources = await firstValueFrom(this.service.eligibleSources());
      this.eligibleSources.set(sources);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.loading.set(false);
    }
  }

  onSelectSource(sourceId: string): void {
    this.selectedSourceId.set(sourceId);
    this.validationError.set(null);
    const source = this.eligibleSources().find(s => s.purchaseOrderId === sourceId);
    if (!source) {
      this.createLines = [];
      return;
    }

    this.createLines = source.lines.map(line => ({
      goodsReceiptId: line.goodsReceiptId,
      goodsReceiptLineId: line.goodsReceiptLineId,
      purchaseOrderLineId: line.purchaseOrderLineId,
      productSku: line.productSku,
      productName: line.productName,
      unitOfMeasureCode: line.unitOfMeasureCode,
      receivedDate: line.receivedDate,
      acceptedQuantity: line.acceptedQuantity,
      alreadyHandedOffQuantity: line.alreadyHandedOffQuantity,
      remainingHandoffQuantity: line.remainingHandoffQuantity,
      unitPrice: line.unitPrice,
      taxRatePercentage: line.taxRatePercentage,
      handoffQuantity: line.remainingHandoffQuantity,
    }));
  }

  calculateLineTotal(line: CreateHandoffLineDraft): number {
    const subtotal = (line.handoffQuantity || 0) * line.unitPrice;
    const tax = line.taxRatePercentage !== null ? Math.round(subtotal * line.taxRatePercentage) / 100 : 0;
    return subtotal + tax;
  }

  onLineQuantityChange(): void {
    this.validateCreateForm();
  }

  validateCreateForm(): boolean {
    if (!this.createInvoiceRef.trim()) {
      this.validationError.set(this.pihText('missingInvoiceRefError'));
      return false;
    }

    const totalHandoff = this.createLines.reduce((sum, l) => sum + (l.handoffQuantity || 0), 0);
    if (totalHandoff <= 0) {
      this.validationError.set(this.pihText('noPositiveHandoffError'));
      return false;
    }

    for (const line of this.createLines) {
      if (line.handoffQuantity > line.remainingHandoffQuantity) {
        this.validationError.set(this.pihText('overHandoffError'));
        return false;
      }
    }

    this.validationError.set(null);
    return true;
  }

  canSubmitCreate(): boolean {
    return !!this.selectedSourceId() && !!this.createInvoiceRef.trim() && this.createLines.length > 0 && !this.validationError();
  }

  async createHandoff(): Promise<void> {
    if (!this.validateCreateForm()) return;
    this.saving.set(true);
    this.error.set(null);

    const payload: PurchaseInvoiceHandoffCreateRequest = {
      purchaseOrderId: this.selectedSourceId(),
      supplierInvoiceReference: this.createInvoiceRef.trim(),
      supplierInvoiceDate: this.createInvoiceDate,
      notes: this.createNotes.trim() || null,
      sources: this.createLines
        .filter(l => l.handoffQuantity > 0)
        .map(l => ({
          goodsReceiptId: l.goodsReceiptId,
          goodsReceiptLineId: l.goodsReceiptLineId,
          quantity: l.handoffQuantity,
        })),
    };

    try {
      const response = await this.service.create(payload);
      void this.router.navigate(['/app/procurement/invoice-handoffs', response.id]);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.saving.set(false);
    }
  }

  async loadDetail(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [handoff, history, audit] = await Promise.all([
        firstValueFrom(this.service.get(id)),
        firstValueFrom(this.service.history(id)),
        firstValueFrom(this.service.audit(id)),
      ]);
      this.handoff.set(handoff);
      this.history.set(history);
      this.audit.set(audit);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.loading.set(false);
    }
  }

  openCancelDialog(): void {
    this.cancelReason = '';
    this.showCancelDialog.set(true);
  }

  closeCancelDialog(): void {
    this.showCancelDialog.set(false);
  }

  async confirmCancel(): Promise<void> {
    const current = this.handoff();
    if (!current) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.service.cancel(current.id, current.version, this.cancelReason);
      this.handoff.set(updated);
      this.closeCancelDialog();
      await this.loadDetail(current.id);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.saving.set(false);
    }
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    const parsed = new Date(value);
    return isNaN(parsed.getTime()) ? value : parsed.toLocaleDateString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  }

  formatDateTime(value?: string | null): string {
    if (!value) return '—';
    const parsed = new Date(value);
    return isNaN(parsed.getTime()) ? value : parsed.toLocaleString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  formatQuantity(value?: number | null): string {
    if (value === null || value === undefined) return '0';
    return Number(value).toLocaleString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { maximumFractionDigits: 4 });
  }

  formatMoney(amount: number, currencyCode: string): string {
    try {
      return new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', {
        style: 'currency',
        currency: currencyCode,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(amount);
    } catch {
      return `${amount.toFixed(2)} ${currencyCode}`;
    }
  }

  errorText(error: SafeUiError): string {
    return error.status === 409 || error.code === 'concurrency_conflict'
      ? this.language.text('prConcurrencyConflictError')
      : error.status === 403 || error.code === 'access_denied' || error.code === 'permission_denied'
        ? this.language.text('accessDenied')
        : this.language.text('requestError');
  }
}
