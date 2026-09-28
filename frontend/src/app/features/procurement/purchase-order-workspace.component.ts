import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyPresentationService } from '../../core/presentation/currency-presentation.service';
import { DataGridAction, DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import {
  PurchaseOrderConfirmationRequest,
  PurchaseOrderConfirmationStatus,
  PurchaseOrderHistoryResponse,
  PurchaseOrderLineResponse,
  PurchaseOrderListItemResponse,
  PurchaseOrderResponse,
  PurchaseOrderSourceOptionResponse,
  PurchaseOrderStatus,
} from './purchase-order.model';
import { PurchaseOrderService } from './purchase-order.service';

type WorkspaceMode = 'list' | 'create' | 'edit' | 'detail';
type DetailTab = 'summary' | 'lines' | 'confirmations' | 'history' | 'audit';
type ActionName = 'reject' | 'return-for-change' | 'cancel' | 'supplier-change-reject';

interface EditLine {
  id: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  orderedQuantity: number;
  unitPrice: number;
  deliveryDate: string;
  notes: string;
}

interface ConfirmationLineDraft {
  id: string;
  productSku: string;
  productName: string;
  orderedQuantity: number;
  confirmedQuantity: number;
  expectedDeliveryDate: string;
  proposedQuantity: number | null;
  proposedUnitPrice: number | null;
  proposedDeliveryDate: string;
  changeReason: string;
}

@Component({
  selector: 'app-purchase-order-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink, DataGridComponent],
  template: `
    @if (mode() === 'list') {
      <section class="ui-page purchase-order-page" data-testid="purchase-order-list">
        <header class="ui-page-header ui-page-header--compact page-header">
          <div>
            <p class="eyebrow">{{ poText('purchaseOrderKicker') }}</p>
            <h1>{{ poText('purchaseOrders') }}</h1>
            <p class="lede">{{ poText('purchaseOrdersLead') }}</p>
          </div>
          <a class="button button--primary" routerLink="/app/procurement/purchase-orders/new" data-testid="new-purchase-order"><svg class="icon" aria-hidden="true"><use href="#icon-plus" /></svg>{{ poText('newPurchaseOrder') }}</a>
        </header>
        <div class="boundary-note" role="note"><svg class="icon" aria-hidden="true"><use href="#icon-info" /></svg><span>{{ poText('purchaseOrderBoundary') }}</span></div>

        @if (loading()) {
          <section class="ui-surface state-card" aria-live="polite"><span class="spinner" aria-hidden="true"></span><h2>{{ poText('loadingPurchaseOrders') }}</h2></section>
        } @else if (error(); as currentError) {
          <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ poText('purchaseOrderListLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadList()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface ledger-panel">
            <div class="filter-toolbar">
              <label class="filter-search"><svg class="icon" aria-hidden="true"><use href="#icon-search" /></svg><input type="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="poText('purchaseOrderSearch')" [attr.aria-label]="poText('purchaseOrderSearch')" /></label>
              <label class="filter-field"><span>{{ poText('purchaseOrderStatusFilter') }}</span><span class="filter-select"><select [value]="statusFilter()" (change)="setStatusFilter($any($event.target).value)"><option value="">{{ poText('purchaseOrderAllStatuses') }}</option>@for (status of statuses; track status) {<option [value]="status">{{ statusLabel(status) }}</option>}</select><svg class="icon filter-select__chevron" aria-hidden="true"><use href="#icon-chevron-down" /></svg></span></label>
              <p class="filter-note">{{ poText('purchaseOrderFilterNote') }}</p>
            </div>
            <app-data-grid
              [rows]="filteredRecords()"
              [columns]="gridColumns()"
              [language]="language.language()"
              [caption]="poText('purchaseOrders')"
              [rowKey]="'id'"
              [rowActions]="[{ key: 'view', label: gridText('View Purchase Order', 'عرض أمر الشراء') }]"
              [loading]="loading()"
              [countLabel]="gridText('purchase orders', 'أوامر الشراء')"
              [loadingLabel]="poText('loadingPurchaseOrders')"
              [emptyLabel]="records().length ? gridText('No matching purchase orders', 'لا توجد أوامر شراء مطابقة') : poText('noPurchaseOrders')"
              [emptyHint]="records().length ? gridText('Clear search or filters to see orders.', 'امسح البحث أو عوامل التصفية لعرض الأوامر.') : poText('noPurchaseOrdersLead')"
              [noMatchesLabel]="gridText('No matching purchase orders', 'لا توجد أوامر شراء مطابقة')"
              [clearFiltersLabel]="gridText('Clear filter', 'مسح التصفية')"
              [filterInputLabel]="gridText('Filter', 'تصفية')"
              [allValuesLabel]="poText('purchaseOrderAllStatuses')"
              [fromLabel]="gridText('From', 'من')"
              [toLabel]="gridText('To', 'إلى')"
              [selectAllLabel]="gridText('Select visible purchase orders', 'تحديد أوامر الشراء الظاهرة')"
              [rowActionsLabel]="gridText('Purchase Order actions', 'إجراءات أمر الشراء')"
              [resizeLabelPrefix]="gridText('Resize', 'تغيير العرض')"
              [previousPageLabel]="gridText('Previous page', 'الصفحة السابقة')"
              [nextPageLabel]="gridText('Next page', 'الصفحة التالية')"
              [perPageLabel]="gridText('per page', 'في الصفحة')"
              (rowAction)="onGridAction($event)" />
          </section>
        }
      </section>
    }

    @if (mode() === 'create') {
      <section class="ui-page purchase-order-page" data-testid="purchase-order-create">
        <header class="ui-page-header ui-page-header--compact page-header"><div><p class="eyebrow">{{ poText('purchaseOrderKicker') }}</p><h1>{{ poText('createPurchaseOrder') }}</h1><p class="lede">{{ poText('purchaseOrderCreateLead') }}</p></div><a class="button button--secondary" routerLink="/app/procurement/purchase-orders">{{ poText('backToPurchaseOrders') }}</a></header>
        <div class="boundary-note" role="note"><svg class="icon" aria-hidden="true"><use href="#icon-info" /></svg><span>{{ poText('purchaseOrderSourceRule') }}</span></div>
        @if (loading()) { <section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ poText('loadingPurchaseOrderSources') }}</h2></section> }
        @else if (error(); as currentError) { <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ poText('purchaseOrderSourceLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="loadSources()">{{ language.text('retry') }}</button></section> }
        @else {
          <section class="ui-surface form-card">
            <label class="field"><span class="field__label">{{ poText('purchaseOrderSourceField') }} *</span><select [ngModel]="selectedSourceId()" (ngModelChange)="selectedSourceId.set($event)" data-testid="purchase-order-source"><option value="">{{ poText('selectPurchaseOrderSource') }}</option>@for (source of sources(); track source.source.sourceDecisionId) {<option [value]="source.source.sourceDecisionId">{{ source.source.purchaseRequestReference }} · {{ source.source.supplier.name }} · {{ source.source.supplierQuotationReference }} · {{ source.source.currency.code }}</option>}</select></label>
            @if (selectedSource(); as source) {
              <section class="source-summary"><div><p class="section-kicker">{{ poText('purchaseOrderSourceSnapshot') }}</p><h2>{{ source.source.purchaseRequestReference }} · {{ source.source.supplier.name }}</h2><p>{{ source.source.sourceDecisionRationale }}</p></div><dl class="fact-grid"><div><dt>{{ poText('purchaseOrderSupplierColumn') }}</dt><dd>{{ source.source.supplier.name }}</dd></div><div><dt>{{ poText('purchaseOrderCurrencyColumn') }}</dt><dd>{{ source.source.currency.code }} · {{ source.source.currency.name }}</dd></div><div><dt>{{ poText('purchaseOrderScope') }}</dt><dd>{{ source.companyId }}{{ source.branchId ? ' · ' + source.branchId : '' }}</dd></div><div><dt>{{ poText('purchaseOrderSourceLines') }}</dt><dd>{{ source.lines.length }}</dd></div></dl><div class="ui-grid-shell"><table class="ui-grid compact-grid"><thead><tr><th scope="col">{{ poText('purchaseOrderProductColumn') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderQuantityColumn') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderUnitPriceColumn') }}</th><th scope="col">{{ poText('purchaseOrderDeliveryColumn') }}</th></tr></thead><tbody>@for (line of source.lines; track line.supplierQuotationLineId) {<tr><td><strong>{{ line.productSku }} · {{ line.productName }}</strong><small>{{ line.unitOfMeasureCode }}</small></td><td class="numeric">{{ formatQuantity(line.selectedQuantity) }}</td><td class="numeric">{{ formatMoney(line.unitPrice, source.source.currency.code) }}</td><td>{{ line.offeredDeliveryDate ? formatDate(line.offeredDeliveryDate) : poText('notAvailable') }}</td></tr>}</tbody></table></div></section>
              <div class="form-actions"><button class="button button--primary" type="button" [disabled]="saving()" (click)="create()" data-testid="create-purchase-order">{{ saving() ? poText('saving') : poText('createPurchaseOrder') }}</button></div>
            } @else { <div class="empty-inline">{{ poText('purchaseOrderSelectSourceLead') }}</div> }
          </section>
        }
      </section>
    }

    @if (mode() === 'edit' && order(); as currentOrder) {
      <section class="ui-page purchase-order-page" data-testid="purchase-order-edit"><header class="ui-page-header ui-page-header--compact page-header"><div><p class="eyebrow">{{ poText('purchaseOrderKicker') }}</p><h1>{{ poText('editPurchaseOrder') }}</h1><p class="lede">{{ currentOrder.source.purchaseRequestReference }} · {{ currentOrder.source.supplier.name }}</p></div><a class="button button--secondary" [routerLink]="['/app/procurement/purchase-orders', currentOrder.id]">{{ language.text('cancel') }}</a></header><section class="ui-surface form-card"><label class="field"><span class="field__label">{{ poText('purchaseOrderNotes') }}</span><textarea [(ngModel)]="editNotes" rows="3"></textarea></label><div class="ui-grid-shell"><table class="ui-grid compact-grid"><thead><tr><th scope="col">{{ poText('purchaseOrderProductColumn') }}</th><th scope="col">{{ poText('purchaseOrderQuantityColumn') }}</th><th scope="col">{{ poText('purchaseOrderUnitPriceColumn') }}</th><th scope="col">{{ poText('purchaseOrderDeliveryColumn') }}</th><th scope="col">{{ poText('purchaseOrderNotes') }}</th></tr></thead><tbody>@for (line of editLines; track line.id) {<tr><td><strong>{{ line.productSku }} · {{ line.productName }}</strong><small>{{ line.unitOfMeasureCode }}</small></td><td><input class="table-input numeric" type="number" min="0.000001" step="0.000001" [(ngModel)]="line.orderedQuantity" /></td><td><input class="table-input numeric" type="number" min="0" step="0.01" [(ngModel)]="line.unitPrice" /></td><td><input class="table-input" type="date" [(ngModel)]="line.deliveryDate" /></td><td><input class="table-input" type="text" maxlength="2048" [(ngModel)]="line.notes" /></td></tr>}</tbody></table></div><div class="form-actions"><button class="button button--secondary" type="button" [routerLink]="['/app/procurement/purchase-orders', currentOrder.id]">{{ language.text('cancel') }}</button><button class="button button--primary" type="button" [disabled]="saving()" (click)="saveEdit()">{{ saving() ? poText('saving') : poText('savePurchaseOrder') }}</button></div></section></section>
    }

    @if (mode() === 'detail' && !order() && error(); as currentError) {
      <section class="ui-page purchase-order-page" data-testid="purchase-order-detail-error">
        <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ poText('purchaseOrderListLoadFailed') }}</strong><p>{{ errorText(currentError) }}</p><button class="button button--secondary" type="button" (click)="retryDetail()">{{ language.text('retry') }}</button></section>
      </section>
    }

    @if (mode() === 'detail' && order(); as currentOrder) {
      <section class="ui-page purchase-order-page" data-testid="purchase-order-detail">
        <header class="ui-page-header ui-page-header--compact page-header"><div><p class="eyebrow">{{ poText('purchaseOrderKicker') }}</p><h1>{{ currentOrder.source.supplierQuotationReference }}</h1><p class="lede">{{ currentOrder.source.purchaseRequestReference }} · {{ currentOrder.source.supplier.name }} · {{ currentOrder.source.currency.code }}</p></div><span class="status-badge status-badge--hero" [class]="statusClass(currentOrder.status)"><span aria-hidden="true"></span>{{ statusLabel(currentOrder.status) }}</span></header>
        <div class="action-rail" role="toolbar" [attr.aria-label]="poText('purchaseOrderActions')"><a class="button button--secondary" routerLink="/app/procurement/purchase-orders">{{ poText('backToPurchaseOrders') }}</a>@if (currentOrder.canEdit) {<a class="button button--secondary" [routerLink]="['/app/procurement/purchase-orders', currentOrder.id, 'edit']">{{ poText('editPurchaseOrder') }}</a>}@if (currentOrder.canSubmit) {<button class="button button--primary" type="button" (click)="runSimpleAction('submit')">{{ poText('submitPurchaseOrder') }}</button>}@if (currentOrder.canApprove) {<button class="button button--primary" type="button" (click)="runSimpleAction('approve')">{{ poText('approvePurchaseOrder') }}</button>}@if (currentOrder.canIssue) {<button class="button button--primary" type="button" (click)="runSimpleAction('issue')">{{ poText('issuePurchaseOrder') }}</button>}@if (currentOrder.canCaptureConfirmation) {<button class="button button--primary" type="button" (click)="activeTab.set('confirmations')">{{ poText('recordSupplierConfirmation') }}</button>}@if (currentOrder.canApproveSupplierChange) {<button class="button button--primary" type="button" (click)="runSimpleAction('supplier-change-approve')">{{ poText('approveSupplierChange') }}</button>}@if (currentOrder.canRejectSupplierChange) {<button class="button button--danger" type="button" (click)="openAction('supplier-change-reject')">{{ poText('rejectSupplierChange') }}</button>}@if (currentOrder.canReject) {<button class="button button--danger" type="button" (click)="openAction('reject')">{{ poText('rejectPurchaseOrder') }}</button>}@if (currentOrder.canReturnForChange) {<button class="button button--secondary" type="button" (click)="openAction('return-for-change')">{{ poText('returnPurchaseOrderForChange') }}</button>}@if (currentOrder.canCancel) {<button class="button button--quiet" type="button" (click)="openAction('cancel')">{{ poText('cancelPurchaseOrder') }}</button>}</div>
        @if (currentOrder.status === 'Cancelled' || currentOrder.status === 'Rejected') {
          <section class="boundary-note terminal-recovery-note" role="note" data-testid="purchase-order-terminal-recovery"><strong>{{ terminalRecoveryTitle() }}</strong><span>{{ terminalRecoveryMessage() }}</span></section>
        }
        @if (error(); as currentError) {<div class="inline-error" role="alert">{{ errorText(currentError) }}</div>}
        <nav class="detail-tabs" role="tablist" [attr.aria-label]="poText('purchaseOrderSections')" (keydown)="onTabsKeydown($event)">@for (tab of tabs; track tab) {<button [id]="tabId(tab)" type="button" role="tab" [attr.aria-selected]="activeTab() === tab" [attr.aria-controls]="panelId(tab)" [attr.tabindex]="activeTab() === tab ? 0 : -1" [class.is-active]="activeTab() === tab" (click)="setTab(tab)">{{ tabLabel(tab) }}</button>}</nav>
        @for (tab of tabs; track tab) {
          @if (loading() || activeTab() !== tab) {<section class="sr-only" [id]="panelId(tab)" role="tabpanel" [attr.aria-labelledby]="tabId(tab)" hidden></section>}
        }
        @if (loading()) {<section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ poText('loadingPurchaseOrder') }}</h2></section>}
        @else if (activeTab() === 'summary') {
          <section class="detail-layout" [id]="panelId('summary')" role="tabpanel" [attr.aria-labelledby]="tabId('summary')"><section class="ui-surface detail-card"><p class="section-kicker">{{ poText('purchaseOrderSourceSnapshot') }}</p><h2>{{ poText('purchaseOrderImmutableLineage') }}</h2><p class="detail-copy">{{ poText('purchaseOrderLineageLead') }}</p><dl class="fact-grid"><div><dt>{{ poText('purchaseOrderPurchaseRequest') }}</dt><dd>{{ currentOrder.source.purchaseRequestReference }}</dd></div><div><dt>{{ poText('purchaseOrderSupplierColumn') }}</dt><dd>{{ currentOrder.source.supplier.name }} · {{ currentOrder.source.supplier.code }}</dd></div><div><dt>{{ poText('purchaseOrderCurrencyColumn') }}</dt><dd>{{ currentOrder.source.currency.code }} · {{ currentOrder.source.currency.name }}</dd></div><div><dt>{{ poText('purchaseOrderPaymentTerm') }}</dt><dd>{{ currentOrder.source.paymentTerm?.code || poText('notAvailable') }}</dd></div><div><dt>{{ poText('purchaseOrderCreatedAt') }}</dt><dd>{{ formatDateTime(currentOrder.createdAt) }}</dd></div><div><dt>{{ poText('purchaseOrderVersion') }}</dt><dd><code>{{ currentOrder.version }}</code></dd></div></dl></section><section class="ui-surface detail-card"><p class="section-kicker">{{ poText('purchaseOrderApproval') }}</p><h2>{{ currentOrder.approval ? currentOrder.approval.stageKey : poText('purchaseOrderNoApproval') }}</h2>@if (currentOrder.approval) {<dl class="fact-grid"><div><dt>{{ poText('purchaseOrderPolicy') }}</dt><dd>{{ currentOrder.approval.policyId }} · {{ currentOrder.approval.policyVersion }}</dd></div><div><dt>{{ poText('purchaseOrderApprovals') }}</dt><dd>{{ currentOrder.approval.recordedApprovals }}/{{ currentOrder.approval.requiredApprovals }}</dd></div><div><dt>{{ poText('purchaseOrderReapproval') }}</dt><dd>{{ currentOrder.approval.isReapproval ? poText('yes') : poText('no') }}</dd></div></dl>}<p class="detail-copy">{{ currentOrder.notes || poText('purchaseOrderNoNotes') }}</p></section></section>
        } @else if (activeTab() === 'lines') {
          <section class="ui-surface detail-card" [id]="panelId('lines')" role="tabpanel" [attr.aria-labelledby]="tabId('lines')"><p class="section-kicker">{{ poText('purchaseOrderLines') }}</p><h2>{{ poText('purchaseOrderLineSnapshots') }}</h2><div class="ui-grid-shell"><table class="ui-grid detail-grid"><thead><tr><th scope="col">{{ poText('purchaseOrderProductColumn') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderOrderedQuantity') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderConfirmedQuantity') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderRemainingQuantity') }}</th><th scope="col" class="numeric">{{ poText('purchaseOrderUnitPriceColumn') }}</th><th scope="col">{{ poText('purchaseOrderDeliveryColumn') }}</th></tr></thead><tbody>@for (line of currentOrder.lines; track line.id) {<tr><td><strong>{{ line.productSku }} · {{ line.productName }}</strong><small>{{ line.unitOfMeasureCode }} · {{ line.notes || poText('notAvailable') }}</small></td><td class="numeric">{{ formatQuantity(line.orderedQuantity) }}</td><td class="numeric">{{ formatQuantity(line.confirmedQuantity) }}</td><td class="numeric" [class.remaining-alert]="line.remainingQuantity > 0">{{ formatQuantity(line.remainingQuantity) }}</td><td class="numeric money">{{ formatMoney(line.unitPrice, currentOrder.source.currency.code) }}</td><td>{{ line.deliveryDate ? formatDate(line.deliveryDate) : poText('notAvailable') }}</td></tr>}</tbody></table></div></section>
        } @else if (activeTab() === 'confirmations') {
          <section class="confirmation-layout" [id]="panelId('confirmations')" role="tabpanel" [attr.aria-labelledby]="tabId('confirmations')"><section class="ui-surface form-card"><p class="section-kicker">{{ poText('recordSupplierConfirmation') }}</p><h2>{{ poText('supplierConfirmationTitle') }}</h2><p class="detail-copy">{{ poText('supplierConfirmationLead') }}</p><label class="field"><span class="field__label">{{ poText('supplierConfirmationStatus') }}</span><select [(ngModel)]="confirmationStatus"><option value="Confirmed">{{ confirmationStatusLabel('Confirmed') }}</option><option value="PartiallyConfirmed">{{ confirmationStatusLabel('PartiallyConfirmed') }}</option><option value="Rejected">{{ confirmationStatusLabel('Rejected') }}</option><option value="NoResponse">{{ confirmationStatusLabel('NoResponse') }}</option></select></label><div class="ui-grid-shell"><table class="ui-grid compact-grid"><thead><tr><th scope="col">{{ poText('purchaseOrderProductColumn') }}</th><th scope="col">{{ poText('purchaseOrderOrderedQuantity') }}</th><th scope="col">{{ poText('purchaseOrderConfirmedQuantity') }}</th><th scope="col">{{ poText('purchaseOrderSupplierChange') }}</th></tr></thead><tbody>@for (line of confirmationLines; track line.id) {<tr><td><strong>{{ line.productSku }} · {{ line.productName }}</strong></td><td class="numeric">{{ formatQuantity(line.orderedQuantity) }}</td><td><input class="table-input numeric" type="number" min="0" [max]="line.orderedQuantity" step="0.000001" [(ngModel)]="line.confirmedQuantity" /></td><td><div class="change-fields"><input class="table-input numeric" type="number" min="0" placeholder="{{ poText('purchaseOrderProposedQuantity') }}" [(ngModel)]="line.proposedQuantity" /><input class="table-input numeric" type="number" min="0" placeholder="{{ poText('purchaseOrderProposedPrice') }}" [(ngModel)]="line.proposedUnitPrice" /><input class="table-input" type="date" [(ngModel)]="line.proposedDeliveryDate" /><input class="table-input" type="text" maxlength="4096" [placeholder]="poText('purchaseOrderChangeReason')" [(ngModel)]="line.changeReason" /></div></td></tr>}</tbody></table></div><div class="confirmation-meta"><label class="field"><span class="field__label">{{ poText('supplierReference') }}</span><input [(ngModel)]="supplierReference" /></label><label class="field"><span class="field__label">{{ poText('supplierContact') }}</span><input [(ngModel)]="supplierContact" /></label><label class="field"><span class="field__label">{{ poText('purchaseOrderNotes') }}</span><textarea [(ngModel)]="confirmationNotes" rows="2"></textarea></label><label class="field"><span class="field__label">{{ poText('purchaseOrderReason') }}</span><textarea [(ngModel)]="confirmationReason" rows="2"></textarea></label><label class="field"><span class="field__label">{{ poText('purchaseOrderEvidenceReference') }}</span><input [(ngModel)]="evidenceReference" /></label></div><div class="form-actions"><button class="button button--primary" type="button" [disabled]="saving()" (click)="captureConfirmation()" data-testid="capture-supplier-confirmation">{{ saving() ? poText('saving') : poText('recordSupplierConfirmation') }}</button></div></section><section class="ui-surface detail-card"><p class="section-kicker">{{ poText('supplierConfirmationHistory') }}</p><h2>{{ poText('supplierConfirmationHistory') }}</h2>@if (confirmations().length === 0) {<div class="empty-inline">{{ poText('noSupplierConfirmations') }}</div>} @else {<ol class="timeline">@for (confirmation of confirmations(); track confirmation.id) {<li><strong>{{ confirmationStatusLabel(confirmation.status) }}</strong><small>{{ formatDate(confirmation.responseDate) }} · {{ formatDateTime(confirmation.recordedAt) }}</small><p>{{ confirmation.reason || confirmation.notes || poText('notAvailable') }}</p>@if (confirmation.changes.length > 0) {<span class="change-pill">{{ confirmation.changes.length }} {{ poText('purchaseOrderChanges') }}</span>}</li>}</ol>}</section></section>
        } @else if (activeTab() === 'history') {
          <section class="ui-surface detail-card" [id]="panelId('history')" role="tabpanel" [attr.aria-labelledby]="tabId('history')"><p class="section-kicker">{{ poText('purchaseOrderHistory') }}</p><h2>{{ poText('purchaseOrderHistory') }}</h2>@if (history().length === 0) {<div class="empty-inline">{{ poText('noPurchaseOrderHistory') }}</div>} @else {<ol class="timeline">@for (entry of history(); track entry.evidenceId) {<li><strong>{{ statusLabel(entry.fromStatus) }} → {{ statusLabel(entry.toStatus) }}</strong><small>{{ actionLabel(entry.action) }} · {{ formatDateTime(entry.occurredAt) }}</small><p>{{ entry.reason || poText('notAvailable') }}</p></li>}</ol>}</section>
        } @else {
          <section class="ui-surface detail-card" [id]="panelId('audit')" role="tabpanel" [attr.aria-labelledby]="tabId('audit')"><p class="section-kicker">{{ poText('purchaseOrderAudit') }}</p><h2>{{ poText('purchaseOrderAudit') }}</h2>@if (audit().length === 0) {<div class="empty-inline">{{ poText('noPurchaseOrderAudit') }}</div>} @else {<div class="audit-list">@for (entry of audit(); track entry.evidenceId) {<article><strong>{{ entry.operationId }}</strong><small>{{ entry.decision }} · {{ formatDateTime(entry.occurredAt) }}</small><p>{{ entry.reason || entry.afterSummary || poText('notAvailable') }}</p><code>{{ entry.correlationId }}</code></article>}</div>}</section>
        }
      </section>
    }

    @if (dialogAction(); as action) {<div class="dialog-backdrop" role="presentation" (click)="closeAction()"><section class="action-dialog" role="dialog" aria-modal="true" aria-labelledby="purchase-order-action-title" tabindex="-1" (click)="$event.stopPropagation()" (keydown)="onActionDialogKeydown($event)"><p class="section-kicker">{{ poText('purchaseOrderKicker') }}</p><h2 id="purchase-order-action-title">{{ actionTitle(action) }}</h2><p>{{ actionLead(action) }}</p><label class="field"><span class="field__label">{{ poText('purchaseOrderReason') }} *</span><textarea [(ngModel)]="actionReason" rows="4" [placeholder]="poText('purchaseOrderReasonHint')"></textarea></label><div class="dialog-actions"><button id="purchase-order-action-cancel" class="button button--secondary" type="button" (click)="closeAction()">{{ language.text('cancel') }}</button><button class="button button--danger" type="button" [disabled]="saving()" (click)="confirmAction()">{{ saving() ? poText('saving') : actionTitle(action) }}</button></div></section></div>}
  `,
  styles: `
    :host { display: block; }
    .page-header { align-items: center; }
    .page-header .lede { max-width: 54rem; margin-bottom: 0; line-height: 1.55; }
    .button { display: inline-flex; align-items: center; justify-content: center; gap: .5rem; min-height: 42px; border: 1px solid transparent; border-radius: var(--radius-sm); padding: .52rem .9rem; color: var(--ink); background: linear-gradient(180deg, var(--surface-raised), var(--surface-tint)); box-shadow: var(--shadow-soft), inset 0 1px var(--control-gloss); font-size: 15px; font-weight: 600; text-decoration: none; cursor: pointer; transition: transform var(--motion-fast) ease, box-shadow var(--motion-fast) ease, filter var(--motion-fast) ease; }
    .button:hover:not(:disabled) { transform: translateY(-1px); }
    .button:disabled { cursor: wait; opacity: .55; }
    .button--primary { border-color: var(--accent-strong); color: var(--action-text); background: linear-gradient(180deg, color-mix(in srgb, var(--accent-action) 88%, white), var(--accent-action)); }
    .button--secondary { border-color: var(--line-strong); }
    .button--quiet { border-color: transparent; color: var(--ink-muted); background: transparent; }
    .button--danger { border-color: color-mix(in srgb, var(--danger) 45%, var(--line)); color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .boundary-note { display: flex; align-items: flex-start; gap: .65rem; border-inline-start: 3px solid var(--support); border-radius: 0 var(--radius-sm) var(--radius-sm) 0; padding: .72rem .9rem; color: var(--ink-muted); background: linear-gradient(120deg, var(--support-soft), color-mix(in srgb, var(--surface-glass) 62%, var(--support-soft))); box-shadow: var(--shadow-soft); font-size: 14px; line-height: 1.5; }
    .boundary-note > .icon { width: 19px; height: 19px; flex: none; color: var(--support); }
    .state-card { display: grid; justify-items: start; align-content: center; gap: .55rem; min-height: 12rem; }
    .state-card h2, .state-card p { margin: 0; }
    .state-card p, .detail-copy, .empty-inline { color: var(--ink-muted); font-size: 14px; line-height: 1.55; }
    .state-card--error, .inline-error { border-color: color-mix(in srgb, var(--danger) 32%, var(--line)); }
    .inline-error { margin-block: .9rem; border: 1px solid; border-radius: var(--radius-sm); padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: 14px; }
    .spinner { width: 2rem; height: 2rem; border: 3px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: po-spin 1s linear infinite; }
    @keyframes po-spin { to { transform: rotate(360deg); } }
    .ledger-panel { padding: 0; overflow: visible; }
    .filter-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: .7rem; padding: .85rem 1rem; border-bottom: 1px solid var(--line); background: color-mix(in srgb, var(--accent-soft) 70%, var(--surface-glass)); }
    .filter-search { position: relative; display: block; min-height: 44px; min-width: min(100%, 18rem); flex: 1 1 16rem; color: var(--accent); }
    .filter-search .icon { position: absolute; z-index: 1; inset-block-start: 50%; inset-inline-start: .8rem; width: 18px; height: 18px; pointer-events: none; transform: translateY(-50%); }
    .filter-search input { display: block; width: 100%; height: 44px; min-height: 44px !important; border: 1px solid var(--line-strong) !important; border-radius: var(--radius-control) !important; padding-block: 0 !important; padding-inline: 2.7rem .75rem !important; outline: 0 !important; color: var(--ink); background: linear-gradient(180deg, var(--surface-raised), var(--surface-tint)) !important; box-shadow: var(--shadow-soft), inset 0 1px var(--control-gloss) !important; font-size: 14px; }
    .filter-search input:focus { border-color: var(--accent) !important; box-shadow: 0 0 0 4px color-mix(in srgb, var(--accent) 28%, transparent), 0 5px 14px color-mix(in srgb, var(--accent) 12%, transparent), inset 0 1px var(--control-gloss) !important; animation: control-focus-ring 190ms ease-out; }
    .filter-field { display: grid; gap: .3rem; min-width: 12rem; color: var(--ink-muted); font-size: 14px; font-weight: 700; }
    .filter-select { position: relative; display: block; }
    .filter-field select { width: 100%; min-height: 44px; appearance: none; border: 1px solid var(--line-strong); border-radius: var(--radius-control); padding-block: .45rem; padding-inline: .65rem 2.6rem; color: var(--ink); background: linear-gradient(180deg, var(--surface-raised), var(--surface-tint)); box-shadow: inset 0 1px var(--control-gloss), var(--shadow-soft); font-size: 14px; }
    .filter-select__chevron { position: absolute; inset-inline-end: .75rem; inset-block-start: 50%; width: 16px; height: 16px; color: var(--ink-muted); pointer-events: none; transform: translateY(-50%); }
    .filter-note { flex: 1 1 100%; margin: 0; color: var(--ink-muted); font-size: 14px; }
    .purchase-order-grid-shell { border: 0; border-radius: 0; }
    .purchase-order-grid { min-width: 58rem; }
    .purchase-order-grid th, .purchase-order-grid td { padding: .68rem .6rem; }
    .purchase-order-grid td small, .detail-grid td small { display: block; margin-top: .16rem; color: var(--ink-muted); font-size: 14px; }
    .record-link { color: var(--ink); font-weight: 900; text-decoration: none; }
    .record-link:hover { color: var(--accent-strong); text-decoration: underline; }
    .currency-badge, .change-pill { display: inline-flex; border: 1px solid color-mix(in srgb, var(--accent-strong) 28%, var(--line)); border-radius: 99px; padding: .24rem .45rem; color: var(--accent-strong); background: var(--accent-soft); font-size: 14px; font-weight: 700; }
    .status-badge { display: inline-flex; min-height: 32px; align-items: center; gap: .4rem; border: 1px solid var(--line); border-radius: 99px; padding: .28rem .65rem; color: var(--ink-muted); background: var(--surface); font-size: 14px; font-weight: 700; white-space: nowrap; }
    .status-badge > span { width: .4rem; height: .4rem; border-radius: 50%; background: currentColor; }
    .status-badge--hero { align-self: center; padding: .45rem .7rem; font-size: 15px; }
    .status-badge--draft, .status-badge--returned { color: var(--support); background: var(--support-soft); }
    .status-badge--pending, .status-badge--changed { color: var(--warning); background: color-mix(in srgb, var(--warning) 10%, var(--surface-raised)); }
    .status-badge--approved, .status-badge--issued, .status-badge--confirmed { color: var(--success); background: var(--accent-soft); }
    .status-badge--partial { color: var(--accent-strong); background: var(--accent-soft); }
    .status-badge--rejected, .status-badge--cancelled { color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .status-badge--no-response { color: var(--ink-muted); background: var(--surface-tint); }
    .numeric { font-variant-numeric: tabular-nums; }
    .money { color: var(--ink-strong); font-weight: 800; }
    .empty-ledger { display: grid; justify-items: start; align-content: center; gap: .45rem; min-height: 17rem; padding: 2rem; }
    .empty-ledger > span { color: var(--accent-strong); font-size: 2.3rem; }
    .empty-ledger h2, .empty-ledger p { margin: 0; }
    .empty-ledger p { max-width: 34rem; }
    .form-card, .detail-card { display: grid; gap: 1rem; }
    .field { display: grid; gap: .5rem; color: var(--ink); font-size: 14px; }
    .field__label { color: var(--ink-muted); font-size: 14px; font-weight: 700; }
    .field input, .field select, .field textarea, .table-input { width: 100%; min-height: 44px; box-sizing: border-box; border: 1px solid var(--line-strong); border-radius: var(--radius-control); padding: .55rem .7rem; color: var(--ink); background: linear-gradient(180deg, var(--surface-raised), var(--surface-tint)); box-shadow: inset 0 1px var(--control-gloss), var(--shadow-soft); font: 14px/1.4 var(--font-sans); }
    .field textarea { resize: vertical; }
    .form-actions, .action-rail, .dialog-actions { display: flex; flex-wrap: wrap; align-items: center; gap: .55rem; }
    .form-actions { justify-content: flex-end; }
    .action-rail { margin-block: .9rem 1rem; }
    .source-summary { display: grid; gap: 1rem; border: 1px solid var(--line); border-radius: var(--radius-sm); padding: 1rem; background: var(--surface); }
    .source-summary h2, .source-summary p { margin: 0; }
    .section-kicker, .eyebrow { color: var(--ink-muted); font-size: 13px; font-weight: 800; letter-spacing: .1em; text-transform: uppercase; }
    .section-kicker { margin: 0; }
    .fact-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .8rem; margin: 0; }
    .fact-grid div { display: grid; gap: .2rem; }
    .fact-grid dt, .fact-grid span { color: var(--ink-muted); font-size: 14px; font-weight: 700; }
    .fact-grid dd { margin: 0; color: var(--ink); font-size: 14px; font-weight: 700; overflow-wrap: anywhere; }
    .detail-tabs { display: flex; gap: .3rem; overflow-x: auto; margin-block: 0 1rem; border-bottom: 1px solid var(--line); }
    .detail-tabs button { min-height: 42px; border: 0; border-bottom: 2px solid transparent; padding: .65rem .8rem; color: var(--ink-muted); background: transparent; font: 600 14px/1.3 var(--font-sans); cursor: pointer; white-space: nowrap; }
    .detail-tabs button.is-active { border-color: var(--accent-strong); color: var(--ink-strong); }
    .ui-grid-shell { overflow-x: auto; }
    .compact-grid, .detail-grid { min-width: 44rem; }
    .compact-grid th, .compact-grid td, .detail-grid th, .detail-grid td { padding: .65rem .55rem; }
    .table-input { min-width: 7rem; min-height: 44px; padding: .42rem .55rem; font-size: 14px; }
    .remaining-alert { color: var(--warning); font-weight: 900; }
    .confirmation-layout { display: grid; grid-template-columns: minmax(0, 1.6fr) minmax(16rem, .8fr); gap: 1rem; }
    .confirmation-meta { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .8rem; }
    .change-fields { display: grid; grid-template-columns: repeat(2, minmax(8rem, 1fr)); gap: .35rem; min-width: 20rem; }
    .timeline, .audit-list { display: grid; gap: .7rem; margin: 0; padding: 0; list-style: none; }
    .timeline li, .audit-list article { border-inline-start: 3px solid var(--accent); padding: .65rem .8rem; background: var(--surface); }
    .timeline strong, .timeline small, .timeline p, .audit-list strong, .audit-list small, .audit-list p { display: block; margin: 0; }
    .timeline small, .audit-list small { margin-top: .25rem; color: var(--ink-muted); font-size: 14px; }
    .timeline p, .audit-list p { margin-top: .42rem; color: var(--ink-muted); font-size: 14px; line-height: 1.5; }
    .audit-list code { display: block; margin-top: .45rem; color: var(--ink-muted); font-size: .64rem; overflow-wrap: anywhere; }
    .dialog-backdrop { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 1rem; background: rgb(15 30 28 / .55); }
    .action-dialog { display: grid; gap: .8rem; width: min(100%, 32rem); border: 1px solid var(--line); border-radius: var(--radius-md); padding: 1.2rem; background: var(--surface-raised); box-shadow: 0 1rem 3rem rgb(0 0 0 / .22); }
    .action-dialog h2, .action-dialog p { margin: 0; }
    .action-dialog > p:not(.section-kicker) { color: var(--ink-muted); font-size: 14px; line-height: 1.55; }
    .dialog-actions { justify-content: flex-end; }
    code { font-family: var(--font-mono); font-size: .66rem; }
    @media (max-width: 900px) { .confirmation-layout { grid-template-columns: 1fr; } }
    @media (max-width: 650px) { .confirmation-meta { grid-template-columns: 1fr; } .change-fields { grid-template-columns: 1fr; min-width: 13rem; } .action-rail .button { flex: 1 1 auto; } }
  `,
})
export class PurchaseOrderWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly currencyPresentation = inject(CurrencyPresentationService);
  private readonly service = inject(PurchaseOrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly mode = signal<WorkspaceMode>('list');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<SafeUiError | null>(null);
  readonly records = signal<PurchaseOrderListItemResponse[]>([]);
  readonly sources = signal<PurchaseOrderSourceOptionResponse[]>([]);
  readonly order = signal<PurchaseOrderResponse | null>(null);
  readonly confirmations = signal<import('./purchase-order.model').PurchaseOrderConfirmationResponse[]>([]);
  readonly history = signal<PurchaseOrderHistoryResponse[]>([]);
  readonly audit = signal<import('./purchase-order.model').PurchaseOrderAuditResponse[]>([]);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly selectedSourceId = signal('');
  readonly activeTab = signal<DetailTab>('summary');
  readonly dialogAction = signal<ActionName | null>(null);
  readonly filteredRecords = computed(() => {
    const query = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    return this.records().filter((record) => (!status || record.status === status) && (!query || `${record.supplierName} ${record.supplierQuotationReference} ${record.supplierCode}`.toLowerCase().includes(query)));
  });
  readonly selectedSource = computed(() => this.sources().find((source) => source.source.sourceDecisionId === this.selectedSourceId()) ?? null);

  readonly statuses: PurchaseOrderStatus[] = ['Draft', 'PendingApproval', 'Approved', 'Issued', 'Confirmed', 'PartiallyConfirmed', 'ChangedPendingApproval', 'Rejected', 'NoResponse', 'ReturnedForChange', 'Cancelled'];
  readonly tabs: DetailTab[] = ['summary', 'lines', 'confirmations', 'history', 'audit'];
  private lastFocusedElement: HTMLElement | null = null;
  private readonly copy: Record<'en' | 'ar', Record<string, string>> = {
    en: {
      purchaseOrderKicker: 'Procurement / Purchase-to-Pay', purchaseOrders: 'Purchase Orders', purchaseOrdersLead: 'Create an immutable order from an approved source decision, then record the supplier response without creating stock or accounting effects.', newPurchaseOrder: 'New Purchase Order', purchaseOrderBoundary: 'This slice covers Purchase Order source lineage, approval, issue, manual supplier confirmation, partial confirmation, rejection, no-response, and supplier-change reapproval. Goods Receipt, stock, invoice, AP, payment, and accounting remain downstream.', loadingPurchaseOrders: 'Loading Purchase Orders…', purchaseOrderListLoadFailed: 'The Purchase Order list could not be loaded safely.', purchaseOrderSearch: 'Search supplier or quotation reference', purchaseOrderStatusFilter: 'Status filter', purchaseOrderAllStatuses: 'All statuses', purchaseOrderFilterNote: 'Results are Tenant- and server-authorized Company/Branch scoped.', noPurchaseOrders: 'No Purchase Orders yet', noPurchaseOrdersLead: 'Create the first order from an eligible approved supplier source decision.', purchaseOrderReferenceColumn: 'Quotation reference', purchaseOrderSupplierColumn: 'Supplier', purchaseOrderStatusColumn: 'Status', purchaseOrderCurrencyColumn: 'Currency', purchaseOrderTotalColumn: 'Total', purchaseOrderUpdatedColumn: 'Updated', createPurchaseOrder: 'Create Purchase Order', purchaseOrderCreateLead: 'Choose a current server-authorized source decision. The order stores source and commercial facts as snapshots.', backToPurchaseOrders: 'Back to Purchase Orders', purchaseOrderSourceRule: 'Only an Approved Purchase Request, an eligible Submitted Supplier Quotation, and the current Source Decision can create an order. The server revalidates every link and line.', loadingPurchaseOrderSources: 'Loading eligible sources…', purchaseOrderSourceLoadFailed: 'Eligible Purchase Order sources could not be loaded safely.', purchaseOrderSourceField: 'Approved source decision', selectPurchaseOrderSource: 'Select an approved supplier source', purchaseOrderSourceSnapshot: 'Source snapshot', purchaseOrderScope: 'Operational scope', purchaseOrderSourceLines: 'Source lines', purchaseOrderProductColumn: 'Product', purchaseOrderQuantityColumn: 'Quantity', purchaseOrderUnitPriceColumn: 'Unit price', purchaseOrderDeliveryColumn: 'Delivery', notAvailable: 'Not available', saving: 'Saving…', purchaseOrderSelectSourceLead: 'Select an eligible source to review its immutable line facts before creating the order.', editPurchaseOrder: 'Edit Purchase Order', purchaseOrderNotes: 'Notes', savePurchaseOrder: 'Save Purchase Order', purchaseOrderActions: 'Purchase Order actions', submitPurchaseOrder: 'Submit for approval', approvePurchaseOrder: 'Approve', issuePurchaseOrder: 'Issue to supplier', recordSupplierConfirmation: 'Record supplier response', approveSupplierChange: 'Approve supplier change', rejectSupplierChange: 'Reject supplier change', rejectPurchaseOrder: 'Reject Purchase Order', returnPurchaseOrderForChange: 'Return for change', cancelPurchaseOrder: 'Cancel Purchase Order', purchaseOrderSections: 'Purchase Order sections', loadingPurchaseOrder: 'Loading Purchase Order…', purchaseOrderPurchaseRequest: 'Purchase Request', purchaseOrderPaymentTerm: 'Payment term snapshot', purchaseOrderCreatedAt: 'Created', purchaseOrderVersion: 'Concurrency version', purchaseOrderImmutableLineage: 'Immutable source lineage', purchaseOrderLineageLead: 'The order is linked to the approved demand, selected supplier quotation, and source decision. Later supplier responses do not silently rewrite these historical facts.', purchaseOrderApproval: 'Approval', purchaseOrderNoApproval: 'Approval not started', purchaseOrderPolicy: 'Policy', purchaseOrderApprovals: 'Approvals', purchaseOrderReapproval: 'Reapproval', yes: 'Yes', no: 'No', purchaseOrderNoNotes: 'No notes recorded.', purchaseOrderLineSnapshots: 'Order line snapshots', purchaseOrderLines: 'lines', purchaseOrderOrderedQuantity: 'Ordered', purchaseOrderConfirmedQuantity: 'Confirmed', purchaseOrderRemainingQuantity: 'Remaining', purchaseOrderSupplierChange: 'Supplier change proposal', purchaseOrderProposedQuantity: 'Proposed quantity', purchaseOrderProposedPrice: 'Proposed price', purchaseOrderChangeReason: 'Change reason', supplierReference: 'Supplier reference', supplierContact: 'Supplier contact', purchaseOrderReason: 'Reason', purchaseOrderEvidenceReference: 'Evidence reference', supplierConfirmationEvidenceDescription: 'Buyer-recorded supplier response evidence', supplierConfirmationTitle: 'Manual supplier confirmation', supplierConfirmationLead: 'Record the supplier response per line. A partial response keeps the remainder visible; proposed commercial changes stay pending until authorized reapproval.', supplierConfirmationStatus: 'Response status', supplierConfirmationHistory: 'Supplier response history', noSupplierConfirmations: 'No supplier responses recorded.', purchaseOrderChanges: 'changes', purchaseOrderHistory: 'Lifecycle history', noPurchaseOrderHistory: 'No lifecycle history is available.', purchaseOrderAudit: 'Audit evidence', noPurchaseOrderAudit: 'No audit evidence is available.', purchaseOrderConflict: 'This Purchase Order changed elsewhere. Reload the latest version before retrying.', purchaseOrderNotFound: 'This Purchase Order could not be found in the current Tenant context.', purchaseOrderTabSummary: 'Summary', purchaseOrderTabLines: 'Lines', purchaseOrderTabConfirmations: 'Supplier response', purchaseOrderTabHistory: 'History', purchaseOrderTabAudit: 'Audit', purchaseOrderStatusDraft: 'Draft', purchaseOrderStatusPendingApproval: 'Pending approval', purchaseOrderStatusApproved: 'Approved', purchaseOrderStatusIssued: 'Issued', purchaseOrderStatusConfirmed: 'Confirmed', purchaseOrderStatusPartiallyConfirmed: 'Partially confirmed', purchaseOrderStatusChangedPendingApproval: 'Changed — pending approval', purchaseOrderStatusRejected: 'Rejected', purchaseOrderStatusNoResponse: 'No response', purchaseOrderStatusReturnedForChange: 'Returned for change', purchaseOrderStatusCancelled: 'Cancelled', purchaseOrderStatusUnknown: 'Unknown status', purchaseOrderConfirmationConfirmed: 'Confirmed', purchaseOrderConfirmationPartial: 'Partially confirmed', purchaseOrderConfirmationRejected: 'Rejected by supplier', purchaseOrderConfirmationNoResponse: 'No response', purchaseOrderActionSubmitted: 'Submitted', purchaseOrderActionApprovalRecorded: 'Approval recorded', purchaseOrderActionIssued: 'Issued', purchaseOrderActionConfirmationRecorded: 'Supplier response recorded', purchaseOrderActionPartial: 'Partial confirmation', purchaseOrderActionSupplierChangeProposed: 'Supplier change proposed', purchaseOrderActionSupplierChangeApproved: 'Supplier change approved', purchaseOrderActionSupplierChangeRejected: 'Supplier change rejected', purchaseOrderActionLifecycle: 'Lifecycle update', purchaseOrderRejectLead: 'Rejection is explicit and remains in lifecycle and audit history.', purchaseOrderReturnLead: 'The order returns for controlled change; no silent commercial mutation is applied.', purchaseOrderCancelLead: 'Cancellation ends the current order path and remains visible in audit history.', purchaseOrderSupplierChangeRejectLead: 'Rejecting the proposal preserves the original ordered commercial values.', purchaseOrderReasonHint: 'Explain the decision. This reason is retained as immutable evidence.'
    },
    ar: {
      purchaseOrderKicker: 'المشتريات / دورة الشراء', purchaseOrders: 'أوامر الشراء', purchaseOrdersLead: 'أنشئ أمرًا ثابتًا من قرار مصدر معتمد، ثم سجل رد المورد دون إنشاء أثر مخزني أو محاسبي.', newPurchaseOrder: 'أمر شراء جديد', purchaseOrderBoundary: 'يغطي هذا النطاق مسار المصدر والموافقة والإصدار وتسجيل رد المورد والتأكيد الجزئي والرفض وعدم الرد وإعادة اعتماد تغييرات المورد. الاستلام والمخزون والفواتير والحسابات مسارات لاحقة.', loadingPurchaseOrders: 'جارٍ تحميل أوامر الشراء…', purchaseOrderListLoadFailed: 'تعذر تحميل قائمة أوامر الشراء بأمان.', purchaseOrderSearch: 'ابحث عن المورد أو مرجع العرض', purchaseOrderStatusFilter: 'مرشح الحالة', purchaseOrderAllStatuses: 'كل الحالات', purchaseOrderFilterNote: 'النتائج مقيدة بالعميل وبنطاق الشركة/الفرع المعتمد من الخادم.', noPurchaseOrders: 'لا توجد أوامر شراء بعد', noPurchaseOrdersLead: 'أنشئ أول أمر من قرار مصدر مورد معتمد.', purchaseOrderReferenceColumn: 'مرجع العرض', purchaseOrderSupplierColumn: 'المورد', purchaseOrderStatusColumn: 'الحالة', purchaseOrderCurrencyColumn: 'العملة', purchaseOrderTotalColumn: 'الإجمالي', purchaseOrderUpdatedColumn: 'آخر تحديث', createPurchaseOrder: 'إنشاء أمر شراء', purchaseOrderCreateLead: 'اختر قرار مصدر معتمدًا من الخادم. يحفظ الأمر قيم المصدر والقيم التجارية كلقطات.', backToPurchaseOrders: 'العودة إلى أوامر الشراء', purchaseOrderSourceRule: 'لا يمكن إنشاء الأمر إلا من طلب شراء معتمد وعرض مورد مؤهل وقرار مصدر حالي. يعيد الخادم التحقق من كل الروابط والبنود.', loadingPurchaseOrderSources: 'جارٍ تحميل المصادر المؤهلة…', purchaseOrderSourceLoadFailed: 'تعذر تحميل مصادر أوامر الشراء بأمان.', purchaseOrderSourceField: 'قرار المصدر المعتمد', selectPurchaseOrderSource: 'اختر مصدر مورد معتمدًا', purchaseOrderSourceSnapshot: 'لقطة المصدر', purchaseOrderScope: 'النطاق التشغيلي', purchaseOrderSourceLines: 'بنود المصدر', purchaseOrderProductColumn: 'المنتج', purchaseOrderQuantityColumn: 'الكمية', purchaseOrderUnitPriceColumn: 'سعر الوحدة', purchaseOrderDeliveryColumn: 'التسليم', notAvailable: 'غير متاح', saving: 'جارٍ الحفظ…', purchaseOrderSelectSourceLead: 'اختر مصدرًا مؤهلًا لمراجعة قيم البنود الثابتة قبل إنشاء الأمر.', editPurchaseOrder: 'تعديل أمر الشراء', purchaseOrderNotes: 'ملاحظات', savePurchaseOrder: 'حفظ أمر الشراء', purchaseOrderActions: 'إجراءات أمر الشراء', submitPurchaseOrder: 'إرسال للموافقة', approvePurchaseOrder: 'موافقة', issuePurchaseOrder: 'إصدار للمورد', recordSupplierConfirmation: 'تسجيل رد المورد', approveSupplierChange: 'موافقة تغيير المورد', rejectSupplierChange: 'رفض تغيير المورد', rejectPurchaseOrder: 'رفض أمر الشراء', returnPurchaseOrderForChange: 'إعادة للتعديل', cancelPurchaseOrder: 'إلغاء أمر الشراء', purchaseOrderSections: 'أقسام أمر الشراء', loadingPurchaseOrder: 'جارٍ تحميل أمر الشراء…', purchaseOrderPurchaseRequest: 'طلب الشراء', purchaseOrderPaymentTerm: 'لقطة شرط الدفع', purchaseOrderCreatedAt: 'أنشئ في', purchaseOrderVersion: 'نسخة التزامن', purchaseOrderImmutableLineage: 'مسار المصدر الثابت', purchaseOrderLineageLead: 'يرتبط الأمر بالطلب المعتمد وعرض المورد وقرار المصدر. لا تعيد ردود المورد اللاحقة كتابة هذه الحقائق.', purchaseOrderApproval: 'الموافقة', purchaseOrderNoApproval: 'لم تبدأ الموافقة', purchaseOrderPolicy: 'السياسة', purchaseOrderApprovals: 'الموافقات', purchaseOrderReapproval: 'إعادة الموافقة', yes: 'نعم', no: 'لا', purchaseOrderNoNotes: 'لا توجد ملاحظات.', purchaseOrderLineSnapshots: 'لقطات بنود الأمر', purchaseOrderLines: 'بنود', purchaseOrderOrderedQuantity: 'المطلوب', purchaseOrderConfirmedQuantity: 'المؤكد', purchaseOrderRemainingQuantity: 'المتبقي', purchaseOrderSupplierChange: 'مقترح تغيير المورد', purchaseOrderProposedQuantity: 'الكمية المقترحة', purchaseOrderProposedPrice: 'السعر المقترح', purchaseOrderChangeReason: 'سبب التغيير', supplierReference: 'مرجع المورد', supplierContact: 'جهة اتصال المورد', purchaseOrderReason: 'السبب', purchaseOrderEvidenceReference: 'مرجع الدليل', supplierConfirmationEvidenceDescription: 'دليل رد المورد المسجل من المشتري', supplierConfirmationTitle: 'تأكيد المورد اليدوي', supplierConfirmationLead: 'سجل رد المورد لكل بند. يبقى المتبقي ظاهرًا في الرد الجزئي، وتظل التغييرات التجارية معلقة حتى إعادة الموافقة.', supplierConfirmationStatus: 'حالة الرد', supplierConfirmationHistory: 'سجل ردود المورد', noSupplierConfirmations: 'لم تسجل ردود للمورد.', purchaseOrderChanges: 'تغييرات', purchaseOrderHistory: 'السجل الحيوي', noPurchaseOrderHistory: 'لا يوجد سجل حيوي متاح.', purchaseOrderAudit: 'دليل التدقيق', noPurchaseOrderAudit: 'لا يوجد دليل تدقيق متاح.', purchaseOrderConflict: 'تغير أمر الشراء في مكان آخر. أعد تحميل أحدث نسخة قبل المحاولة.', purchaseOrderNotFound: 'تعذر العثور على أمر الشراء في نطاق العميل الحالي.', purchaseOrderTabSummary: 'الملخص', purchaseOrderTabLines: 'البنود', purchaseOrderTabConfirmations: 'رد المورد', purchaseOrderTabHistory: 'السجل', purchaseOrderTabAudit: 'التدقيق', purchaseOrderStatusDraft: 'مسودة', purchaseOrderStatusPendingApproval: 'بانتظار الموافقة', purchaseOrderStatusApproved: 'معتمد', purchaseOrderStatusIssued: 'صادر', purchaseOrderStatusConfirmed: 'مؤكد', purchaseOrderStatusPartiallyConfirmed: 'مؤكد جزئيًا', purchaseOrderStatusChangedPendingApproval: 'تغيير بانتظار الموافقة', purchaseOrderStatusRejected: 'مرفوض', purchaseOrderStatusNoResponse: 'بدون رد', purchaseOrderStatusReturnedForChange: 'معاد للتعديل', purchaseOrderStatusCancelled: 'ملغى', purchaseOrderStatusUnknown: 'حالة غير معروفة', purchaseOrderConfirmationConfirmed: 'مؤكد', purchaseOrderConfirmationPartial: 'مؤكد جزئيًا', purchaseOrderConfirmationRejected: 'مرفوض من المورد', purchaseOrderConfirmationNoResponse: 'بدون رد', purchaseOrderActionSubmitted: 'تم الإرسال', purchaseOrderActionApprovalRecorded: 'تم تسجيل الموافقة', purchaseOrderActionIssued: 'تم الإصدار', purchaseOrderActionConfirmationRecorded: 'تم تسجيل رد المورد', purchaseOrderActionPartial: 'تأكيد جزئي', purchaseOrderActionSupplierChangeProposed: 'اقتراح تغيير المورد', purchaseOrderActionSupplierChangeApproved: 'تمت الموافقة على تغيير المورد', purchaseOrderActionSupplierChangeRejected: 'تم رفض تغيير المورد', purchaseOrderActionLifecycle: 'تحديث الدورة', purchaseOrderRejectLead: 'الرفض صريح ويبقى في السجل ودليل التدقيق.', purchaseOrderReturnLead: 'يعود الأمر لتغيير مضبوط دون تعديل تجاري صامت.', purchaseOrderCancelLead: 'ينهي الإلغاء مسار الأمر ويبقى ظاهرًا في دليل التدقيق.', purchaseOrderSupplierChangeRejectLead: 'يحافظ رفض المقترح على القيم التجارية الأصلية.', purchaseOrderReasonHint: 'اشرح القرار. يحتفظ النظام بالسبب كدليل ثابت.'
    }
  };
  editNotes = '';
  editLines: EditLine[] = [];
  confirmationStatus: PurchaseOrderConfirmationStatus = 'Confirmed';
  confirmationLines: ConfirmationLineDraft[] = [];
  supplierReference = '';
  supplierContact = '';
  confirmationReason = '';
  confirmationNotes = '';
  evidenceReference = '';
  actionReason = '';

  poText(key: string): string { return this.copy[this.language.language()][key] ?? key; }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = params.get('id');
      if (this.route.snapshot.url.some((segment) => segment.path === 'new')) {
        this.mode.set('create');
        void this.loadSources();
      } else if (id) {
        this.mode.set(this.route.snapshot.url.some((segment) => segment.path === 'edit') ? 'edit' : 'detail');
        void this.loadDetail(id);
      } else {
        this.mode.set('list');
        void this.loadList();
      }
    });
  }

  async loadList(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try { this.records.set(await firstValueFrom(this.service.list())); } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.loading.set(false); }
  }

  async loadSources(): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try { this.sources.set(await firstValueFrom(this.service.sourceOptions())); } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.loading.set(false); }
  }

  async loadDetail(id: string): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const currentOrder = await firstValueFrom(this.service.get(id));
      this.order.set(currentOrder);
      this.editNotes = currentOrder.notes ?? '';
      this.editLines = currentOrder.lines.map((line) => this.toEditLine(line));
      this.confirmationLines = currentOrder.lines.map((line) => this.toConfirmationLine(line));
      const [confirmations, history, audit] = await Promise.all([
        firstValueFrom(this.service.confirmations(id)),
        firstValueFrom(this.service.history(id)),
        firstValueFrom(this.service.audit(id)),
      ]);
      this.confirmations.set(confirmations); this.history.set(history); this.audit.set(audit);
    } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.loading.set(false); }
  }

  retryDetail(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) void this.loadDetail(id);
  }

  setStatusFilter(value: string): void { this.statusFilter.set(value); }

  formatLineCount(count: number): string {
    if (this.language.language() === 'en') return `${count} ${count === 1 ? 'line' : 'lines'}`;
    const form = new Intl.PluralRules('ar').select(count);
    const item = form === 'two' ? 'بندان' : form === 'few' || form === 'zero' ? 'بنود' : form === 'many' ? 'بندًا' : 'بند';
    return `${new Intl.NumberFormat('ar').format(count)} ${item}`;
  }

  gridColumns(): DataGridColumn<PurchaseOrderListItemResponse>[] {
    return [
      { key: 'supplierQuotationReference', label: this.poText('purchaseOrderReferenceColumn'), value: (record) => record.supplierQuotationReference, link: (record) => `/app/procurement/purchase-orders/${record.id}`, secondaryText: (record) => this.formatLineCount(record.lineCount), filter: 'text', width: 208 },
      { key: 'supplierName', label: this.poText('purchaseOrderSupplierColumn'), value: (record) => record.supplierName, display: (record) => `${record.supplierName} · ${record.supplierCode}`, filter: 'text', width: 208 },
      { key: 'status', label: this.poText('purchaseOrderStatusColumn'), value: (record) => record.status, display: (record) => this.statusLabel(record.status), filter: 'select', filterOptions: this.statuses.map((status) => ({ value: status, label: this.statusLabel(status) })), badge: true, width: 166 },
      { key: 'currencyCode', label: this.poText('purchaseOrderCurrencyColumn'), value: (record) => record.currencyCode, filter: 'select', width: 112 },
      { key: 'total', label: this.poText('purchaseOrderTotalColumn'), value: (record) => record.total, display: (record) => this.formatAmount(record.total), currencySymbol: (record) => ({ url: this.currencyPresentation.symbolAssetUrl(record.currencyCode), text: this.currencyPresentation.symbolText(record.currencyCode) }), filter: 'number-range', align: 'end', width: 168 },
      { key: 'updatedAt', label: this.poText('purchaseOrderUpdatedColumn'), value: (record) => record.updatedAt.slice(0, 10), display: (record) => this.formatDateTime(record.updatedAt), filter: 'date-range', width: 190 },
    ];
  }

  gridText(english: string, arabic: string): string { return this.language.language() === 'ar' ? arabic : english; }

  onGridAction(event: DataGridAction<PurchaseOrderListItemResponse>): void {
    if (event.action === 'view') void this.router.navigate(['/app/procurement/purchase-orders', event.row.id]);
  }

  async create(): Promise<void> {
    const source = this.selectedSource();
    if (!source) { this.error.set({ code: 'validation_failed', status: 400, correlationId: null }); return; }
    this.saving.set(true); this.error.set(null);
    try { const created = await this.service.create({ sourceDecisionId: source.source.sourceDecisionId }); await this.router.navigate(['/app/procurement/purchase-orders', created.id]); }
    catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  async saveEdit(): Promise<void> {
    const currentOrder = this.order();
    if (!currentOrder) return;
    if (this.editLines.some((line) => line.orderedQuantity <= 0 || line.unitPrice < 0)) { this.error.set({ code: 'validation_failed', status: 400, correlationId: null }); return; }
    this.saving.set(true); this.error.set(null);
    try {
      await this.service.edit(currentOrder.id, { notes: this.editNotes.trim() || null, lines: this.editLines.map((line) => ({ purchaseOrderLineId: line.id, orderedQuantity: line.orderedQuantity, unitPrice: line.unitPrice, deliveryDate: line.deliveryDate || null, notes: line.notes.trim() || null })) }, currentOrder.version);
      await this.router.navigate(['/app/procurement/purchase-orders', currentOrder.id]);
    } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  async runSimpleAction(action: 'submit' | 'approve' | 'issue' | 'supplier-change-approve'): Promise<void> {
    const currentOrder = this.order(); if (!currentOrder) return;
    this.saving.set(true); this.error.set(null);
    try {
      if (action === 'submit') await this.service.submit(currentOrder.id, currentOrder.version);
      if (action === 'approve') await this.service.approve(currentOrder.id, currentOrder.version);
      if (action === 'issue') await this.service.issue(currentOrder.id, currentOrder.version);
      if (action === 'supplier-change-approve') await this.service.approveSupplierChange(currentOrder.id, currentOrder.version);
      await this.loadDetail(currentOrder.id);
    } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  openAction(action: ActionName): void {
    this.lastFocusedElement = document.activeElement as HTMLElement | null;
    this.actionReason = '';
    this.dialogAction.set(action);
    setTimeout(() => document.getElementById('purchase-order-action-cancel')?.focus(), 0);
  }

  closeAction(): void {
    if (this.saving()) return;
    this.dialogAction.set(null);
    this.restoreFocusToOpener();
  }

  onActionDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.trapTabKey(event, event.currentTarget as HTMLElement);
      return;
    }

    if (event.key === 'Escape') {
      if (this.saving()) return;
      event.preventDefault();
      this.closeAction();
    }
  }

  setTab(tab: DetailTab): void {
    this.activeTab.set(tab);
  }

  onTabsKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key !== 'ArrowLeft' && key !== 'ArrowRight' && key !== 'Home' && key !== 'End') return;
    event.preventDefault();
    const currentIndex = this.tabs.indexOf(this.activeTab());
    const isRtl = this.language.language() === 'ar';
    let nextIndex: number;
    if (key === 'Home') nextIndex = 0;
    else if (key === 'End') nextIndex = this.tabs.length - 1;
    else {
      const forward = key === 'ArrowRight';
      const movesNext = isRtl ? !forward : forward;
      nextIndex = movesNext ? (currentIndex + 1) % this.tabs.length : (currentIndex - 1 + this.tabs.length) % this.tabs.length;
    }

    const nextTab = this.tabs[nextIndex];
    this.setTab(nextTab);
    setTimeout(() => document.getElementById(this.tabId(nextTab))?.focus(), 0);
  }

  tabId(tab: DetailTab): string { return `purchase-order-tab-${tab}`; }
  panelId(tab: DetailTab): string { return `purchase-order-tabpanel-${tab}`; }

  async confirmAction(): Promise<void> {
    const action = this.dialogAction(); const currentOrder = this.order();
    if (!action || !currentOrder || !this.actionReason.trim()) return;
    this.saving.set(true); this.error.set(null);
    try {
      if (action === 'reject') await this.service.reject(currentOrder.id, currentOrder.version, this.actionReason.trim());
      if (action === 'return-for-change') await this.service.returnForChange(currentOrder.id, currentOrder.version, this.actionReason.trim());
      if (action === 'cancel') await this.service.cancel(currentOrder.id, currentOrder.version, this.actionReason.trim());
      if (action === 'supplier-change-reject') await this.service.rejectSupplierChange(currentOrder.id, currentOrder.version, this.actionReason.trim());
      this.dialogAction.set(null); this.restoreFocusToOpener(); await this.loadDetail(currentOrder.id);
    } catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  async captureConfirmation(): Promise<void> {
    const currentOrder = this.order(); if (!currentOrder) return;
    const hasChanges = this.confirmationLines.some((line) => line.proposedQuantity !== null || line.proposedUnitPrice !== null || !!line.proposedDeliveryDate);
    if (this.confirmationLines.some((line) => line.confirmedQuantity < 0 || line.confirmedQuantity > line.orderedQuantity || (hasChanges && !line.changeReason.trim() && (line.proposedQuantity !== null || line.proposedUnitPrice !== null || !!line.proposedDeliveryDate)))) { this.error.set({ code: 'validation_failed', status: 400, correlationId: null }); return; }
    if (this.confirmationStatus === 'Confirmed' && this.confirmationLines.some((line) => line.confirmedQuantity !== line.orderedQuantity)) { this.error.set({ code: 'validation_failed', status: 400, correlationId: null }); return; }
    if (this.confirmationStatus === 'PartiallyConfirmed' && (this.confirmationLines.every((line) => line.confirmedQuantity === 0) || this.confirmationLines.every((line) => line.confirmedQuantity >= line.orderedQuantity))) { this.error.set({ code: 'validation_failed', status: 400, correlationId: null }); return; }
    this.saving.set(true); this.error.set(null);
    const payload: PurchaseOrderConfirmationRequest = {
      status: this.confirmationStatus, responseDate: new Date().toISOString().slice(0, 10), supplierReference: this.supplierReference.trim() || null, supplierContact: this.supplierContact.trim() || null, reason: this.confirmationReason.trim() || null, notes: this.confirmationNotes.trim() || null,
      lines: this.confirmationLines.map((line) => ({ purchaseOrderLineId: line.id, confirmedQuantity: line.confirmedQuantity, expectedDeliveryDate: line.expectedDeliveryDate || null, proposedQuantity: line.proposedQuantity, proposedUnitPrice: line.proposedUnitPrice, proposedDeliveryDate: line.proposedDeliveryDate || null, changeReason: line.changeReason.trim() || null })),
      evidence: this.evidenceReference.trim() ? [{ referenceId: this.evidenceReference.trim(), fileName: null, contentType: null, description: this.poText('supplierConfirmationEvidenceDescription'), source: 'buyer-recorded', externalReference: null }] : [],
    };
    try { await this.service.captureConfirmation(currentOrder.id, payload, currentOrder.version); await this.loadDetail(currentOrder.id); }
    catch (error: unknown) { this.error.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  statusLabel(status: string): string {
    const key: Record<string, string> = { Draft: 'purchaseOrderStatusDraft', PendingApproval: 'purchaseOrderStatusPendingApproval', Approved: 'purchaseOrderStatusApproved', Issued: 'purchaseOrderStatusIssued', Confirmed: 'purchaseOrderStatusConfirmed', PartiallyConfirmed: 'purchaseOrderStatusPartiallyConfirmed', ChangedPendingApproval: 'purchaseOrderStatusChangedPendingApproval', Rejected: 'purchaseOrderStatusRejected', NoResponse: 'purchaseOrderStatusNoResponse', ReturnedForChange: 'purchaseOrderStatusReturnedForChange', Cancelled: 'purchaseOrderStatusCancelled' };
    return this.poText(key[status] ?? 'purchaseOrderStatusUnknown');
  }

  confirmationStatusLabel(status: string): string {
    const key: Record<string, string> = { Confirmed: 'purchaseOrderConfirmationConfirmed', PartiallyConfirmed: 'purchaseOrderConfirmationPartial', Rejected: 'purchaseOrderConfirmationRejected', NoResponse: 'purchaseOrderConfirmationNoResponse' };
    return this.poText(key[status] ?? 'purchaseOrderStatusUnknown');
  }

  statusClass(status: string): string {
    const classes: Record<string, string> = { Draft: 'draft', PendingApproval: 'pending', Approved: 'approved', Issued: 'issued', Confirmed: 'confirmed', PartiallyConfirmed: 'partial', ChangedPendingApproval: 'changed', Rejected: 'rejected', NoResponse: 'no-response', ReturnedForChange: 'returned', Cancelled: 'cancelled' };
    return `status-badge--${classes[status] ?? 'unknown'}`;
  }
  tabLabel(tab: DetailTab): string { return this.poText(({ summary: 'purchaseOrderTabSummary', lines: 'purchaseOrderTabLines', confirmations: 'purchaseOrderTabConfirmations', history: 'purchaseOrderTabHistory', audit: 'purchaseOrderTabAudit' }[tab])); }
  actionLabel(action: string): string { return this.poText(({ Submitted: 'purchaseOrderActionSubmitted', ApprovalRecorded: 'purchaseOrderActionApprovalRecorded', Issued: 'purchaseOrderActionIssued', ConfirmationRecorded: 'purchaseOrderActionConfirmationRecorded', PartiallyConfirmed: 'purchaseOrderActionPartial', SupplierChangeProposed: 'purchaseOrderActionSupplierChangeProposed', SupplierChangeApproved: 'purchaseOrderActionSupplierChangeApproved', SupplierChangeRejected: 'purchaseOrderActionSupplierChangeRejected' }[action] ?? 'purchaseOrderActionLifecycle')); }
  actionTitle(action: ActionName): string { return this.poText(({ reject: 'rejectPurchaseOrder', 'return-for-change': 'returnPurchaseOrderForChange', cancel: 'cancelPurchaseOrder', 'supplier-change-reject': 'rejectSupplierChange' }[action])); }
  actionLead(action: ActionName): string { return this.poText(({ reject: 'purchaseOrderRejectLead', 'return-for-change': 'purchaseOrderReturnLead', cancel: 'purchaseOrderCancelLead', 'supplier-change-reject': 'purchaseOrderSupplierChangeRejectLead' }[action])); }
  formatDate(value: string): string { return new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium' }).format(new Date(value)); }
  formatDateTime(value: string): string { return new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  formatQuantity(value: number): string { return new Intl.NumberFormat(this.language.language(), { maximumFractionDigits: 6 }).format(value); }
  private formatAmount(value: number): string { return new Intl.NumberFormat(this.language.language(), { maximumFractionDigits: 2, minimumFractionDigits: 2 }).format(value); }
  formatMoney(value: number, currency: string): string {
    const language = this.language.language();
    const safeCurrency = (currency || '').trim();
    if (safeCurrency) {
      try {
        return new Intl.NumberFormat(language, { style: 'currency', currency: safeCurrency, currencyDisplay: 'code', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
      } catch {
        // Fall back safely if Intl rejects non-ISO or unrecognized currency code
      }
    }
    const formatted = new Intl.NumberFormat(language, { maximumFractionDigits: 2, minimumFractionDigits: 2 }).format(value);
    return safeCurrency ? `${formatted} ${safeCurrency}` : formatted;
  }
  errorText(error: SafeUiError): string { return error.status === 409 ? this.poText('purchaseOrderConflict') : error.status === 403 ? this.language.text('accessDenied') : error.status === 404 ? this.poText('purchaseOrderNotFound') : this.language.text('requestError'); }
  terminalRecoveryTitle(): string { return this.language.language() === 'ar' ? 'استهلاك قرار المصدر' : 'Source decision consumed'; }
  terminalRecoveryMessage(): string { return this.language.language() === 'ar' ? 'استهلك أمر الشراء هذا قرار مصدره. لمواصلة الشراء، أنشئ قرار توريد جديدًا أو اختر قرارًا جديدًا. إعادة الفتح المنضبطة غير متاحة في هذه الشريحة من الإصدار.' : 'This Purchase Order has consumed its source decision. To continue purchasing, create/select a new sourcing decision. Controlled reopening is not available in this release slice.'; }

  private trapTabKey(event: KeyboardEvent, panel: HTMLElement): void {
    const focusable = this.getFocusableElements(panel);
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (event.shiftKey) {
      if (active === first || !active || !panel.contains(active)) {
        event.preventDefault();
        last.focus();
      }
      return;
    }

    if (active === last || !active || !panel.contains(active)) {
      event.preventDefault();
      first.focus();
    }
  }

  private getFocusableElements(panel: HTMLElement): HTMLElement[] {
    const selector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(panel.querySelectorAll<HTMLElement>(selector));
  }

  private restoreFocusToOpener(): void {
    const element = this.lastFocusedElement;
    this.lastFocusedElement = null;
    if (element && document.contains(element)) {
      setTimeout(() => element.focus(), 0);
    }
  }

  private toEditLine(line: PurchaseOrderLineResponse): EditLine { return { id: line.id, productSku: line.productSku, productName: line.productName, unitOfMeasureCode: line.unitOfMeasureCode, orderedQuantity: line.orderedQuantity, unitPrice: line.unitPrice, deliveryDate: line.deliveryDate ?? '', notes: line.notes ?? '' }; }
  private toConfirmationLine(line: PurchaseOrderLineResponse): ConfirmationLineDraft { return { id: line.id, productSku: line.productSku, productName: line.productName, orderedQuantity: line.orderedQuantity, confirmedQuantity: line.remainingQuantity > 0 ? line.confirmedQuantity : line.orderedQuantity, expectedDeliveryDate: line.deliveryDate ?? '', proposedQuantity: null, proposedUnitPrice: null, proposedDeliveryDate: '', changeReason: '' }; }
}
