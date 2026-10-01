import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService } from '../../core/i18n/language.service';
import { InventoryService } from '../inventory/inventory.service';
import {
  GoodsReceiptAuditResponse,
  GoodsReceiptCreateRequest,
  GoodsReceiptEligibleSourceResponse,
  GoodsReceiptHistoryResponse,
  GoodsReceiptListItemResponse,
  GoodsReceiptLineResponse,
  GoodsReceiptResponse,
  GoodsReceiptStatus,
  GoodsReceiptWarehouseOptionResponse,
} from './goods-receipt.model';
import { GoodsReceiptService } from './goods-receipt.service';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { TabsComponent } from '../../shared/ui/tabs.component';
import type { AppTab } from '../../shared/ui/tabs.component';

type WorkspaceMode = 'list' | 'create' | 'detail';
type DetailTab = 'summary' | 'lines' | 'history' | 'audit';

interface CreateReceiptLineDraft {
  purchaseOrderLineId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  confirmedQuantity: number;
  alreadyReceivedQuantity: number;
  remainingReceivableQuantity: number;
  unitPrice: number;
  receivedQuantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  damagedQuantity: number | null;
  damageNotes: string;
  notes: string;
}

@Component({
  selector: 'app-goods-receipt-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink, DataGridComponent, PageHeaderComponent, TabsComponent],
  template: `
    @if (mode() === 'list') {
      <section class="ui-page goods-receipt-page" data-testid="goods-receipt-list">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ grText('goodsReceiptKicker') }}</p>
            <h1>{{ grText('goodsReceipts') }}</h1>
            <p>{{ grText('goodsReceiptsLead') }}</p>
          </div>
          <div page-header-actions><a class="button button--primary" routerLink="/app/procurement/goods-receipts/new" data-testid="new-goods-receipt">＋ {{ grText('newGoodsReceipt') }}</a></div>
        </app-page-header>
        <div class="boundary-note" role="note"><span aria-hidden="true">◇</span><span>{{ grText('goodsReceiptBoundary') }}</span></div>

        @if (loading()) {
          <section class="ui-surface state-card" aria-live="polite"><span class="spinner" aria-hidden="true"></span><h2>{{ grText('loadingGoodsReceipts') }}</h2></section>
        } @else if (error(); as currentError) {
          <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ grText('goodsReceiptListLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadList()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface ledger-panel">
            <div class="filter-toolbar">
              <label class="filter-search"><span aria-hidden="true">⌕</span><input type="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="grText('goodsReceiptSearch')" /><span class="sr-only">{{ grText('goodsReceiptSearch') }}</span></label>
              <label class="filter-field"><span>{{ grText('goodsReceiptStatusFilter') }}</span><select [value]="statusFilter()" (change)="setStatusFilter($any($event.target).value)"><option value="">{{ grText('goodsReceiptAllStatuses') }}</option>@for (status of statuses; track status) {<option [value]="status">{{ statusLabel(status) }}</option>}</select></label>
              <p class="filter-note">{{ grText('goodsReceiptFilterNote') }}</p>
            </div>
            @if (filteredRecords().length === 0) {
              <div class="empty-ledger"><span aria-hidden="true">◌</span><h2>{{ grText('noGoodsReceipts') }}</h2><p>{{ grText('noGoodsReceiptsLead') }}</p></div>
            } @else {
              <app-data-grid [rows]="filteredRecords()" [columns]="receiptColumns" [language]="language.language()" [clientPaging]="true" [showPager]="true" [scopeLabel]="gridScopeLabel()" [caption]="grText('goodsReceipts')" [countLabel]="grText('goodsReceipts')" [filterInputLabel]="grText('goodsReceiptSearch')" [previousPageLabel]="language.text('previous')" [nextPageLabel]="language.text('next')" />
            }
          </section>
        }
      </section>
    }

    @if (mode() === 'create') {
      <section class="ui-page goods-receipt-page" data-testid="goods-receipt-create">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ grText('goodsReceiptKicker') }}</p>
            <h1>{{ grText('createGoodsReceipt') }}</h1>
            <p>{{ grText('goodsReceiptCreateLead') }}</p>
          </div>
          <div page-header-actions><a class="button button--secondary" routerLink="/app/procurement/goods-receipts">{{ grText('backToGoodsReceipts') }}</a></div>
        </app-page-header>
        <div class="boundary-note" role="note"><span aria-hidden="true">◇</span><span>{{ grText('goodsReceiptSourceRule') }}</span></div>

        @if (loading()) {
          <section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ grText('loadingGoodsReceiptSources') }}</h2></section>
        } @else if (error(); as currentError) {
          <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ grText('goodsReceiptSourceLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadCreatePrerequisites()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface form-card">
            <div class="create-meta-grid">
              <label class="field">
                <span class="field__label">{{ grText('goodsReceiptSourceField') }} *</span>
                <select [ngModel]="selectedSourceId()" (ngModelChange)="onSelectSource($event)" data-testid="goods-receipt-source">
                  <option value="">{{ grText('selectGoodsReceiptSource') }}</option>
                  @for (source of eligibleSources(); track source.purchaseOrderId) {
                    <option [value]="source.purchaseOrderId">{{ source.supplierName }} ({{ source.supplierCode }}) · PO {{ source.purchaseOrderId.substring(0, 8) }} · {{ source.currencyCode }}</option>
                  }
                </select>
              </label>

              <label class="field">
                <span class="field__label">{{ grText('goodsReceiptWarehouse') }} *</span>
                <select [(ngModel)]="createWarehouseId" data-testid="goods-receipt-warehouse">
                  <option value="">{{ grText('selectWarehouse') }}</option>
                  @for (wh of warehouses(); track wh.warehouseId) {
                    <option [value]="wh.warehouseId">{{ wh.code }} · {{ wh.name }}</option>
                  }
                </select>
              </label>

              <label class="field">
                <span class="field__label">{{ grText('goodsReceiptDate') }} *</span>
                <input type="date" [(ngModel)]="createReceivedDate" data-testid="goods-receipt-received-date" />
              </label>

              <label class="field">
                <span class="field__label">{{ grText('goodsReceiptReferenceNote') }}</span>
                <input type="text" maxlength="256" [(ngModel)]="createReferenceNote" [placeholder]="grText('goodsReceiptReferenceNotePlaceholder')" data-testid="goods-receipt-reference-note" />
              </label>
            </div>

            <label class="field">
              <span class="field__label">{{ grText('goodsReceiptNotes') }}</span>
              <textarea [(ngModel)]="createNotes" rows="2" [placeholder]="grText('goodsReceiptNotesPlaceholder')"></textarea>
            </label>

            @if (createLines.length > 0) {
              <section class="source-lines-section">
                <p class="section-kicker">{{ grText('goodsReceiptLines') }}</p>
                <h2>{{ grText('goodsReceiptLineEntryTitle') }}</h2>
                <p class="detail-copy">{{ grText('goodsReceiptLineEntryLead') }}</p>

                <app-data-grid [rows]="createLines" [columns]="createLineColumns" [rowActionsTemplate]="createLineActions" rowKey="purchaseOrderLineId" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="grText('goodsReceiptLineEntryTitle')" [countLabel]="grText('goodsReceiptLines')" [rowActionsLabel]="grText('goodsReceiptLinesTitle')">
                  <ng-template #createLineActions let-line>
                    <div class="grid-line-editors">
                      <label><span>{{ grText('goodsReceiptReceivedQty') }} *</span><input class="table-input numeric" type="number" min="0" step="0.000001" [(ngModel)]="line.receivedQuantity" (ngModelChange)="onLineReceivedChange(line)" [ngModelOptions]="{standalone: true}" [attr.aria-label]="grText('goodsReceiptReceivedQty')" /></label>
                      <label><span>{{ grText('goodsReceiptAcceptedQty') }} *</span><input class="table-input numeric" type="number" min="0" [max]="line.receivedQuantity" step="0.000001" [(ngModel)]="line.acceptedQuantity" (ngModelChange)="onLineAcceptedChange(line)" [ngModelOptions]="{standalone: true}" [attr.aria-label]="grText('goodsReceiptAcceptedQty')" /></label>
                      <label><span>{{ grText('goodsReceiptRejectedQty') }}</span><input class="table-input numeric" type="number" min="0" [max]="line.receivedQuantity" step="0.000001" [(ngModel)]="line.rejectedQuantity" (ngModelChange)="onLineRejectedChange(line)" [ngModelOptions]="{standalone: true}" [attr.aria-label]="grText('goodsReceiptRejectedQty')" /></label>
                      <label><span>{{ grText('goodsReceiptDamagedQty') }}</span><input class="table-input numeric" type="number" min="0" [max]="line.receivedQuantity" step="0.000001" [(ngModel)]="line.damagedQuantity" [ngModelOptions]="{standalone: true}" [attr.aria-label]="grText('goodsReceiptDamagedQty')" /></label>
                      <label><span>{{ grText('goodsReceiptDamageNotes') }}</span><input class="table-input" type="text" maxlength="2048" [placeholder]="grText('damageNotesPlaceholder')" [(ngModel)]="line.damageNotes" [ngModelOptions]="{standalone: true}" [attr.aria-label]="grText('goodsReceiptDamageNotes')" /></label>
                    </div>
                  </ng-template>
                </app-data-grid>

                @if (validationError()) {
                  <div class="inline-error" role="alert">{{ validationError() }}</div>
                }

                <div class="form-actions">
                  <button class="button button--primary" type="button" [disabled]="saving() || !canSubmitCreate()" (click)="createReceipt()" data-testid="submit-goods-receipt">
                    {{ saving() ? grText('saving') : grText('recordGoodsReceipt') }}
                  </button>
                </div>
              </section>
            } @else {
              <div class="empty-inline">{{ grText('goodsReceiptSelectSourceLead') }}</div>
            }
          </section>
        }
      </section>
    }

    @if (mode() === 'detail' && receipt(); as currentReceipt) {
      <section class="ui-page goods-receipt-page" data-testid="goods-receipt-detail">
        <app-page-header>
          <div page-header-copy>
            <p class="eyebrow">{{ grText('goodsReceiptKicker') }}</p>
            <h1>{{ currentReceipt.referenceNote || grText('goodsReceipt') + ' ' + currentReceipt.id.substring(0, 8) }}</h1>
            <p>{{ currentReceipt.supplierName }} ({{ currentReceipt.supplierCode }}) · {{ formatDate(currentReceipt.receivedDate) }}</p>
          </div>
          <div page-header-actions>
            <span class="status-badge" [class]="statusClass(currentReceipt.status)"><span aria-hidden="true"></span>{{ statusLabel(currentReceipt.status) }}</span>
            <a class="button button--secondary" routerLink="/app/procurement/goods-receipts">{{ grText('backToGoodsReceipts') }}</a>
            @if (currentReceipt.canCancel) { <button class="button button--danger" type="button" (click)="openCancelDialog()" data-testid="cancel-goods-receipt">{{ grText('cancelGoodsReceipt') }}</button> }
          </div>
        </app-page-header>

        @if (currentReceipt.status === 'Cancelled') {
          <section class="boundary-note terminal-recovery-note" role="note">
            <strong>{{ grText('goodsReceiptCancelledNotice') }}</strong>
            <span>{{ currentReceipt.cancellationReason || grText('noCancellationReason') }} ({{ formatDateTime(currentReceipt.cancelledAt ?? '') }})</span>
          </section>
        }

        @if (error(); as currentError) {
          <div class="inline-error" role="alert">{{ errorText(currentError) }}</div>
        }

        <app-tabs [tabs]="detailTabItems()" [selected]="activeTab()" [ariaLabel]="grText('goodsReceiptSections')" (selectedChange)="selectTab($event)" />

        @if (activeTab() === 'summary') {
          <section class="detail-layout" role="tabpanel" [attr.aria-labelledby]="tabId('summary')">
            <section class="ui-surface detail-card">
              <p class="section-kicker">{{ grText('receiptDetails') }}</p>
              <h2>{{ grText('goodsReceiptSummaryTitle') }}</h2>
              <dl class="fact-grid">
                <div><dt>{{ grText('goodsReceiptSupplierColumn') }}</dt><dd>{{ currentReceipt.supplierName }} · {{ currentReceipt.supplierCode }}</dd></div>
                <div><dt>{{ grText('goodsReceiptPurchaseOrder') }}</dt><dd><code>{{ currentReceipt.purchaseOrderId }}</code></dd></div>
                <div><dt>{{ grText('goodsReceiptWarehouse') }}</dt><dd><code>{{ currentReceipt.warehouseId }}</code></dd></div>
                <div><dt>{{ grText('goodsReceiptDate') }}</dt><dd>{{ formatDate(currentReceipt.receivedDate) }}</dd></div>
                <div><dt>{{ grText('goodsReceiptReferenceNote') }}</dt><dd>{{ currentReceipt.referenceNote || grText('notAvailable') }}</dd></div>
                <div><dt>{{ grText('goodsReceiptCreatedAt') }}</dt><dd>{{ formatDateTime(currentReceipt.createdAt) }}</dd></div>
                <div><dt>{{ grText('goodsReceiptVersion') }}</dt><dd><code>{{ currentReceipt.version }}</code></dd></div>
              </dl>
              @if (currentReceipt.notes) {
                <p class="detail-copy">{{ currentReceipt.notes }}</p>
              }
            </section>
          </section>
        } @else if (activeTab() === 'lines') {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('lines')">
            <p class="section-kicker">{{ grText('goodsReceiptLines') }}</p>
            <h2>{{ grText('goodsReceiptLinesTitle') }}</h2>
            <app-data-grid [rows]="currentReceipt.lines" [columns]="detailLineColumns" [rowActionsTemplate]="detailLineActions" [language]="language.language()" [clientPaging]="true" [showPager]="true" [caption]="grText('goodsReceiptLinesTitle')" [countLabel]="grText('goodsReceiptLines')" [rowActionsLabel]="grText('inventoryMovement')">
              <ng-template #detailLineActions let-line>
                @if (currentReceipt.status === 'Recorded' && line.acceptedQuantity > 0) {
                  <button class="button button--secondary" type="button" [disabled]="saving()" (click)="postAcceptedLine(currentReceipt, line)">{{ grText('postToInventory') }}</button>
                } @else { <span class="muted">{{ grText('notAvailable') }}</span> }
              </ng-template>
            </app-data-grid>
          </section>
        } @else if (activeTab() === 'history') {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('history')">
            <p class="section-kicker">{{ grText('lifecycleHistory') }}</p>
            <h2>{{ grText('lifecycleHistory') }}</h2>
            @if (history().length === 0) {
              <div class="empty-inline">{{ grText('noHistory') }}</div>
            } @else {
              <ol class="timeline">
                @for (entry of history(); track entry.evidenceId) {
                  <li>
                    <strong>{{ statusLabel(entry.fromStatus) }} → {{ statusLabel(entry.toStatus) }}</strong>
                    <small>{{ entry.action }} · {{ formatDateTime(entry.occurredAt) }}</small>
                    <p>{{ entry.reason || grText('notAvailable') }}</p>
                  </li>
                }
              </ol>
            }
          </section>
        } @else {
          <section class="ui-surface detail-card" role="tabpanel" [attr.aria-labelledby]="tabId('audit')">
            <p class="section-kicker">{{ grText('auditEvidence') }}</p>
            <h2>{{ grText('auditEvidence') }}</h2>
            @if (audit().length === 0) {
              <div class="empty-inline">{{ grText('noAudit') }}</div>
            } @else {
              <div class="audit-list">
                @for (entry of audit(); track entry.evidenceId) {
                  <article>
                    <strong>{{ entry.operationId }}</strong>
                    <small>{{ entry.decision }} · {{ formatDateTime(entry.occurredAt) }}</small>
                    <p>{{ entry.reason || entry.afterSummary || grText('notAvailable') }}</p>
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
        <section class="action-dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-receipt-title" tabindex="-1" (click)="$event.stopPropagation()">
          <p class="section-kicker">{{ grText('goodsReceiptKicker') }}</p>
          <h2 id="cancel-receipt-title">{{ grText('cancelGoodsReceiptTitle') }}</h2>
          <p>{{ grText('cancelGoodsReceiptLead') }}</p>
          <label class="field">
            <span class="field__label">{{ grText('cancellationReason') }} *</span>
            <textarea [(ngModel)]="cancelReason" rows="3" [placeholder]="grText('cancellationReasonHint')"></textarea>
          </label>
          <div class="dialog-actions">
            <button class="button button--secondary" type="button" (click)="closeCancelDialog()">{{ language.text('cancel') }}</button>
            <button class="button button--danger" type="button" [disabled]="saving() || !cancelReason.trim()" (click)="confirmCancel()">
              {{ saving() ? grText('saving') : grText('confirmCancel') }}
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
    .spinner { width: 2rem; height: 2rem; border: 3px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: gr-spin 1s linear infinite; }
    @keyframes gr-spin { to { transform: rotate(360deg); } }
    .ledger-panel { padding: 0; overflow: hidden; }
    .filter-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: .7rem; padding: .85rem 1rem; border-bottom: 1px solid var(--line); background: color-mix(in srgb, var(--accent-soft) 70%, var(--surface-raised)); }
    .filter-field { display: grid; gap: .25rem; min-width: 12rem; color: var(--ink-muted); font-size: .64rem; font-weight: 900; letter-spacing: .06em; text-transform: uppercase; }
    .filter-field select { text-transform: none; letter-spacing: normal; }
    .filter-note { flex: 1 1 100%; margin: 0; color: var(--ink-muted); font-size: .66rem; }
    .status-badge { display: inline-flex; align-items: center; gap: .35rem; border: 1px solid var(--line); border-radius: 99px; padding: .28rem .5rem; color: var(--ink-muted); background: var(--surface); font-size: .63rem; font-weight: 900; white-space: nowrap; }
    .status-badge > span { width: .4rem; height: .4rem; border-radius: 50%; background: currentColor; }
    .status-badge--recorded { color: var(--success); background: var(--accent-soft); }
    .status-badge--cancelled { color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .numeric { font-variant-numeric: tabular-nums; }
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
    .grid-line-editors { display: grid; grid-template-columns: repeat(5, minmax(8rem, 1fr)); gap: .45rem; min-width: 43rem; }
    .grid-line-editors label { display: grid; gap: .2rem; color: var(--ink-muted); font-size: .62rem; font-weight: 800; }
    .grid-line-editors label span { white-space: nowrap; }
    .grid-line-editors input { min-width: 0; }
    .remaining-highlight { color: var(--accent-strong); font-weight: 800; }
    .accepted-highlight { color: var(--success); font-weight: 800; }
    .rejected-highlight { color: var(--danger); font-weight: 800; }
    .damaged-highlight { color: var(--warning); font-weight: 800; }
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
export class GoodsReceiptWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly service = inject(GoodsReceiptService);
  private readonly inventory = inject(InventoryService, { optional: true });
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly mode = signal<WorkspaceMode>('list');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<SafeUiError | null>(null);
  readonly validationError = signal<string | null>(null);
  readonly records = signal<GoodsReceiptListItemResponse[]>([]);
  readonly eligibleSources = signal<GoodsReceiptEligibleSourceResponse[]>([]);
  readonly warehouses = signal<GoodsReceiptWarehouseOptionResponse[]>([]);
  readonly receipt = signal<GoodsReceiptResponse | null>(null);
  readonly history = signal<GoodsReceiptHistoryResponse[]>([]);
  readonly audit = signal<GoodsReceiptAuditResponse[]>([]);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly selectedSourceId = signal('');
  readonly activeTab = signal<DetailTab>('summary');
  readonly showCancelDialog = signal(false);

  createWarehouseId = '';
  createReceivedDate = new Date().toISOString().slice(0, 10);
  createReferenceNote = '';
  createNotes = '';
  createLines: CreateReceiptLineDraft[] = [];
  cancelReason = '';

  readonly statuses: GoodsReceiptStatus[] = ['Recorded', 'Cancelled'];
  readonly tabs: DetailTab[] = ['summary', 'lines', 'history', 'audit'];
  detailTabItems(): AppTab[] { return this.tabs.map(tab => ({ id: tab, label: this.tabLabel(tab), tabId: this.tabId(tab) })); }

  readonly filteredRecords = computed(() => {
    const query = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    return this.records().filter((record) => {
      const matchStatus = !status || record.status === status;
      const matchQuery = !query || `${record.supplierName} ${record.supplierCode} ${record.referenceNote ?? ''} ${record.id}`.toLowerCase().includes(query);
      return matchStatus && matchQuery;
    });
  });
  get receiptColumns(): DataGridColumn<GoodsReceiptListItemResponse>[] {
    return [
    { key: 'referenceNote', label: this.grText('goodsReceiptReferenceColumn'), value: row => row.referenceNote || row.id.substring(0, 8), link: row => `/app/procurement/goods-receipts/${row.id}`, secondaryText: row => `${row.lineCount} ${this.grText('goodsReceiptLines')}`, filter: 'text' },
    { key: 'supplierName', label: this.grText('goodsReceiptSupplierColumn'), value: row => row.supplierName, secondaryText: row => row.supplierCode, filter: 'text' },
    { key: 'status', label: this.grText('goodsReceiptStatusColumn'), value: row => this.statusLabel(row.status), badge: true, filter: 'select' },
    { key: 'receivedDate', label: this.grText('goodsReceiptDateColumn'), value: row => row.receivedDate, display: row => this.formatDate(row.receivedDate), filter: 'date-range' },
    { key: 'totalAcceptedQuantity', label: this.grText('goodsReceiptAcceptedColumn'), value: row => row.totalAcceptedQuantity, display: row => this.formatQuantity(row.totalAcceptedQuantity), filter: 'number-range', align: 'end' },
    { key: 'totalRejectedQuantity', label: this.grText('goodsReceiptRejectedColumn'), value: row => row.totalRejectedQuantity, display: row => this.formatQuantity(row.totalRejectedQuantity), filter: 'number-range', align: 'end' },
    { key: 'updatedAt', label: this.grText('goodsReceiptUpdatedColumn'), value: row => row.updatedAt, display: row => this.formatDateTime(row.updatedAt), filter: 'date-range' },
    ];
  }
  get createLineColumns(): DataGridColumn<CreateReceiptLineDraft>[] {
    return [
    { key: 'productSku', label: this.grText('goodsReceiptProductColumn'), value: row => row.productSku, secondaryText: row => `${row.productName} · ${row.unitOfMeasureCode}`, filter: 'text' },
    { key: 'confirmedQuantity', label: this.grText('goodsReceiptConfirmedQty'), value: row => row.confirmedQuantity, display: row => this.formatQuantity(row.confirmedQuantity), filter: 'number-range', align: 'end' },
    { key: 'remainingReceivableQuantity', label: this.grText('goodsReceiptRemainingQty'), value: row => row.remainingReceivableQuantity, display: row => this.formatQuantity(row.remainingReceivableQuantity), filter: 'number-range', align: 'end' },
    { key: 'receivedQuantity', label: this.grText('goodsReceiptReceivedQty'), value: row => row.receivedQuantity, filter: 'number-range', align: 'end' },
    { key: 'acceptedQuantity', label: this.grText('goodsReceiptAcceptedQty'), value: row => row.acceptedQuantity, filter: 'number-range', align: 'end' },
    { key: 'rejectedQuantity', label: this.grText('goodsReceiptRejectedQty'), value: row => row.rejectedQuantity, filter: 'number-range', align: 'end' },
    { key: 'damagedQuantity', label: this.grText('goodsReceiptDamagedQty'), value: row => row.damagedQuantity, filter: 'number-range', align: 'end' },
    { key: 'damageNotes', label: this.grText('goodsReceiptDamageNotes'), value: row => row.damageNotes, filter: 'text' },
    ];
  }
  get detailLineColumns(): DataGridColumn<GoodsReceiptLineResponse>[] {
    return [
    { key: 'productSku', label: this.grText('goodsReceiptProductColumn'), value: row => row.productSku, secondaryText: row => `${row.productName} · ${row.unitOfMeasureCode}`, filter: 'text' },
    { key: 'orderedQuantityAtReceipt', label: this.grText('orderedAtReceipt'), value: row => row.orderedQuantityAtReceipt, display: row => this.formatQuantity(row.orderedQuantityAtReceipt), filter: 'number-range', align: 'end' },
    { key: 'receivedQuantity', label: this.grText('receivedQty'), value: row => row.receivedQuantity, display: row => this.formatQuantity(row.receivedQuantity), filter: 'number-range', align: 'end' },
    { key: 'acceptedQuantity', label: this.grText('acceptedQty'), value: row => row.acceptedQuantity, display: row => this.formatQuantity(row.acceptedQuantity), filter: 'number-range', align: 'end' },
    { key: 'rejectedQuantity', label: this.grText('rejectedQty'), value: row => row.rejectedQuantity, display: row => this.formatQuantity(row.rejectedQuantity), filter: 'number-range', align: 'end' },
    { key: 'damagedQuantity', label: this.grText('damagedQty'), value: row => row.damagedQuantity ?? 0, display: row => this.formatQuantity(row.damagedQuantity ?? 0), filter: 'number-range', align: 'end' },
    { key: 'remainingReceivableQuantityAfter', label: this.grText('remainingReceivableAfter'), value: row => row.remainingReceivableQuantityAfter, display: row => this.formatQuantity(row.remainingReceivableQuantityAfter), filter: 'number-range', align: 'end' },
    { key: 'damageNotes', label: this.grText('damageNotes'), value: row => row.damageNotes || this.grText('notAvailable'), filter: 'text' },
    ];
  }

  gridScopeLabel(): string {
    return this.language.language() === 'ar' ? 'تطبق تصفية الشبكة وترتيبها على الإيصالات المحملة فقط' : 'Grid filters and sorting apply to loaded receipts only';
  }

  private readonly copy: Record<'en' | 'ar', Record<string, string>> = {
    en: {
      goodsReceiptKicker: 'Procurement / Inventory Inbound',
      goodsReceipts: 'Goods Receipts',
      goodsReceipt: 'Goods Receipt',
      goodsReceiptsLead: 'Record physical inbound delivery against Confirmed Purchase Orders with server-authoritative warehouse validation.',
      newGoodsReceipt: 'New Goods Receipt',
      goodsReceiptBoundary: 'This slice records physical goods receipt, quality inspection (accepted / rejected / damaged), and receivable balance updates. Downstream invoice handoff and accounting remain decoupled.',
      loadingGoodsReceipts: 'Loading Goods Receipts…',
      goodsReceiptListLoadFailed: 'The Goods Receipt list could not be loaded safely.',
      goodsReceiptSearch: 'Search supplier, reference note, or receipt ID',
      goodsReceiptStatusFilter: 'Status filter',
      goodsReceiptAllStatuses: 'All statuses',
      goodsReceiptFilterNote: 'Results are Tenant- and server-authorized Company/Branch scoped.',
      noGoodsReceipts: 'No Goods Receipts yet',
      noGoodsReceiptsLead: 'Capture the first physical delivery from a Confirmed Purchase Order.',
      goodsReceiptReferenceColumn: 'Receipt Reference',
      goodsReceiptSupplierColumn: 'Supplier',
      goodsReceiptStatusColumn: 'Status',
      goodsReceiptDateColumn: 'Received Date',
      goodsReceiptAcceptedColumn: 'Accepted Qty',
      goodsReceiptRejectedColumn: 'Rejected Qty',
      goodsReceiptUpdatedColumn: 'Updated',
      goodsReceiptLines: 'lines',
      createGoodsReceipt: 'Record Goods Receipt',
      goodsReceiptCreateLead: 'Select an eligible Confirmed Purchase Order and authorized warehouse to capture the inbound delivery.',
      backToGoodsReceipts: 'Back to Goods Receipts',
      goodsReceiptSourceRule: 'Only Confirmed or Partially Confirmed Purchase Orders with positive remaining receivable quantity are eligible.',
      loadingGoodsReceiptSources: 'Loading eligible sources and warehouses…',
      goodsReceiptSourceLoadFailed: 'Eligible receipt sources could not be loaded safely.',
      goodsReceiptSourceField: 'Confirmed Purchase Order',
      selectGoodsReceiptSource: 'Select an eligible Purchase Order',
      goodsReceiptWarehouse: 'Destination Warehouse',
      selectWarehouse: 'Select an authorized warehouse',
      goodsReceiptDate: 'Received Date',
      goodsReceiptReferenceNote: 'Delivery / Reference Note (optional)',
      goodsReceiptReferenceNotePlaceholder: 'e.g. DN-2026-001 or Supplier Packing Slip',
      goodsReceiptNotes: 'Receipt Notes (optional)',
      goodsReceiptNotesPlaceholder: 'Optional receiving observations or dock notes',
      goodsReceiptLineEntryTitle: 'Line Item Receipt & Inspection',
      goodsReceiptLineEntryLead: 'Enter received, accepted, and rejected quantities. Received must equal Accepted + Rejected. Damaged is recorded independently for damaged units.',
      goodsReceiptProductColumn: 'Product',
      goodsReceiptConfirmedQty: 'Confirmed',
      goodsReceiptRemainingQty: 'Receivable Remainder',
      goodsReceiptReceivedQty: 'Received',
      goodsReceiptAcceptedQty: 'Accepted',
      goodsReceiptRejectedQty: 'Rejected',
      goodsReceiptDamagedQty: 'Damaged',
      goodsReceiptDamageNotes: 'Damage Notes',
      damageNotesPlaceholder: 'Describe physical condition',
      goodsReceiptSelectSourceLead: 'Select an eligible Purchase Order above to populate receivable lines.',
      saving: 'Recording…',
      recordGoodsReceipt: 'Record Goods Receipt',
      receiptDetails: 'Receipt Details',
      goodsReceiptSummaryTitle: 'Receipt & Source Facts',
      goodsReceiptPurchaseOrder: 'Purchase Order ID',
      goodsReceiptCreatedAt: 'Recorded At',
      goodsReceiptVersion: 'Version',
      goodsReceiptLinesTitle: 'Recorded Receipt Line Items',
      orderedAtReceipt: 'Ordered at Receipt',
      receivedQty: 'Received',
      acceptedQty: 'Accepted',
      rejectedQty: 'Rejected',
      damagedQty: 'Damaged',
      remainingReceivableAfter: 'Remainder After',
      damageNotes: 'Damage Notes',
      inventoryMovement: 'Inventory Movement',
      postToInventory: 'Post accepted quantity',
      lifecycleHistory: 'Lifecycle History',
      auditEvidence: 'Audit Evidence',
      noHistory: 'No lifecycle history recorded.',
      noAudit: 'No audit evidence recorded.',
      notAvailable: '—',
      cancelGoodsReceipt: 'Cancel Goods Receipt',
      cancelGoodsReceiptTitle: 'Cancel Goods Receipt?',
      cancelGoodsReceiptLead: 'Cancelling restores the remaining receivable quantity on the Purchase Order. Cancellation is blocked if referenced by an active Purchase Invoice Handoff.',
      cancellationReason: 'Cancellation Reason',
      cancellationReasonHint: 'Explain why this goods receipt is being cancelled',
      confirmCancel: 'Confirm Cancellation',
      goodsReceiptActions: 'Goods Receipt Actions',
      goodsReceiptSections: 'Goods Receipt Sections',
      goodsReceiptCancelledNotice: 'This Goods Receipt has been cancelled.',
      noCancellationReason: 'No reason provided',
      unbalancedQuantityError: 'For each line, Received Quantity must equal Accepted Quantity + Rejected Quantity.',
      damagedExceedsReceivedError: 'Damaged Quantity cannot exceed Received Quantity.',
      overReceiptError: 'Accepted Quantity cannot exceed the Remaining Receivable Quantity.',
      noPositiveReceivedError: 'At least one line must have a Received Quantity greater than zero.',
      missingWarehouseError: 'Please select a destination warehouse.',
      statusRecorded: 'Recorded',
      statusCancelled: 'Cancelled',
      tabSummary: 'Summary',
      tabLines: 'Lines',
      tabHistory: 'History',
      tabAudit: 'Audit',
    },
    ar: {
      goodsReceiptKicker: 'المشتريات / استلام المخزون الوارد',
      goodsReceipts: 'سندات استلام البضائع',
      goodsReceipt: 'سند استلام بضاعة',
      goodsReceiptsLead: 'تسجيل الاستلام الفعلي للبضائع الموردة مقابل أوامر الشراء المؤكدة مع التحقق المعتمد من المستودع.',
      newGoodsReceipt: 'سند استلام جديد',
      goodsReceiptBoundary: 'تسجل هذه الشريحة استلام البضائع الفعلي والفحص النوعي (المقبول / المرفوض / التالف) وتحديث الرصيد المتبقي. تظل مطابقة الفاتورة والقيود المالية مسارات لاحقة.',
      loadingGoodsReceipts: 'جارٍ تحميل سندات الاستلام…',
      goodsReceiptListLoadFailed: 'تعذّر تحميل قائمة سندات الاستلام بأمان.',
      goodsReceiptSearch: 'البحث بالمورد أو المرجع أو المعرف',
      goodsReceiptStatusFilter: 'مرشح الحالة',
      goodsReceiptAllStatuses: 'كل الحالات',
      goodsReceiptFilterNote: 'النتائج معتمدة من الخادم ومقيدة بسياق العميل والشركة/الفرع.',
      noGoodsReceipts: 'لا توجد سندات استلام بعد',
      noGoodsReceiptsLead: 'سجل أول استلام فعلي من أمر شراء مؤكد.',
      goodsReceiptReferenceColumn: 'مرجع الاستلام',
      goodsReceiptSupplierColumn: 'المورد',
      goodsReceiptStatusColumn: 'الحالة',
      goodsReceiptDateColumn: 'تاريخ الاستلام',
      goodsReceiptAcceptedColumn: 'الكمية المقبولة',
      goodsReceiptRejectedColumn: 'الكمية المرفوضة',
      goodsReceiptUpdatedColumn: 'آخر تحديث',
      goodsReceiptLines: 'بنود',
      createGoodsReceipt: 'تسجيل سند استلام',
      goodsReceiptCreateLead: 'اختر أمر شراء مؤهلاً ومستودعاً معتمداً لتسجيل الاستلام الفعلي.',
      backToGoodsReceipts: 'العودة إلى سندات الاستلام',
      goodsReceiptSourceRule: 'أوامر الشراء المؤكدة ذات الرصيد المتبقي الإيجابي هي المؤهلة فقط.',
      loadingGoodsReceiptSources: 'جارٍ تحميل الأوامر المؤهلة والمستودعات…',
      goodsReceiptSourceLoadFailed: 'تعذّر تحميل مصادر الاستلام المؤهلة.',
      goodsReceiptSourceField: 'أمر الشراء المؤكد',
      selectGoodsReceiptSource: 'اختر أمر شراء مؤهلاً',
      goodsReceiptWarehouse: 'المستودع الوجهة',
      selectWarehouse: 'اختر مستودعاً معتمداً',
      goodsReceiptDate: 'تاريخ الاستلام',
      goodsReceiptReferenceNote: 'مذكرة التسليم / المرجع (اختياري)',
      goodsReceiptReferenceNotePlaceholder: 'مثال: إشعار تسليم المورد',
      goodsReceiptNotes: 'ملاحظات الاستلام (اختياري)',
      goodsReceiptNotesPlaceholder: 'أي ملاحظات تشغيلية عند رصيف الاستلام',
      goodsReceiptLineEntryTitle: 'إدخال البنود والفحص',
      goodsReceiptLineEntryLead: 'أدخل الكمية المستلمة والمقبولة والمرفوضة. المستلم = المقبول + المرفوض. التالف يسجل بشكل وصفي مستقل.',
      goodsReceiptProductColumn: 'المنتج',
      goodsReceiptConfirmedQty: 'المؤكد',
      goodsReceiptRemainingQty: 'المتبقي للاستلام',
      goodsReceiptReceivedQty: 'المستلم',
      goodsReceiptAcceptedQty: 'المقبول',
      goodsReceiptRejectedQty: 'المرفوض',
      goodsReceiptDamagedQty: 'التالف',
      goodsReceiptDamageNotes: 'ملاحظات التلف',
      damageNotesPlaceholder: 'وصف حالة التلف',
      goodsReceiptSelectSourceLead: 'اختر أمر شراء أعلاه لعرض البنود القابلة للاستلام.',
      saving: 'جارٍ التسجيل…',
      recordGoodsReceipt: 'حفظ سند الاستلام',
      receiptDetails: 'تفاصيل الاستلام',
      goodsReceiptSummaryTitle: 'بيانات الاستلام والمصدر',
      goodsReceiptPurchaseOrder: 'معرف أمر الشراء',
      goodsReceiptCreatedAt: 'تاريخ التسجيل',
      goodsReceiptVersion: 'النسخة',
      goodsReceiptLinesTitle: 'بنود سند الاستلام',
      orderedAtReceipt: 'المطلوب عند الاستلام',
      receivedQty: 'المستلم',
      acceptedQty: 'المقبول',
      rejectedQty: 'المرفوض',
      damagedQty: 'التالف',
      remainingReceivableAfter: 'المتبقي بعد الاستلام',
      damageNotes: 'ملاحظات التلف',
      inventoryMovement: 'حركة المخزون',
      postToInventory: 'ترحيل الكمية المقبولة',
      lifecycleHistory: 'سجل دورة الحياة',
      auditEvidence: 'دليل التدقيق',
      noHistory: 'لا يوجد سجل دورة حياة.',
      noAudit: 'لا توجد أدلة تدقيق.',
      notAvailable: '—',
      cancelGoodsReceipt: 'إلغاء سند الاستلام',
      cancelGoodsReceiptTitle: 'إلغاء سند الاستلام؟',
      cancelGoodsReceiptLead: 'يؤدي الإلغاء إلى إعادة الرصيد القابل للاستلام لأمر الشراء. يُمنع الإلغاء إذا كان السند مرتبطاً بفاتورة نشطة.',
      cancellationReason: 'سبب الإلغاء',
      cancellationReasonHint: 'وضح سبب إلغاء هذا السند',
      confirmCancel: 'تأكيد الإلغاء',
      goodsReceiptActions: 'إجراءات سند الاستلام',
      goodsReceiptSections: 'أقسام سند الاستلام',
      goodsReceiptCancelledNotice: 'تم إلغاء سند الاستلام هذا.',
      noCancellationReason: 'لم يُقدَّم سبب',
      unbalancedQuantityError: 'يجب أن تساوي الكمية المستلمة مجموع المقبول والمرفوض لكل بند.',
      damagedExceedsReceivedError: 'لا يمكن أن تتجاوز الكمية التالفة الكمية المستلمة.',
      overReceiptError: 'لا يمكن أن تتجاوز الكمية المقبولة الرصيد المتبقي للاستلام.',
      noPositiveReceivedError: 'يجب استلام كمية أكبر من الصفر في بند واحد على الأقل.',
      missingWarehouseError: 'يرجى اختيار مستودع وجهة معتمد.',
      statusRecorded: 'مسجل',
      statusCancelled: 'ملغي',
      tabSummary: 'الملخص',
      tabLines: 'البنود',
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

  grText(key: string): string {
    const lang = this.language.language() as 'en' | 'ar';
    return this.copy[lang]?.[key] ?? this.copy.en[key] ?? key;
  }

  statusLabel(status: GoodsReceiptStatus): string {
    return status === 'Recorded' ? this.grText('statusRecorded') : status === 'Cancelled' ? this.grText('statusCancelled') : status;
  }

  statusClass(status: GoodsReceiptStatus): string {
    return status === 'Recorded' ? 'status-badge--recorded' : status === 'Cancelled' ? 'status-badge--cancelled' : '';
  }

  tabLabel(tab: DetailTab): string {
    switch (tab) {
      case 'summary': return this.grText('tabSummary');
      case 'lines': return this.grText('tabLines');
      case 'history': return this.grText('tabHistory');
      case 'audit': return this.grText('tabAudit');
    }
  }

  tabId(tab: DetailTab): string { return `gr-tab-${tab}`; }
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
      const [sources, warehouses] = await Promise.all([
        firstValueFrom(this.service.eligibleSources()),
        firstValueFrom(this.service.warehouses()),
      ]);
      this.eligibleSources.set(sources);
      this.warehouses.set(warehouses.filter(w => w.isActive));
      if (warehouses.length > 0 && !this.createWarehouseId) {
        this.createWarehouseId = warehouses[0].warehouseId;
      }
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
      purchaseOrderLineId: line.purchaseOrderLineId,
      productSku: line.productSku,
      productName: line.productName,
      unitOfMeasureCode: line.unitOfMeasureCode,
      confirmedQuantity: line.confirmedQuantity,
      alreadyReceivedQuantity: line.alreadyReceivedQuantity,
      remainingReceivableQuantity: line.remainingReceivableQuantity,
      unitPrice: line.unitPrice,
      receivedQuantity: line.remainingReceivableQuantity,
      acceptedQuantity: line.remainingReceivableQuantity,
      rejectedQuantity: 0,
      damagedQuantity: null,
      damageNotes: '',
      notes: '',
    }));
  }

  onLineReceivedChange(line: CreateReceiptLineDraft): void {
    line.acceptedQuantity = line.receivedQuantity;
    line.rejectedQuantity = 0;
    this.validateCreateForm();
  }

  onLineAcceptedChange(line: CreateReceiptLineDraft): void {
    line.rejectedQuantity = Math.max(0, line.receivedQuantity - line.acceptedQuantity);
    this.validateCreateForm();
  }

  onLineRejectedChange(line: CreateReceiptLineDraft): void {
    line.acceptedQuantity = Math.max(0, line.receivedQuantity - line.rejectedQuantity);
    this.validateCreateForm();
  }

  validateCreateForm(): boolean {
    if (!this.createWarehouseId) {
      this.validationError.set(this.grText('missingWarehouseError'));
      return false;
    }

    const totalReceived = this.createLines.reduce((sum, l) => sum + (l.receivedQuantity || 0), 0);
    if (totalReceived <= 0) {
      this.validationError.set(this.grText('noPositiveReceivedError'));
      return false;
    }

    for (const line of this.createLines) {
      if ((line.acceptedQuantity + line.rejectedQuantity) !== line.receivedQuantity) {
        this.validationError.set(this.grText('unbalancedQuantityError'));
        return false;
      }
      if ((line.damagedQuantity ?? 0) > line.receivedQuantity) {
        this.validationError.set(this.grText('damagedExceedsReceivedError'));
        return false;
      }
      if (line.acceptedQuantity > line.remainingReceivableQuantity) {
        this.validationError.set(this.grText('overReceiptError'));
        return false;
      }
    }

    this.validationError.set(null);
    return true;
  }

  canSubmitCreate(): boolean {
    return !!this.selectedSourceId() && !!this.createWarehouseId && this.createLines.length > 0 && !this.validationError();
  }

  async createReceipt(): Promise<void> {
    if (!this.validateCreateForm()) return;
    this.saving.set(true);
    this.error.set(null);

    const payload: GoodsReceiptCreateRequest = {
      purchaseOrderId: this.selectedSourceId(),
      warehouseId: this.createWarehouseId,
      receivedDate: this.createReceivedDate,
      referenceNote: this.createReferenceNote.trim() || null,
      notes: this.createNotes.trim() || null,
      lines: this.createLines
        .filter(l => l.receivedQuantity > 0)
        .map(l => ({
          purchaseOrderLineId: l.purchaseOrderLineId,
          receivedQuantity: l.receivedQuantity,
          acceptedQuantity: l.acceptedQuantity,
          rejectedQuantity: l.rejectedQuantity,
          damagedQuantity: l.damagedQuantity && l.damagedQuantity > 0 ? l.damagedQuantity : null,
          damageNotes: l.damageNotes.trim() || null,
          notes: l.notes.trim() || null,
        })),
    };

    try {
      const response = await this.service.create(payload);
      void this.router.navigate(['/app/procurement/goods-receipts', response.id]);
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
      const [receipt, history, audit] = await Promise.all([
        firstValueFrom(this.service.get(id)),
        firstValueFrom(this.service.history(id)),
        firstValueFrom(this.service.audit(id)),
      ]);
      this.receipt.set(receipt);
      this.history.set(history);
      this.audit.set(audit);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.loading.set(false);
    }
  }

  async postAcceptedLine(receipt: GoodsReceiptResponse, line: GoodsReceiptResponse['lines'][number]): Promise<void> {
    if (!this.inventory || receipt.status !== 'Recorded' || line.acceptedQuantity <= 0) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.inventory.postGoodsReceipt(receipt.id, line.id, receipt.version);
      await this.loadDetail(receipt.id);
    } catch (err) {
      this.error.set(toSafeUiError(err));
    } finally {
      this.saving.set(false);
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
    const current = this.receipt();
    if (!current) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.service.cancel(current.id, current.version, this.cancelReason);
      this.receipt.set(updated);
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

  errorText(error: SafeUiError): string {
    return error.status === 409 || error.code === 'concurrency_conflict'
      ? this.language.text('prConcurrencyConflictError')
      : error.status === 403 || error.code === 'access_denied' || error.code === 'permission_denied'
        ? this.language.text('accessDenied')
        : this.language.text('requestError');
  }
}
