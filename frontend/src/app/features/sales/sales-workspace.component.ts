import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { DatePipe, DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, combineLatest } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService } from '../../core/i18n/language.service';
import { CustomerRecord, CurrencyRecord, ExchangeRateRecord, PaymentTermRecord, ProductRecord, TaxRecord, UnitOfMeasureRecord } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import { PriceListRecord } from '../master-data/price-list.model';
import { PriceListService } from '../master-data/price-list.service';
import { PurchaseRequestOrganizationScopeResponse } from '../procurement/purchase-request.model';
import { PurchaseRequestService } from '../procurement/purchase-request.service';
import { InventoryService } from '../inventory/inventory.service';
import { InventoryAvailability, InventoryReservation, InventoryWarehouseOption } from '../inventory/inventory.model';
import {
  SalesAuditResponse,
  SalesCreditOverrideRequest,
  SalesCreditResponse,
  SalesDeliveryResponse,
  SalesFulfillmentResponse,
  SalesInvoiceEligibilityResponse,
  SalesInvoiceRequestResponse,
  SalesHistoryResponse,
  SalesOrderResponse,
  SalesOrderEditRequest,
  SalesOrderStatus,
  SalesOrderSummaryResponse,
  SalesQuotationCreateRequest,
  SalesQuotationEditRequest,
  SalesQuotationLineResponse,
  SalesQuotationResponse,
  SalesQuotationRevisionResponse,
  SalesQuotationStatus,
  SalesQuotationSummaryResponse,
} from './sales.model';
import { SalesService } from './sales.service';

type WorkspaceDocument = 'quotation' | 'order';
type SalesListRecord = SalesQuotationSummaryResponse | SalesOrderSummaryResponse;
type DetailTab = 'summary' | 'lines' | 'revisions' | 'history' | 'audit' | 'credit' | 'fulfillment';
type WorkspaceMode = 'list' | 'create' | 'edit' | 'view';

interface LineDraft {
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  unitPriceOverride: number | null;
  discountPercent: number;
  notes: string;
  taxId: string;
}

interface QuotationDraft {
  companyId: string;
  branchId: string;
  customerId: string;
  quotationDate: string;
  validUntil: string;
  currencyId: string;
  priceListId: string;
  customerContactId: string;
  notes: string;
  customerReference: string;
  exchangeRateId: string;
  paymentTermId: string;
  lines: LineDraft[];
}

@Component({
  selector: 'app-sales-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, DatePipe, DecimalPipe, FormsModule, NgTemplateOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="sales-workspace" aria-labelledby="sales-title">
      <app-page-header class="page-header">
        <div class="page-header__copy" page-header-copy>
          <p class="eyebrow">{{ language.text('salesNavLabel') }} / {{ documentLabel() }}</p>
          <h1 id="sales-title">{{ language.text('salesWorkspace') }}</h1>
          <p class="hero-lede">{{ language.text('salesWorkspaceLead') }}</p>
        </div>
        
      </app-page-header>ndaryHint') }}</p>

      <nav class="sales-switcher" [attr.aria-label]="language.text('salesWorkspace')">
        <a routerLink="/app/sales/quotations" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: false }">{{ language.text('salesQuotationsNavLabel') }}</a>
        <a routerLink="/app/sales/orders" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: false }">{{ language.text('salesOrdersNavLabel') }}</a>
      </nav>

      @if (mode() === 'list') {
        <section class="paper-panel" aria-labelledby="sales-list-title">
          <div class="section-heading">
            <div><p class="eyebrow eyebrow--soft">{{ language.text('salesRegister') }}</p><h2 id="sales-list-title">{{ documentLabel() }}</h2><p>{{ language.text('salesRegisterLead') }}</p></div>
            <div class="section-actions"><button class="button button--quiet" type="button" (click)="loadList()" [disabled]="loading()">↻ {{ language.text('refresh') }}</button>@if (documentType() === 'quotation') { <a class="button button--primary" routerLink="/app/sales/quotations/new">＋ {{ language.text('newQuotation') }}</a> }</div>
          </div>
          <form class="sales-toolbar" role="search" (ngSubmit)="applyFilter()">
            <label class="sales-search"><span class="sr-only">{{ language.text('searchRecords') }}</span><input type="search" name="salesSearch" [ngModel]="search()" (ngModelChange)="search.set($event)" [placeholder]="language.text('salesSearchPlaceholder')" /><span aria-hidden="true">⌕</span></label>
            <label class="sales-status-filter"><span>{{ language.text('statusFilter') }}</span><select name="salesStatus" [ngModel]="statusFilter()" (ngModelChange)="statusFilter.set($event); loadList()"><option value="">{{ language.text('allStatuses') }}</option>@for (status of statusOptions(); track status) { <option [value]="status">{{ statusLabel(status) }}</option> }</select></label>
            <button class="button button--quiet" type="submit">{{ language.text('searchRecords') }}</button><span class="toolbar-count">{{ filteredRecords().length }} {{ language.text('recordCount') }}</span>
          </form>
          @if (loading()) { <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('salesLoading') }}</b></div> }
          @else if (listError()) { <div class="state-card state-card--error" role="alert"><b>{{ errorMessage(listError()) }}</b><p>{{ language.text('salesListLoadFailed') }}</p><button class="text-button" type="button" (click)="loadList()">{{ language.text('retry') }} ↻</button></div> }
          @else if (filteredRecords().length === 0) { <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ documentType() === 'quotation' ? language.text('noSalesQuotations') : language.text('noSalesOrders') }}</b><p>{{ language.text('salesEmptyLead') }}</p></div></div> }
          @else {
            <app-data-grid [caption]="documentLabel()" [rows]="filteredRecords()" [columns]="gridColumns()" [language]="language.language()" [pageSize]="filteredRecords().length || 1" [showPager]="false" [rowActions]="gridActions()" [countLabel]="language.text('recordCount')" [rowActionsLabel]="language.text('viewRecord')" (rowAction)="onGridAction($event)" />
          }
        </section>
      } @else if (mode() === 'create' || mode() === 'edit') {
        <section class="paper-panel" aria-labelledby="sales-form-title">
          <div class="detail-topline"><button class="back-link" type="button" (click)="backToList()">← {{ language.text('salesQuotationsNavLabel') }}</button></div>
          <div class="detail-heading"><div><p class="eyebrow eyebrow--soft">{{ mode() === 'create' ? language.text('newQuotation') : language.text('salesEditQuotation') }}</p><h2 id="sales-form-title">{{ mode() === 'create' ? language.text('newQuotation') : language.text('salesEditQuotation') }}</h2><p>{{ language.text('salesFormLead') }}</p></div><span class="scope-stamp">{{ language.text('serverAuthority') }}</span></div>
          @if (mutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b><span>{{ mutationError()?.code === 'concurrency_conflict' ? language.text('salesStaleLead') : language.text('salesValidationLead') }}</span></div> }
          @if (referenceError()) { <div class="inline-alert" role="alert"><b>{{ language.text('salesReferenceUnavailable') }}</b><span>{{ language.text('salesReferenceUnavailableLead') }}</span></div> }
          <form class="sales-form" (ngSubmit)="saveQuotation()" novalidate>
            <div class="form-grid form-grid--context">
              <label class="form-field form-field--scope"><span>{{ language.text('organizationScope') }}</span><select name="organizationScope" [ngModel]="draft.companyId ? draft.companyId + '|' + draft.branchId : ''" (ngModelChange)="setOrganizationScope($event)" required><option value="">{{ language.text('organizationScopeSelectHint') }}</option>@for (scope of organizationScopes(); track organizationScopeKey(scope)) {<option [value]="organizationScopeKey(scope)">{{ scope.displayName }}</option>}</select><small>{{ language.text('salesScopeHint') }}</small></label>
              <label class="form-field"><span>{{ language.text('customer') }}</span><select name="customerId" [(ngModel)]="draft.customerId" required><option value="">{{ language.text('salesSelectCustomer') }}</option>@for (customer of customers(); track customer.id) {<option [value]="customer.id">{{ displayCustomer(customer) }}</option>}</select></label>
              <label class="form-field"><span>{{ language.text('currency') }}</span><select name="currencyId" [(ngModel)]="draft.currencyId" required><option value="">{{ language.text('salesSelectCurrency') }}</option>@for (currency of currencies(); track currency.id) {<option [value]="currency.id">{{ currency.code }} · {{ displayCurrency(currency) }}</option>}</select></label>
               <label class="form-field"><span>{{ language.text('priceLists') }}</span><select name="priceListId" [(ngModel)]="draft.priceListId"><option value="">{{ language.text('salesAutomaticPriceSource') }}</option>@for (priceList of priceLists(); track priceList.id) {<option [value]="priceList.id">{{ priceList.code }} · {{ priceList.currencyCode }}</option>}</select></label>
              @if (documentType() === 'quotation') { <label class="form-field"><span>{{ language.text('paymentTerms') }}</span><select name="paymentTermId" [(ngModel)]="draft.paymentTermId" required><option value="">{{ language.text('salesSelectPaymentTerm') }}</option>@for (paymentTerm of paymentTerms(); track paymentTerm.id) { @if (paymentTerm.lifecycleState === 'Active') { <option [value]="paymentTerm.id">{{ paymentTerm.code }} · {{ paymentTerm.englishName || paymentTerm.arabicName || paymentTerm.code }} · v{{ paymentTerm.currentVersionNumber }}</option> } }</select></label> }
               <label class="form-field"><span>{{ language.text('exchangeRates') }}</span><select name="exchangeRateId" [(ngModel)]="draft.exchangeRateId"><option value="">{{ language.text('salesNoExchangeRate') }}</option>@for (rate of exchangeRates(); track rate.id) {<option [value]="rate.id">{{ rate.sourceCurrencyCode }} → {{ rate.targetCurrencyCode }} · v{{ rate.currentVersionNumber }}</option>}</select><small>{{ language.text('salesExchangeRateHint') }}</small></label>
               <label class="form-field"><span>{{ language.text('documentDate') }}</span><input name="quotationDate" type="date" [(ngModel)]="draft.quotationDate" required [disabled]="mode() === 'edit'" /></label>
              <label class="form-field"><span>{{ language.text('salesValidUntil') }}</span><input name="validUntil" type="date" [(ngModel)]="draft.validUntil" required /></label>
              <label class="form-field"><span>{{ language.text('salesCustomerReference') }}</span><input name="customerReference" [(ngModel)]="draft.customerReference" autocomplete="off" /></label>
            </div>
            <label class="form-field"><span>{{ language.text('salesCustomerContact') }}</span><input name="customerContactId" [(ngModel)]="draft.customerContactId" autocomplete="off" /><small>{{ language.text('salesExternalContactHint') }}</small></label>
            <label class="form-field"><span>{{ language.text('notes') }}</span><textarea name="notes" rows="3" [(ngModel)]="draft.notes"></textarea></label>
            <section class="line-editor" aria-labelledby="line-editor-title"><div class="subsection-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('salesLineEntry') }}</p><h3 id="line-editor-title">{{ language.text('salesLines') }}</h3></div><button class="button button--quiet" type="button" (click)="addLine()">＋ {{ language.text('addLine') }}</button></div><p class="field-note">{{ language.text('salesPricingServerNote') }}</p>
              @for (line of draft.lines; track $index; let index = $index) {<div class="line-row"><span class="line-index">{{ (index + 1).toString().padStart(2, '0') }}</span><label class="form-field"><span>{{ language.text('product') }}</span><select [name]="'product-' + index" [(ngModel)]="line.productId" required><option value="">{{ language.text('salesSelectProduct') }}</option>@for (product of products(); track product.id) {<option [value]="product.id">{{ product.sku }} · {{ displayProduct(product) }}</option>}</select></label><label class="form-field"><span>{{ language.text('unitOfMeasure') }}</span><select [name]="'uom-' + index" [(ngModel)]="line.unitOfMeasureId" required><option value="">{{ language.text('salesSelectUnit') }}</option>@for (unit of units(); track unit.id) {<option [value]="unit.id">{{ unit.code }} · {{ displayUnit(unit) }}</option>}</select></label><label class="form-field form-field--short"><span>{{ language.text('quantity') }}</span><input [name]="'quantity-' + index" type="number" min="0.000001" step="0.000001" [(ngModel)]="line.quantity" required /></label><label class="form-field form-field--short"><span>{{ language.text('salesDiscount') }}</span><input [name]="'discount-' + index" type="number" min="0" max="100" step="0.01" [(ngModel)]="line.discountPercent" /><small>{{ language.text('salesAuthorityHint') }}</small></label><button class="remove-line" type="button" (click)="removeLine(index)" [disabled]="draft.lines.length === 1" [attr.aria-label]="language.text('removeLine')">×</button></div>}
            </section>
             <p class="field-note">{{ language.text('salesTaxEvidenceHint') }}</p>
             <div class="tax-reference-grid" aria-label="Tax references">@for (line of draft.lines; track $index; let index = $index) {<label class="form-field"><span>{{ language.text('taxes') }} {{ index + 1 }}</span><select [name]="'tax-' + index" [(ngModel)]="line.taxId"><option value="">{{ language.text('salesNoTax') }}</option>@for (tax of taxes(); track tax.id) {<option [value]="tax.id">{{ tax.code }} · v{{ tax.currentVersionNumber }}</option>}</select></label>}</div>
             <div class="form-actions"><button class="button button--quiet" type="button" (click)="backToList()">{{ language.text('cancel') }}</button><button class="button button--primary" type="submit" [disabled]="saving() || referenceError()">{{ saving() ? language.text('actionInProgress') : language.text('saveDraft') }}</button></div>
          </form>
        </section>
      } @else {
        <section class="paper-panel" aria-labelledby="sales-detail-title">
          <div class="detail-topline"><button class="back-link" type="button" (click)="backToList()">← {{ documentLabel() }}</button></div>
          @if (detailLoading()) { <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('salesLoadingDetail') }}</b></div> }
          @else if (detailError()) { <div class="state-card state-card--error" role="alert"><b>{{ errorMessage(detailError()) }}</b><p>{{ language.text('salesDetailLoadFailed') }}</p><button class="text-button" type="button" (click)="loadDetail()">{{ language.text('retryLoad') }} ↻</button></div> }
          @else if (selectedQuotation(); as quote) { <ng-container *ngTemplateOutlet="quotationDetail; context: { record: quote }" /> }
          @else if (selectedOrder(); as order) { <ng-container *ngTemplateOutlet="orderDetail; context: { record: order }" /> }
        </section>
      }
    </section>

    <ng-template #quotationDetail let-record="record">
      <div class="detail-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('salesQuotation') }} · R{{ record.revisionNumber }}</p><h2 id="sales-detail-title">{{ record.number }}</h2><p>{{ record.customerName }} · {{ record.customerCode }}</p></div><div class="detail-actions"><span class="status-pill" [class]="'status-pill status-pill--' + statusTone(record.status)"><i aria-hidden="true"></i>{{ statusLabel(record.status) }}</span>@if (canEditQuotation(record)) {<button class="button button--quiet" type="button" (click)="editQuotation(record.id)">{{ language.text('editRecord') }}</button>}@for (action of quotationActions(record); track action.key) {<button class="button" [class.button--primary]="action.key === 'submit' || action.key === 'approve' || action.key === 'send' || action.key === 'convert'" [class.button--danger]="action.key === 'reject' || action.key === 'cancel'" type="button" (click)="runQuotationAction(action.key)">{{ action.label }}</button>}</div></div>
      @if (mutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b><span>{{ mutationError()?.code === 'concurrency_conflict' ? language.text('salesStaleLead') : language.text('salesActionFailed') }}</span><button class="text-button" type="button" (click)="loadDetail()">{{ language.text('reloadLatestVersion') }}</button></div> }
      <div class="commercial-strip"><div><span>{{ language.text('customer') }}</span><b>{{ record.customerName }}</b><small>{{ record.customerCode }}</small></div><div><span>{{ language.text('salesValidity') }}</span><b>{{ record.quotationDate | date:'mediumDate' }} → {{ record.validUntil | date:'mediumDate' }}</b></div><div><span>{{ language.text('currency') }}</span><b>{{ record.currencyCode }}</b></div><div class="commercial-strip__total"><span>{{ language.text('salesTotal') }}</span><b>{{ record.total | number:'1.2-2' }} {{ record.currencyCode }}</b></div></div>
      <nav class="tabs" role="tablist" [attr.aria-label]="language.text('salesQuotation')"><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'summary'" [class.is-active]="detailTab() === 'summary'" (click)="setTab('summary')">{{ language.text('salesSummary') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'lines'" [class.is-active]="detailTab() === 'lines'" (click)="setTab('lines')">{{ language.text('salesLines') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'revisions'" [class.is-active]="detailTab() === 'revisions'" (click)="setTab('revisions')">{{ language.text('salesRevisions') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'history'" [class.is-active]="detailTab() === 'history'" (click)="setTab('history')">{{ language.text('salesHistory') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'audit'" [class.is-active]="detailTab() === 'audit'" (click)="setTab('audit')">{{ language.text('audit') }}</button></nav>
      @switch (detailTab()) { @case ('summary') { <ng-container *ngTemplateOutlet="summary; context: { record: record }" /> } @case ('lines') { <ng-container *ngTemplateOutlet="lines; context: { record: record }" /> } @case ('revisions') { <ng-container *ngTemplateOutlet="revisionList" /> } @case ('history') { <ng-container *ngTemplateOutlet="historyList" /> } @case ('audit') { <ng-container *ngTemplateOutlet="auditList" /> } }
    </ng-template>

    <ng-template #orderDetail let-record="record">
      @if (canEditOrder(record)) { <button class="button button--quiet" type="button" (click)="editOrder(record.id)">{{ language.text('editRecord') }}</button> }
      <div class="detail-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('salesOrder') }}</p><h2 id="sales-detail-title">{{ record.number }}</h2><p>{{ record.customerName }} · {{ record.customerCode }}</p></div><div class="detail-actions"><span class="status-pill" [class]="'status-pill status-pill--' + statusTone(record.status)"><i aria-hidden="true"></i>{{ statusLabel(record.status) }}</span>@for (action of orderActions(record); track action.key) {<button class="button" [class.button--primary]="action.key === 'submit' || action.key === 'approve' || action.key === 'confirm'" [class.button--danger]="action.key === 'reject' || action.key === 'cancel'" type="button" (click)="runOrderAction(action.key)">{{ action.label }}</button>}</div></div>
      @if (mutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b><span>{{ mutationError()?.code === 'concurrency_conflict' ? language.text('salesStaleLead') : language.text('salesActionFailed') }}</span></div> }
      <div class="commercial-strip"><div><span>{{ language.text('customer') }}</span><b>{{ record.customerName }}</b><small>{{ record.customerCode }}</small></div><div><span>{{ language.text('salesSource') }}</span><b>{{ record.sourceQuotationNumber }}</b><small>R{{ record.sourceQuotationRevision }}</small></div><div><span>{{ language.text('salesCreditStatus') }}</span><b class="credit-text" [class.credit-text--hold]="record.creditOutcome === 'Blocked' || record.creditOutcome === 'Unknown'">{{ creditLabel(record.creditOutcome) }}</b><small>{{ record.creditReason || language.text('salesNoCreditReason') }}</small></div><div class="commercial-strip__total"><span>{{ language.text('salesTotal') }}</span><b>{{ record.total | number:'1.2-2' }} {{ record.currencyCode }}</b></div></div>
      <nav class="tabs" role="tablist" [attr.aria-label]="language.text('salesOrder')"><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'summary'" [class.is-active]="detailTab() === 'summary'" (click)="setTab('summary')">{{ language.text('salesSummary') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'lines'" [class.is-active]="detailTab() === 'lines'" (click)="setTab('lines')">{{ language.text('salesLines') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'fulfillment'" [class.is-active]="detailTab() === 'fulfillment'" (click)="setTab('fulfillment'); loadFulfillment()">{{ language.text('salesFulfillment') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'credit'" [class.is-active]="detailTab() === 'credit'" (click)="setTab('credit')">{{ language.text('salesCredit') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'history'" [class.is-active]="detailTab() === 'history'" (click)="setTab('history')">{{ language.text('salesHistory') }}</button><button role="tab" type="button" [attr.aria-selected]="detailTab() === 'audit'" [class.is-active]="detailTab() === 'audit'" (click)="setTab('audit')">{{ language.text('audit') }}</button></nav>
      @switch (detailTab()) { @case ('summary') { <ng-container *ngTemplateOutlet="summary; context: { record: record }" /> } @case ('lines') { <ng-container *ngTemplateOutlet="lines; context: { record: record }" /> } @case ('fulfillment') { <ng-container *ngTemplateOutlet="fulfillmentPanel; context: { record: record }" /> } @case ('credit') { <ng-container *ngTemplateOutlet="creditPanel; context: { record: record }" /> } @case ('history') { <ng-container *ngTemplateOutlet="historyList" /> } @case ('audit') { <ng-container *ngTemplateOutlet="auditList" /> } }
    </ng-template>

    <ng-template #summary let-record="record"><div class="summary-grid"><div><span>{{ language.text('companyId') }}</span><b>{{ record.companyId }}</b></div><div><span>{{ language.text('branchId') }}</span><b>{{ record.branchId || language.text('emptyValue') }}</b></div><div><span>{{ language.text('customer') }}</span><b>{{ record.customerName }}</b></div><div><span>{{ language.text('salesDocumentStatus') }}</span><b>{{ statusLabel(record.status) }}</b></div><div><span>{{ language.text('salesCreated') }}</span><b>{{ record.createdAt | date:'medium' }}</b></div><div><span>{{ language.text('salesUpdated') }}</span><b>{{ record.updatedAt | date:'medium' }}</b></div><div class="summary-wide"><span>{{ language.text('salesBoundary') }}</span><p>{{ language.text('salesBoundaryText') }}</p></div></div></ng-template>

    <ng-template #lines let-record="record"><div class="record-table-wrap"><table class="record-table"><caption class="sr-only">{{ language.text('salesLines') }}</caption><thead><tr><th scope="col">{{ language.text('product') }}</th><th scope="col">{{ language.text('unitOfMeasure') }}</th><th scope="col">{{ language.text('quantity') }}</th><th scope="col">{{ language.text('salesResolvedPrice') }}</th><th scope="col">{{ language.text('salesDiscount') }}</th><th scope="col">{{ language.text('salesLineTotal') }}</th><th scope="col">{{ language.text('salesPriceEvidence') }}</th></tr></thead><tbody>@for (line of record.lines; track line.id) {<tr><td><span class="record-name">{{ line.productSku }}</span><small>{{ line.productName }}</small></td><td>{{ line.unitOfMeasureCode }}</td><td>{{ line.quantity }}</td><td><strong>{{ line.unitPrice | number:'1.2-8' }}</strong><small>{{ language.text('salesOriginalPrice') }} {{ line.resolvedUnitPrice | number:'1.2-8' }}</small></td><td>{{ line.discountPercent | number:'1.0-2' }}%</td><td><strong>{{ line.lineTotal | number:'1.2-2' }}</strong></td><td><span class="evidence-chip">{{ line.priceProvenance }} · v{{ line.priceVersionNumber || '?' }}</span>@if (line.manualPriceApplied) {<small>{{ line.commercialAuthorityPolicyId }}</small>}</td></tr>}</tbody></table></div></ng-template>

    <ng-template #revisionList><section class="evidence-list"><h3>{{ language.text('salesRevisionEvidence') }}</h3>@if (revisions().length === 0) {<p class="muted-line">{{ language.text('salesNoRevisions') }}</p>} @else {@for (revision of revisions(); track revision.id) {<article class="evidence-row"><div><b>R{{ revision.revisionNumber }} · {{ statusLabel(revision.status) }}</b><small>{{ revision.occurredAt | date:'medium' }} · {{ revision.actorId }}</small></div><code>{{ revision.snapshotHash.slice(0, 18) }}…</code><span>{{ revision.reason || language.text('salesOriginalRevision') }}</span></article>}}</section></ng-template>
    <ng-template #historyList><section class="evidence-list"><h3>{{ language.text('salesHistory') }}</h3>@if (history().length === 0) {<p class="muted-line">{{ language.text('salesNoHistory') }}</p>} @else {@for (entry of history(); track entry.id) {<article class="evidence-row"><div><b>{{ entry.action }}</b><small>{{ entry.occurredAt | date:'medium' }} · {{ entry.actorId }}</small></div><span>{{ entry.fromStatus || '—' }} → {{ entry.toStatus || '—' }}</span><span>{{ entry.reason || entry.creditOutcome || language.text('salesNoReason') }}</span></article>}}</section></ng-template>
    <ng-template #auditList><section class="evidence-list"><h3>{{ language.text('audit') }}</h3>@if (audit().length === 0) {<p class="muted-line">{{ language.text('salesNoAudit') }}</p>} @else {@for (entry of audit(); track entry.id) {<article class="evidence-row"><div><b>{{ entry.operationId }}</b><small>{{ entry.occurredAt | date:'medium' }} · {{ entry.actorId }}</small></div><span class="evidence-chip">{{ entry.decision }}</span><span>{{ entry.reason || entry.afterSummary || language.text('salesNoReason') }}</span></article>}}</section></ng-template>
    <ng-template #fulfillmentPanel let-record="record">
      <section class="fulfillment-panel">
        <div class="subsection-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('salesFulfillment') }}</p><h3>{{ language.text('salesFulfillmentTitle') }}</h3><p class="field-note">{{ language.text('salesFulfillmentLead') }}</p></div><div class="section-actions"><button class="button button--quiet" type="button" (click)="printFulfillment()">{{ language.text('salesPrintFulfillment') }}</button><button class="button button--quiet" type="button" (click)="loadFulfillment()">{{ language.text('refresh') }}</button></div></div>
        <div class="form-grid form-grid--context"><label class="form-field"><span>{{ language.text('salesWarehouse') }}</span><select [ngModel]="selectedWarehouseId()" (ngModelChange)="selectWarehouse($event)"><option value="">{{ language.text('salesSelectWarehouse') }}</option>@for (warehouse of warehouses(); track warehouse.warehouseId) {<option [value]="warehouse.warehouseId">{{ warehouse.displayName }}</option>}</select></label><label class="form-field"><span>{{ language.text('salesInvoiceSource') }}</span><select [ngModel]="selectedInvoiceDeliveryId()" (ngModelChange)="selectInvoiceDelivery($event || null)"><option [ngValue]="null">{{ language.text('salesAllPostedDeliveries') }}</option>@for (delivery of deliveries(); track delivery.id) { @if (delivery.status === 'Posted') {<option [value]="delivery.id">{{ delivery.id.slice(0, 8) }} · {{ delivery.postedAt | date:'mediumDate' }}</option>} }</select></label><label class="form-field"><span>{{ language.text('invoiceDate') }}</span><input type="date" [ngModel]="invoiceDate()" (ngModelChange)="invoiceDate.set($event)" /></label></div>
        @if (record.paymentTerm; as paymentTerm) { <article class="evidence-row"><div><b>{{ language.text('paymentTerms') }} · {{ paymentTerm.code }}</b><small>v{{ paymentTerm.versionNumber }} · {{ paymentTerm.effectiveOn | date:'mediumDate' }}</small></div><span>{{ paymentTerm.englishName || paymentTerm.arabicName || paymentTerm.referenceValue }}</span></article> }
        @if (fulfillment(); as state) {
          <div class="record-table-wrap"><table class="record-table"><caption class="sr-only">{{ language.text('salesFulfillment') }}</caption><thead><tr><th>{{ language.text('salesLine') }}</th><th>{{ language.text('quantity') }}</th><th>{{ language.text('salesAvailable') }}</th><th>{{ language.text('salesReserved') }}</th><th>{{ language.text('salesDelivered') }}</th><th>{{ language.text('salesInvoiced') }}</th><th>{{ language.text('salesStatus') }}</th></tr></thead><tbody>@for (line of state.lines; track line.orderLineId) {<tr><td>{{ line.orderLineId.slice(0, 8) }}</td><td>{{ line.orderedQuantity }}</td><td>{{ availabilityByLine()[line.orderLineId]?.onHandQuantity ?? '—' }} / {{ availabilityByLine()[line.orderLineId]?.availableQuantity ?? '—' }}<small>{{ language.text('salesOnHand') }} / {{ language.text('salesAvailable') }}</small></td><td>{{ line.reservedQuantity }} <small>{{ line.unallocatedQuantity }} {{ language.text('salesUnallocated') }}</small></td><td>{{ line.deliveredQuantity }} / {{ line.orderedQuantity }}</td><td>{{ line.invoicedQuantity }} / {{ line.deliveredQuantity }}</td><td><span class="status-pill" [class]="'status-pill status-pill--' + statusTone(line.status)"><i aria-hidden="true"></i>{{ line.status }}</span></td></tr>}</tbody></table></div>
          <div class="form-actions"><button class="button button--primary" type="button" (click)="reserveOrder()" [disabled]="saving() || !selectedWarehouseId() || record.status !== 'Confirmed'">{{ language.text('salesReserve') }}</button><button class="button" type="button" (click)="postDelivery()" [disabled]="saving() || !selectedWarehouseId() || record.status !== 'Confirmed'">{{ language.text('salesPostDelivery') }}</button><button class="button" type="button" (click)="checkInvoiceEligibility()" [disabled]="saving() || !record.paymentTerm">{{ language.text('salesCheckInvoiceEligibility') }}</button><button class="button button--primary" type="button" (click)="requestInvoice()" [disabled]="saving() || !record.paymentTerm || invoiceEligibility()?.status !== 'Eligible'">{{ language.text('salesRequestInvoice') }}</button></div>
          @if (invoiceEligibility(); as eligibility) { <p class="field-note" role="status">{{ eligibility.code }} - {{ eligibility.totalAmount | number:'1.2-2' }} {{ eligibility.currencyCode }}</p> }
          @if (warehouseReservations().length > 0) {
            <section class="evidence-list reservation-list" aria-labelledby="sales-reservations-title">
              <h3 id="sales-reservations-title">{{ language.text('salesReservations') }}</h3>
              @for (reservation of warehouseReservations(); track reservation.id) {
                <article class="evidence-row">
                  <div><b>{{ reservation.productSku }} · {{ reservation.productName }}</b><small>{{ reservation.warehouseName }} · {{ reservation.sourceLineId?.slice(0, 8) }}</small></div>
                  <span>{{ reservation.reservedQuantity }} {{ language.text('salesReserved') }} · {{ reservation.unallocatedQuantity }} {{ language.text('salesUnallocated') }}</span>
                  @if (reservation.status === 'Active') {
                    <div class="section-actions"><input class="reservation-quantity" type="number" min="0.000001" [max]="reservation.reservedQuantity" step="0.000001" [ngModel]="reductionQuantity(reservation)" (ngModelChange)="setReductionQuantity(reservation.id, $event)" [attr.aria-label]="language.text('salesReduceReservation')" /><button class="button" type="button" (click)="reduceReservation(reservation)" [disabled]="saving() || !reductionQuantity(reservation)">{{ language.text('salesReduceReservation') }}</button><button class="button button--danger" type="button" (click)="releaseReservation(reservation)" [disabled]="saving()">{{ language.text('salesReleaseReservation') }}</button></div>
                  } @else { <span class="evidence-chip">{{ reservation.status }}</span> }
                </article>
              }
            </section>
          }
          @if (deliveries().length > 0) { <p class="field-note">{{ deliveries().length }} {{ language.text('salesDeliveries') }} - {{ invoiceRequests().length }} {{ language.text('salesInvoiceRequests') }}</p> }
        } @else {
          <div class="state-card state-card--loading"><span class="loader" aria-hidden="true"></span><b>{{ language.text('salesLoading') }}</b></div>
        }
      </section>
    </ng-template>
    <ng-template #creditPanel let-record="record"><section class="credit-panel"><div class="credit-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('salesCredit') }}</p><h3>{{ creditLabel(credit()?.outcome || record.creditOutcome) }}</h3><p>{{ credit()?.reason || record.creditReason || language.text('salesNoCreditReason') }}</p></div>@if ((credit()?.outcome || record.creditOutcome) === 'Unknown') {<span class="unknown-badge">{{ language.text('salesCreditUnknown') }}</span>} @else {<span class="status-pill" [class]="'status-pill status-pill--' + statusTone(credit()?.outcome || record.creditOutcome)"><i aria-hidden="true"></i>{{ creditLabel(credit()?.outcome || record.creditOutcome) }}</span>}</div>@if (credit(); as currentCredit) {<div class="credit-metrics"><div><span>{{ language.text('salesOpenExposure') }}</span><b>{{ currentCredit.openReceivables ?? '—' }}</b></div><div><span>{{ language.text('salesNetExposure') }}</span><b>{{ currentCredit.netReceivableExposure ?? '—' }}</b></div><div><span>{{ language.text('salesProposedExposure') }}</span><b>{{ currentCredit.proposedExposure ?? '—' }}</b></div><div><span>{{ language.text('salesCreditLimit') }}</span><b>{{ currentCredit.creditLimit ?? '—' }}</b></div></div><small class="field-note">{{ language.text('salesCreditAsOf') }} {{ currentCredit.asOfDate | date:'mediumDate' }}</small>}</section>@if (record.status === 'CreditHold') {<form class="override-card" (ngSubmit)="overrideCredit(record)" novalidate><h3>{{ language.text('salesCreditOverride') }}</h3><p>{{ language.text('salesCreditOverrideLead') }}</p><label class="form-field"><span>{{ language.text('salesOverrideReason') }}</span><textarea name="overrideReason" rows="2" [(ngModel)]="overrideReason" required></textarea></label><label class="form-field"><span>{{ language.text('salesOverrideExpiry') }}</span><input name="overrideExpiry" type="datetime-local" [(ngModel)]="overrideExpiry" required /></label><button class="button button--primary" type="submit" [disabled]="saving()">{{ language.text('salesGrantOverride') }}</button></form>}</ng-template>
  `,
  styles: `
    :host { display:block; --sales-ink:#172d2c; --sales-paper:#fffdf8; --sales-copper:#b56b3a; --sales-rule:#d9d5ca; }
    .sales-workspace { display:grid; gap:1.15rem; }
    
    .hero-lede { max-width:38rem; margin:.8rem 0 0; color:#c4d4ce; line-height:1.6; }.hero-ledger { display:grid; align-content:center; gap:.65rem; min-width:min(100%,22rem); }.hero-ledger div { display:grid; grid-template-columns:2rem 1fr; column-gap:.6rem; border-block-end:1px solid rgb(255 255 255 / 18%); padding:.35rem 0 .55rem; }.hero-ledger span { color:#e4a77d; font:800 .72rem/1 var(--font-mono); }.hero-ledger b,.hero-ledger small { grid-column:2; }.hero-ledger b { font-size:.75rem; }.hero-ledger small { color:#b9cec6; font-size:.68rem; line-height:1.35; }
    .sales-switcher { display:flex; gap:.3rem; border-block-end:1px solid var(--line); }.sales-switcher a { color:var(--ink-muted); padding:.7rem .85rem; font-size:.78rem; font-weight:800; text-decoration:none; }.sales-switcher a.is-active { color:var(--sales-ink); border-block-end:3px solid var(--sales-copper); }
    .paper-panel { border:1px solid var(--sales-rule); border-radius:1rem; padding:clamp(1rem,2.5vw,1.6rem); background:var(--sales-paper); box-shadow:0 .8rem 2.6rem rgb(23 45 44 / 7%); }.section-heading,.detail-heading,.subsection-heading { display:flex; align-items:flex-start; justify-content:space-between; gap:1rem; }.section-heading h2,.detail-heading h2 { margin:.15rem 0 0; font:800 clamp(1.45rem,3vw,2.3rem)/1.05 var(--font-display); letter-spacing:-.045em; }.section-heading p:not(.eyebrow),.detail-heading p:not(.eyebrow) { color:var(--ink-muted); line-height:1.5; }.section-actions,.detail-actions,.form-actions { display:flex; align-items:center; flex-wrap:wrap; gap:.55rem; }.button { display:inline-flex; align-items:center; justify-content:center; min-height:2.5rem; border:1px solid var(--line-strong); border-radius:.55rem; padding:.55rem .75rem; color:var(--ink); background:transparent; font-weight:800; font-size:.75rem; text-decoration:none; cursor:pointer; }.button:hover { border-color:var(--sales-copper); }.button--primary { border-color:var(--sales-copper); color:#fff; background:var(--sales-copper); }.button--danger { color:var(--danger); border-color:color-mix(in srgb,var(--danger) 40%,var(--line)); }.button--quiet { background:#f4f1e9; }.button:disabled { cursor:wait; opacity:.55; }
    .sales-toolbar { display:flex; align-items:end; flex-wrap:wrap; gap:.7rem; margin:1.2rem 0; border-block:1px solid var(--sales-rule); padding:.85rem 0; }.sales-search { position:relative; flex:1 1 16rem; }.sales-search input { width:100%; min-height:2.6rem; border:1px solid var(--line-strong); border-radius:.5rem; padding:.55rem 2rem .55rem .7rem; background:#fff; }.sales-search span { position:absolute; inset-inline-end:.7rem; inset-block-start:.58rem; color:var(--sales-copper); font-size:1.1rem; }.sales-status-filter { display:grid; gap:.25rem; min-width:10rem; color:var(--ink-muted); font-size:.7rem; font-weight:800; }.sales-status-filter select { min-height:2.6rem; border:1px solid var(--line-strong); border-radius:.5rem; padding:.4rem; background:#fff; }.toolbar-count { color:var(--ink-muted); font-size:.72rem; font-weight:800; }
    .record-table-wrap { overflow-x:auto; border:1px solid var(--sales-rule); border-radius:.65rem; }.record-table { width:100%; border-collapse:collapse; font-size:.76rem; }.record-table th,.record-table td { border-block-end:1px solid var(--sales-rule); padding:.78rem .65rem; text-align:start; vertical-align:middle; }.record-table th { color:var(--ink-muted); background:#f3f0e8; font-size:.63rem; letter-spacing:.07em; text-transform:uppercase; }.record-table tbody tr:hover { background:#fbf7ee; }.record-table tbody tr:last-child td { border-block-end:0; }.record-name,.record-code,.record-table small { display:block; }.record-code { border:0; padding:0; color:var(--sales-ink); background:transparent; font-weight:850; text-align:start; cursor:pointer; }.record-code:hover { color:var(--sales-copper); }.record-table small,.record-cards small { margin-top:.2rem; color:var(--ink-muted); font-size:.66rem; }.amount { white-space:nowrap; }.status-pill { display:inline-flex; align-items:center; gap:.35rem; border:1px solid var(--line); border-radius:99px; padding:.3rem .55rem; color:var(--ink-muted); background:#fff; font-size:.65rem; font-weight:850; white-space:nowrap; }.status-pill i { width:.4rem; height:.4rem; border-radius:50%; background:currentColor; }.status-pill--Draft,.status-pill--ReturnedForChange,.status-pill--Pending { color:#8b6a32; background:#fff6df; }.status-pill--PendingApproval,.status-pill--Warning { color:#986221; background:#fff1df; }.status-pill--Approved,.status-pill--Eligible,.status-pill--Confirmed,.status-pill--Overridden { color:#267455; background:#e9f6ed; }.status-pill--Rejected,.status-pill--Cancelled,.status-pill--Withdrawn,.status-pill--Blocked,.status-pill--CreditHold { color:#a84440; background:#fff0ed; }.status-pill--Unknown { color:#6b5a72; background:#f3eef6; }.record-cards { display:none; }.record-card { width:100%; border:1px solid var(--sales-rule); border-radius:.7rem; padding:.9rem; color:var(--ink); background:#fff; text-align:start; }.card-top,.card-facts { display:flex; justify-content:space-between; gap:.6rem; }.record-card strong { display:block; margin-top:.7rem; }.card-facts { margin-top:.9rem; color:var(--ink-muted); font-size:.7rem; }.card-facts b { display:block; margin-top:.2rem; color:var(--ink); }
    .detail-topline { margin-bottom:1rem; }.back-link { border:0; padding:0; color:var(--sales-copper); background:transparent; font-weight:800; cursor:pointer; }.scope-stamp { border:1px solid color-mix(in srgb,var(--sales-copper) 34%,var(--line)); border-radius:99px; padding:.35rem .6rem; color:var(--sales-copper); font:800 .64rem/1 var(--font-mono); text-transform:uppercase; }.commercial-strip { display:grid; grid-template-columns:repeat(4,1fr); gap:0; margin:1.25rem 0; border-block:1px solid var(--sales-rule); }.commercial-strip > div { min-height:4.2rem; border-inline-end:1px solid var(--sales-rule); padding:.75rem .8rem; }.commercial-strip > div:last-child { border-inline-end:0; }.commercial-strip span,.commercial-strip b,.commercial-strip small,.summary-grid span,.summary-grid b { display:block; }.commercial-strip span,.summary-grid span { color:var(--ink-muted); font-size:.65rem; font-weight:800; letter-spacing:.04em; text-transform:uppercase; }.commercial-strip b { margin-top:.3rem; font-size:.82rem; }.commercial-strip small { margin-top:.2rem; color:var(--ink-muted); font-size:.68rem; }.commercial-strip__total { background:#f8efe6; }.commercial-strip__total b { color:var(--sales-copper); font:850 1.08rem/1.2 var(--font-display); }.tabs { display:flex; gap:.2rem; overflow-x:auto; margin-bottom:1.1rem; border-block-end:1px solid var(--sales-rule); }.tabs button { border:0; border-block-end:3px solid transparent; padding:.7rem .75rem; color:var(--ink-muted); background:transparent; font-size:.75rem; font-weight:800; cursor:pointer; white-space:nowrap; }.tabs button.is-active { border-block-end-color:var(--sales-copper); color:var(--sales-ink); }.summary-grid { display:grid; grid-template-columns:repeat(3,1fr); gap:1px; border:1px solid var(--sales-rule); background:var(--sales-rule); }.summary-grid > div { min-height:4.3rem; padding:.8rem; background:#fffdf8; }.summary-grid b { margin-top:.35rem; font-size:.78rem; overflow-wrap:anywhere; }.summary-wide { grid-column:1/-1; }.summary-wide p { color:var(--ink-muted); line-height:1.5; }.evidence-list { display:grid; gap:.6rem; }.evidence-list h3 { margin:0 0 .25rem; font:800 1rem/1.2 var(--font-display); }.evidence-row { display:grid; grid-template-columns:minmax(10rem,1.1fr) minmax(8rem,1fr) minmax(8rem,1fr); gap:.7rem; border-inline-start:3px solid var(--sales-copper); border-block-end:1px solid var(--sales-rule); padding:.65rem .75rem; background:#fff; }.evidence-row b,.evidence-row small { display:block; }.evidence-row small { margin-top:.2rem; color:var(--ink-muted); font-size:.65rem; }.evidence-row span,.evidence-row code { align-self:center; color:var(--ink-muted); font-size:.7rem; overflow-wrap:anywhere; }.evidence-chip { display:inline-flex; width:max-content; border-radius:99px; padding:.24rem .45rem; color:var(--sales-copper); background:#f9eee7; font-size:.64rem; font-weight:800; }.line-editor { margin-top:1.4rem; border-block:1px solid var(--sales-rule); padding:1rem 0; }.line-editor h3 { margin:.1rem 0 0; font:800 1.2rem/1.2 var(--font-display); }.field-note { color:var(--ink-muted); font-size:.72rem; line-height:1.45; }.line-row { display:grid; grid-template-columns:2rem 1.4fr 1fr .6fr .7fr 2rem; align-items:end; gap:.55rem; margin-top:.75rem; border:1px solid var(--sales-rule); padding:.7rem; background:#fff; }.line-index { align-self:center; color:var(--sales-copper); font:800 .72rem var(--font-mono); }.form-grid { display:grid; gap:.8rem; }.form-grid--context { grid-template-columns:repeat(4,1fr); }.sales-form { display:grid; gap:.85rem; margin-top:1.2rem; }.form-field { display:grid; gap:.32rem; color:var(--ink-muted); font-size:.72rem; font-weight:800; }.form-field input,.form-field select,.form-field textarea { width:100%; min-height:2.55rem; border:1px solid var(--line-strong); border-radius:.45rem; padding:.55rem .6rem; color:var(--ink); background:#fff; font-weight:500; }.form-field textarea { resize:vertical; }.form-field small { color:var(--ink-muted); font-size:.64rem; font-weight:500; line-height:1.35; }.form-field--short { min-width:0; }.remove-line { width:2rem; height:2rem; border:1px solid var(--line); border-radius:.4rem; color:var(--danger); background:#fff; font-size:1.2rem; cursor:pointer; }.remove-line:disabled { cursor:not-allowed; opacity:.4; }.credit-panel { display:grid; gap:1rem; border:1px solid var(--sales-rule); padding:1rem; background:#fffdf8; }.credit-heading { display:flex; justify-content:space-between; gap:1rem; }.credit-heading h3 { margin:.2rem 0; font:850 1.35rem/1.1 var(--font-display); }.credit-heading p { color:var(--ink-muted); }.credit-text--hold,.unknown-badge { color:var(--danger); }.unknown-badge { border:1px solid color-mix(in srgb,var(--danger) 35%,var(--line)); border-radius:99px; padding:.35rem .55rem; font-size:.65rem; font-weight:850; }.credit-metrics { display:grid; grid-template-columns:repeat(4,1fr); gap:.6rem; }.credit-metrics div { border-top:2px solid var(--sales-copper); padding:.55rem; background:#fff; }.credit-metrics span,.credit-metrics b { display:block; }.credit-metrics span { color:var(--ink-muted); font-size:.65rem; }.credit-metrics b { margin-top:.3rem; font-size:.85rem; }.override-card { display:grid; gap:.7rem; margin-top:1rem; border:1px solid color-mix(in srgb,var(--danger) 28%,var(--sales-rule)); padding:1rem; background:#fff8f3; }.override-card h3 { margin:0; font:800 1rem var(--font-display); }.override-card p { margin:0; color:var(--ink-muted); font-size:.75rem; }.state-card { display:flex; gap:.8rem; align-items:flex-start; border:1px dashed var(--sales-rule); padding:1.2rem; color:var(--ink-muted); }.state-card b { color:var(--ink); }.state-card p { margin:.3rem 0; }.state-card--error { border-color:color-mix(in srgb,var(--danger) 36%,var(--line)); }.inline-alert { display:flex; flex-wrap:wrap; gap:.5rem; align-items:center; margin:1rem 0; border-inline-start:3px solid var(--danger); padding:.7rem .8rem; color:var(--danger); background:#fff2ef; font-size:.75rem; }.text-button { border:0; padding:0; color:var(--sales-copper); background:transparent; font-weight:800; cursor:pointer; }.muted-line { color:var(--ink-muted); font-size:.78rem; }
    @media (max-width:900px) { .hero-ledger { min-width:0; }.form-grid--context { grid-template-columns:repeat(2,1fr); }.line-row { grid-template-columns:2rem 1fr 1fr; }.line-row .form-field--short,.line-row .remove-line { grid-column:auto; } .commercial-strip { grid-template-columns:repeat(2,1fr); }.commercial-strip > div:nth-child(2) { border-inline-end:0; }.commercial-strip > div:nth-child(-n+2) { border-block-end:1px solid var(--sales-rule); }.credit-metrics { grid-template-columns:repeat(2,1fr); } }
    @media (max-width:620px) { .section-heading,.detail-heading,.subsection-heading,.credit-heading { flex-direction:column; }.section-actions,.detail-actions { width:100%; }.section-actions .button,.detail-actions .button { flex:1; }.form-grid--context,.summary-grid { grid-template-columns:1fr; }.summary-wide { grid-column:auto; }.commercial-strip { grid-template-columns:1fr; }.commercial-strip > div { border-inline-end:0; border-block-end:1px solid var(--sales-rule); }.commercial-strip > div:last-child { border-block-end:0; }.record-table-wrap { display:none; }.record-cards { display:grid; gap:.6rem; }.line-row { grid-template-columns:1.7rem 1fr; }.line-row .form-field { grid-column:2; }.line-row .remove-line { grid-column:2; justify-self:start; }.credit-metrics { grid-template-columns:1fr 1fr; }.evidence-row { grid-template-columns:1fr; gap:.3rem; } }
    @media (prefers-reduced-motion:reduce) { * { scroll-behavior:auto !important; transition:none !important; } }
  `,
})
export class SalesWorkspaceComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sales = inject(SalesService);
  private readonly inventory = inject(InventoryService);
  private readonly masterData = inject(MasterDataService);
  private readonly priceListsApi = inject(PriceListService);
  private readonly purchaseRequests = inject(PurchaseRequestService);

  readonly documentType = signal<WorkspaceDocument>('quotation');
  readonly mode = signal<WorkspaceMode>('list');
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly saving = signal(false);
  readonly listError = signal<SafeUiError | null>(null);
  readonly detailError = signal<SafeUiError | null>(null);
  readonly mutationError = signal<SafeUiError | null>(null);
  readonly referenceError = signal<SafeUiError | null>(null);
  readonly quotations = signal<SalesQuotationSummaryResponse[]>([]);
  readonly orders = signal<SalesOrderSummaryResponse[]>([]);
  readonly selectedQuotation = signal<SalesQuotationResponse | null>(null);
  readonly selectedOrder = signal<SalesOrderResponse | null>(null);
  readonly revisions = signal<SalesQuotationRevisionResponse[]>([]);
  readonly history = signal<SalesHistoryResponse[]>([]);
  readonly audit = signal<SalesAuditResponse[]>([]);
  readonly credit = signal<SalesCreditResponse | null>(null);
  readonly fulfillment = signal<SalesFulfillmentResponse | null>(null);
  readonly deliveries = signal<SalesDeliveryResponse[]>([]);
  readonly invoiceRequests = signal<SalesInvoiceRequestResponse[]>([]);
  readonly warehouses = signal<InventoryWarehouseOption[]>([]);
  readonly warehouseReservations = signal<InventoryReservation[]>([]);
  readonly reservationReductions = signal<Record<string, number>>({});
  readonly availabilityByLine = signal<Record<string, InventoryAvailability>>({});
  readonly selectedWarehouseId = signal('');
  readonly selectedInvoiceDeliveryId = signal<string | null>(null);
  readonly invoiceDate = signal(new Date().toISOString().slice(0, 10));
  readonly invoiceEligibility = signal<SalesInvoiceEligibilityResponse | null>(null);
  readonly customers = signal<CustomerRecord[]>([]);
  readonly currencies = signal<CurrencyRecord[]>([]);
  readonly products = signal<ProductRecord[]>([]);
  readonly units = signal<UnitOfMeasureRecord[]>([]);
  readonly priceLists = signal<PriceListRecord[]>([]);
  readonly paymentTerms = signal<PaymentTermRecord[]>([]);
  readonly taxes = signal<TaxRecord[]>([]);
  readonly exchangeRates = signal<ExchangeRateRecord[]>([]);
  readonly organizationScopes = signal<PurchaseRequestOrganizationScopeResponse[]>([]);
  readonly detailTab = signal<DetailTab>('summary');
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly overrideReason = signal('');
  readonly overrideExpiry = signal('');
  draft: QuotationDraft = this.emptyDraft();
  private currentId: string | null = null;

  readonly filteredRecords = computed(() => {
    const query = this.search().trim().toLowerCase();
    const records = this.documentType() === 'quotation' ? this.quotations() : this.orders();
    if (!query) return records;
    return records.filter(record => `${record.number} ${record.customerName} ${record.customerCode} ${record.currencyCode}`.toLowerCase().includes(query));
  });

  gridColumns(): DataGridColumn<SalesListRecord>[] { return [
    { key: 'status', label: this.language.text('salesStatus'), value: (record) => record.status, display: (record) => this.statusLabel(record.status), badge: true, filter: 'select', filterOptions: this.statusOptions().map((status) => ({ value: status, label: this.statusLabel(status) })) },
    { key: 'document', label: this.language.text('salesDocument'), value: (record) => record.number, secondaryText: (record) => `${this.shortId(record.id)} · ${'sourceQuotationNumber' in record ? `Q${record.sourceQuotationRevision}` : `R${record.revisionNumber}`}`, link: (record) => `/app/sales/${this.documentType() === 'quotation' ? 'quotations' : 'orders'}/${record.id}`, filter: 'text' },
    { key: 'customer', label: this.language.text('customer'), value: (record) => record.customerName, secondaryText: (record) => record.customerCode, filter: 'text' },
    { key: 'source', label: this.language.text('salesSource'), value: (record) => this.sourceLabel(record), filter: 'text' },
    { key: 'total', label: this.language.text('salesTotal'), value: (record) => record.total, display: (record) => `${record.total.toLocaleString(this.language.language() === 'ar' ? 'ar-SA' : 'en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${record.currencyCode}`, filter: 'number-range', align: 'end' },
    { key: 'updated', label: this.language.text('salesUpdated'), value: (record) => record.updatedAt, display: (record) => new Date(record.updatedAt).toLocaleDateString(this.language.language()), filter: 'date-range' },
  ]; }
  gridActions(): readonly { key: string; label: string }[] { return [{ key: 'view', label: this.language.text('viewRecord') }]; }
  onGridAction(event: { row: SalesListRecord }): void { this.openRecord(event.row.id); }

  ngOnInit(): void {
    combineLatest([this.route.url, this.route.paramMap]).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(([segments, params]) => {
      this.documentType.set(segments.some(segment => segment.path === 'orders') ? 'order' : 'quotation');
      const isCreate = segments.some(segment => segment.path === 'new');
      const isEdit = segments.some(segment => segment.path === 'edit');
      const id = params.get('id');
      this.currentId = id;
      this.mode.set(isCreate ? 'create' : isEdit && id ? 'edit' : id ? 'view' : 'list');
      this.detailTab.set('summary');
      this.mutationError.set(null);
      if (isCreate) { this.draft = this.emptyDraft(); void this.loadReferences(); }
      else if (id) { void this.loadDetail(); if (isEdit) void this.loadReferences(); }
      else void this.loadList();
    });
  }

  documentLabel(): string { return this.documentType() === 'quotation' ? this.language.text('salesQuotationsNavLabel') : this.language.text('salesOrdersNavLabel'); }
  statusOptions(): string[] { return this.documentType() === 'quotation' ? ['Draft', 'PendingApproval', 'Approved', 'Sent', 'Expired', 'Converted', 'Withdrawn', 'Rejected', 'ReturnedForChange', 'Cancelled'] : ['Draft', 'PendingApproval', 'Approved', 'CreditHold', 'Confirmed', 'Rejected', 'ReturnedForChange', 'Cancelled']; }
  applyFilter(): void { this.search.set(this.search().trim()); }

  async loadList(): Promise<void> {
    this.loading.set(true); this.listError.set(null);
    try {
      if (this.documentType() === 'quotation') this.quotations.set(await firstValueFrom(this.sales.quotations(this.statusFilter() as SalesQuotationStatus | '')));
      else this.orders.set(await firstValueFrom(this.sales.orders(this.statusFilter() as SalesOrderStatus | '')));
    } catch (error) { this.listError.set(toSafeUiError(error)); }
    finally { this.loading.set(false); }
  }

  async loadDetail(): Promise<void> {
    if (!this.currentId) return;
    this.detailLoading.set(true); this.detailError.set(null); this.selectedQuotation.set(null); this.selectedOrder.set(null); this.credit.set(null);
    try {
      if (this.documentType() === 'quotation') {
        const quote = await firstValueFrom(this.sales.quotation(this.currentId)); this.selectedQuotation.set(quote);
        if (this.mode() === 'edit') this.draft = this.draftFromQuotation(quote);
        const [revisions, history, audit] = await Promise.all([firstValueFrom(this.sales.quotationRevisions(this.currentId)), firstValueFrom(this.sales.quotationHistory(this.currentId)), firstValueFrom(this.sales.quotationAudit(this.currentId))]);
        this.revisions.set(revisions); this.history.set(history); this.audit.set(audit);
      } else {
        const order = await firstValueFrom(this.sales.order(this.currentId)); this.selectedOrder.set(order); if (this.mode() === 'edit') this.draft = this.draftFromOrder(order);
        const [history, audit, credit] = await Promise.all([firstValueFrom(this.sales.orderHistory(this.currentId)), firstValueFrom(this.sales.orderAudit(this.currentId)), firstValueFrom(this.sales.orderCredit(this.currentId))]);
        this.history.set(history); this.audit.set(audit); this.credit.set(credit); void this.loadFulfillment();
      }
    } catch (error) { this.detailError.set(toSafeUiError(error)); }
    finally { this.detailLoading.set(false); }
  }

  async loadReferences(): Promise<void> {
    this.referenceError.set(null);
    try {
      const [customers, currencies, products, units, priceLists, paymentTerms, taxes, exchangeRates, organizationScopes] = await Promise.all([
        firstValueFrom(this.masterData.list('customers')),
        firstValueFrom(this.masterData.list('currencies')),
        firstValueFrom(this.masterData.list('products')),
        firstValueFrom(this.masterData.list('units')),
        firstValueFrom(this.priceListsApi.list()),
        firstValueFrom(this.masterData.list('payment-terms')),
        firstValueFrom(this.masterData.list('taxes')),
        firstValueFrom(this.masterData.list('exchange-rates')),
        firstValueFrom(this.purchaseRequests.organizationScopes()),
      ]);
      this.customers.set(customers as CustomerRecord[]); this.currencies.set(currencies as CurrencyRecord[]); this.products.set(products as ProductRecord[]); this.units.set(units as UnitOfMeasureRecord[]); this.priceLists.set(priceLists);
      this.paymentTerms.set(paymentTerms as PaymentTermRecord[]); this.taxes.set(taxes as TaxRecord[]); this.exchangeRates.set(exchangeRates as ExchangeRateRecord[]);
      this.organizationScopes.set(organizationScopes ?? []);
      if (this.mode() === 'create' && !this.draft.companyId && organizationScopes.length === 1) this.setOrganizationScope(this.organizationScopeKey(organizationScopes[0]));
    } catch (error) { this.referenceError.set(toSafeUiError(error)); }
  }

  openRecord(id: string): void { void this.router.navigate(['/app/sales', this.documentType() === 'quotation' ? 'quotations' : 'orders', id]); }
  editQuotation(id: string): void { void this.router.navigate(['/app/sales/quotations', id, 'edit']); }
  editOrder(id: string): void { void this.router.navigate(['/app/sales/orders', id, 'edit']); }
  backToList(): void { void this.router.navigate(['/app/sales', this.documentType() === 'quotation' ? 'quotations' : 'orders']); }
  setTab(tab: DetailTab): void { this.detailTab.set(tab); }
  async loadFulfillment(): Promise<void> {
    const order = this.selectedOrder();
    if (!order) return;
    try {
      const state = await firstValueFrom(this.sales.fulfillment(order.id));
      this.fulfillment.set(state); this.deliveries.set(state.deliveries); this.invoiceRequests.set(state.invoiceRequests);
      const [warehouses, reservations] = await Promise.all([
        firstValueFrom(this.inventory.warehouses(order.companyId, order.branchId ?? undefined)),
        firstValueFrom(this.inventory.reservations({ companyId: order.companyId, branchId: order.branchId ?? undefined })),
      ]);
      const authorizedWarehouses = warehouses.filter(item => item.isActive && item.companyId === order.companyId && item.branchId === order.branchId);
      this.warehouses.set(authorizedWarehouses);
      if (!authorizedWarehouses.some(item => item.warehouseId === this.selectedWarehouseId())) this.selectedWarehouseId.set(authorizedWarehouses[0]?.warehouseId ?? '');
      this.warehouseReservations.set(reservations.filter(item => item.sourceDocumentId === order.id && item.sourceRevision === (order.revisionNumber ?? 1)));
      if (this.selectedInvoiceDeliveryId() && !state.deliveries.some(item => item.id === this.selectedInvoiceDeliveryId() && item.status === 'Posted')) this.selectedInvoiceDeliveryId.set(null);
      const availability: Record<string, InventoryAvailability> = {};
      const warehouseId = this.selectedWarehouseId();
      if (warehouseId) await Promise.all(order.lines.map(async line => {
        try {
          availability[line.id] = await firstValueFrom(this.inventory.availability({ warehouseId, companyId: order.companyId, branchId: order.branchId, productId: line.productId, unitOfMeasureId: line.unitOfMeasureId }));
        } catch { /* unauthorized line visibility remains absent rather than fabricated */ }
      }));
      this.availabilityByLine.set(availability);
    } catch (error) { this.detailError.set(toSafeUiError(error)); }
  }
  selectWarehouse(id: string): void { this.selectedWarehouseId.set(id); }
  selectInvoiceDelivery(id: string | null): void { this.selectedInvoiceDeliveryId.set(id); this.invoiceEligibility.set(null); }
  reductionQuantity(reservation: InventoryReservation): number { return this.reservationReductions()[reservation.id] ?? Math.min(1, reservation.reservedQuantity); }
  setReductionQuantity(id: string, quantity: number): void { this.reservationReductions.update(values => ({ ...values, [id]: Number(quantity) })); }
  async reduceReservation(reservation: InventoryReservation): Promise<void> {
    const quantity = this.reductionQuantity(reservation);
    if (quantity <= 0 || quantity > reservation.reservedQuantity) return;
    this.saving.set(true); this.mutationError.set(null);
    try { await this.inventory.reduceReservation(reservation.id, reservation.version, quantity, 'sales fulfillment reduction'); await this.loadFulfillment(); }
    catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  async releaseReservation(reservation: InventoryReservation): Promise<void> {
    this.saving.set(true); this.mutationError.set(null);
    try { await this.inventory.releaseReservation(reservation.id, reservation.version, 'sales fulfillment release'); await this.loadFulfillment(); }
    catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  printFulfillment(): void { window.print(); }
  async reserveOrder(): Promise<void> {
    const order = this.selectedOrder();
    if (!order || !this.selectedWarehouseId() || !this.fulfillment()) return;
    this.saving.set(true); this.mutationError.set(null);
    try {
      await this.sales.reserveOrder(order.id, { warehouseId: this.selectedWarehouseId(), lines: this.fulfillment()!.lines.filter(line => line.remainingFulfillableQuantity > 0).map(line => ({ orderLineId: line.orderLineId, quantity: line.orderedQuantity })) }, order.version);
      await this.loadFulfillment();
    } catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  async postDelivery(): Promise<void> {
    const order = this.selectedOrder();
    if (!order || !this.selectedWarehouseId() || !this.fulfillment()) return;
    this.saving.set(true); this.mutationError.set(null);
    try {
      const reservations = await firstValueFrom(this.inventory.reservations({ companyId: order.companyId, branchId: order.branchId ?? undefined, warehouseId: this.selectedWarehouseId() }));
      const lines = this.fulfillment()!.lines.map(line => {
        const reservation = reservations.find(item => item.status === 'Active' && item.sourceDocumentId === order.id && item.sourceRevision === (order.revisionNumber ?? 1) && item.sourceLineId === line.orderLineId && item.warehouseId === this.selectedWarehouseId() && item.reservedQuantity > 0);
        return reservation && line.remainingFulfillableQuantity > 0 ? { orderLineId: line.orderLineId, reservationId: reservation.id, quantity: Math.min(line.remainingFulfillableQuantity, reservation.reservedQuantity) } : null;
      }).filter((line): line is { orderLineId: string; reservationId: string; quantity: number } => line !== null);
      if (!lines.length) throw new Error('No reserved quantity is available for delivery.');
      await this.sales.postDelivery(order.id, { warehouseId: this.selectedWarehouseId(), deliveryDate: new Date().toISOString().slice(0, 10), lines }, order.version);
      await this.loadFulfillment();
    } catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  async checkInvoiceEligibility(): Promise<void> {
    const order = this.selectedOrder(); const state = this.fulfillment();
    if (!order || !state || !order.paymentTerm) return;
    this.saving.set(true); this.mutationError.set(null);
    try {
      const lines = state.lines.filter(line => line.remainingInvoiceableQuantity > 0).map(line => ({ orderLineId: line.orderLineId, quantity: line.remainingInvoiceableQuantity }));
      this.invoiceEligibility.set(await firstValueFrom(this.sales.evaluateInvoiceEligibility(order.id, { deliveryId: this.selectedInvoiceDeliveryId(), paymentTermId: null, invoiceDate: this.invoiceDate(), lines })));
    } catch (error) { this.invoiceEligibility.set(null); this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  async requestInvoice(): Promise<void> {
    const order = this.selectedOrder(); const eligibility = this.invoiceEligibility();
    if (!order || !eligibility || eligibility.status !== 'Eligible' || !order.paymentTerm) return;
    this.saving.set(true); this.mutationError.set(null);
    try {
      await this.sales.requestInvoice(order.id, { deliveryId: this.selectedInvoiceDeliveryId(), paymentTermId: null, invoiceDate: this.invoiceDate(), lines: eligibility.lines.map(line => ({ orderLineId: line.orderLineId, quantity: line.requestedQuantity })) }, order.version);
      this.invoiceEligibility.set(null); await this.loadFulfillment();
    } catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }
  addLine(): void { this.draft.lines = [...this.draft.lines, this.emptyLine()]; }
  removeLine(index: number): void { if (this.draft.lines.length > 1) this.draft.lines = this.draft.lines.filter((_, lineIndex) => lineIndex !== index); }

  async saveQuotation(): Promise<void> {
    this.saving.set(true); this.mutationError.set(null);
    const payload: SalesQuotationCreateRequest = { companyId: this.draft.companyId.trim(), branchId: this.draft.branchId.trim() || null, customerId: this.draft.customerId, quotationDate: this.draft.quotationDate, validUntil: this.draft.validUntil, currencyId: this.draft.currencyId, priceListId: this.draft.priceListId || null, customerContactId: this.draft.customerContactId.trim() || null, notes: this.draft.notes.trim() || null, customerReference: this.draft.customerReference.trim() || null, exchangeRateId: this.draft.exchangeRateId || null, paymentTermId: this.draft.paymentTermId || null, lines: this.draft.lines.map(line => ({ productId: line.productId, unitOfMeasureId: line.unitOfMeasureId, quantity: Number(line.quantity), unitPriceOverride: line.unitPriceOverride, discountPercent: Number(line.discountPercent), taxId: line.taxId || null, notes: line.notes.trim() || null })) };
    try {
      if (this.mode() === 'edit' && this.documentType() === 'order' && this.currentId && this.selectedOrder()) {
        const editPayload: SalesOrderEditRequest = { currencyId: payload.currencyId, priceListId: payload.priceListId, exchangeRateId: payload.exchangeRateId, lines: payload.lines };
        await this.sales.editOrder(this.currentId, editPayload, this.selectedOrder()!.version);
      }
      else if (this.mode() === 'edit' && this.currentId && this.selectedQuotation()) {
        const { customerId: _customerId, quotationDate: _quotationDate, ...editPayload } = payload;
        await this.sales.editQuotation(this.currentId, editPayload, this.selectedQuotation()!.version);
      }
      else await this.sales.createQuotation(payload);
      await this.backToList();
    } catch (error) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }

  canEditQuotation(record: SalesQuotationResponse): boolean { return record.status === 'Draft' || record.status === 'ReturnedForChange'; }
  canEditOrder(record: SalesOrderResponse): boolean { return record.status === 'Draft' || record.status === 'ReturnedForChange'; }
  quotationActions(record: SalesQuotationResponse): { key: 'submit' | 'approve' | 'reject' | 'return' | 'send' | 'withdraw' | 'cancel' | 'convert'; label: string }[] {
    const labels = { submit: this.language.text('submitForApproval'), approve: this.language.text('approveRequest'), reject: this.language.text('rejectRequest'), return: this.language.text('returnForChange'), send: this.language.text('salesSendQuotation'), withdraw: this.language.text('salesWithdrawQuotation'), cancel: this.language.text('cancelRequest'), convert: this.language.text('salesConvertToOrder') } as const;
    if (record.status === 'Draft' || record.status === 'ReturnedForChange') return [{ key: 'submit', label: labels.submit }];
    if (record.status === 'PendingApproval') return [{ key: 'approve', label: labels.approve }, { key: 'return', label: labels.return }, { key: 'reject', label: labels.reject }];
    if (record.status === 'Approved') return [{ key: 'send', label: labels.send }, { key: 'convert', label: labels.convert }, { key: 'withdraw', label: labels.withdraw }, { key: 'cancel', label: labels.cancel }];
    if (record.status === 'Sent') return [{ key: 'convert', label: labels.convert }, { key: 'withdraw', label: labels.withdraw }];
    return [];
  }
  orderActions(record: SalesOrderResponse): { key: 'submit' | 'approve' | 'reject' | 'return' | 'confirm' | 'cancel'; label: string }[] {
    const labels = { submit: this.language.text('submitForApproval'), approve: this.language.text('approveRequest'), reject: this.language.text('rejectRequest'), return: this.language.text('returnForChange'), confirm: this.language.text('salesConfirmOrder'), cancel: this.language.text('cancelRequest') } as const;
    if (record.status === 'Draft' || record.status === 'ReturnedForChange') return [{ key: 'submit', label: labels.submit }];
    if (record.status === 'PendingApproval') return [{ key: 'approve', label: labels.approve }, { key: 'return', label: labels.return }, { key: 'reject', label: labels.reject }];
    if (record.status === 'Approved' || record.status === 'CreditHold') return [{ key: 'confirm', label: labels.confirm }, { key: 'return', label: labels.return }, { key: 'cancel', label: labels.cancel }];
    return [];
  }

  async runQuotationAction(action: 'submit' | 'approve' | 'reject' | 'return' | 'send' | 'withdraw' | 'cancel' | 'convert'): Promise<void> {
    const quote = this.selectedQuotation(); if (!quote) return; this.saving.set(true); this.mutationError.set(null);
    try { if (action === 'convert') { const order = await this.sales.convertQuotation(quote.id, quote.version); await this.router.navigate(['/app/sales/orders', order.id]); } else { await this.sales.quotationAction(quote.id, action, quote.version); await this.loadDetail(); } }
    catch (error) { this.mutationError.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }
  async runOrderAction(action: 'submit' | 'approve' | 'reject' | 'return' | 'confirm' | 'cancel'): Promise<void> {
    const order = this.selectedOrder(); if (!order) return; this.saving.set(true); this.mutationError.set(null);
    try { await this.sales.orderAction(order.id, action, order.version); await this.loadDetail(); } catch (error) { this.mutationError.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }
  async overrideCredit(record: SalesOrderResponse): Promise<void> {
    if (!this.overrideReason().trim() || !this.overrideExpiry()) return;
    this.saving.set(true); this.mutationError.set(null);
    const payload: SalesCreditOverrideRequest = { reason: this.overrideReason().trim(), expiresAt: new Date(this.overrideExpiry()).toISOString(), scope: null, sourceReference: null };
    try { await this.sales.overrideCredit(record.id, payload, record.version); this.overrideReason.set(''); await this.loadDetail(); } catch (error) { this.mutationError.set(toSafeUiError(error)); } finally { this.saving.set(false); }
  }

  statusLabel(status: string): string { const key = `salesStatus${status}` as 'salesStatusDraft'; return this.language.text(key); }
  creditLabel(outcome: string): string { const key = `salesCredit${outcome}` as 'salesCreditUnknown'; return this.language.text(key); }
  statusTone(status: string): string { return status; }
  sourceLabel(record: SalesQuotationSummaryResponse | SalesOrderSummaryResponse): string { return 'sourceQuotationNumber' in record ? record.sourceQuotationNumber : `R${record.revisionNumber}`; }
  shortId(id: string): string { return id.slice(0, 8).toUpperCase(); }
  errorMessage(error: SafeUiError | null): string { if (!error) return this.language.text('requestError'); if (error.code === 'concurrency_conflict') return this.language.text('salesConcurrencyError'); if (error.code === 'access_denied' || error.code === 'permission_denied') return this.language.text('accessUnavailable'); if (error.code === 'network_error' || error.code === 'persistence_unavailable') return this.language.text('salesUnavailableError'); return this.language.text('salesValidationError'); }
  displayCustomer(customer: CustomerRecord): string { return `${customer.code} · ${this.language.language() === 'ar' ? customer.arabicTradingName || customer.arabicLegalName || customer.code : customer.englishTradingName || customer.englishLegalName || customer.code}`; }
  displayCurrency(currency: CurrencyRecord): string { return this.language.language() === 'ar' ? currency.arabicName || currency.englishName || currency.code : currency.englishName || currency.arabicName || currency.code; }
  displayProduct(product: ProductRecord): string { return this.language.language() === 'ar' ? product.arabicName || product.englishName || product.sku : product.englishName || product.arabicName || product.sku; }
  displayUnit(unit: UnitOfMeasureRecord): string { return this.language.language() === 'ar' ? unit.arabicName || unit.englishName || unit.code : unit.englishName || unit.arabicName || unit.code; }
  organizationScopeKey(scope: PurchaseRequestOrganizationScopeResponse): string { return `${scope.companyId}|${scope.branchId ?? ''}`; }
  setOrganizationScope(key: string): void { const [companyId, branchId] = key.split('|'); this.draft = { ...this.draft, companyId: companyId ?? '', branchId: branchId || '' }; }
  organizationScopeLabel(): string { const scope = this.organizationScopes().find(item => item.companyId === this.draft.companyId && item.branchId === (this.draft.branchId || null)); return scope?.displayName ?? this.language.text('organizationScopeUnresolved'); }

  private emptyLine(): LineDraft { return { productId: '', unitOfMeasureId: '', quantity: 1, unitPriceOverride: null, discountPercent: 0, notes: '', taxId: '' }; }
  private draftFromQuotation(quote: SalesQuotationResponse): QuotationDraft { return { companyId: quote.companyId, branchId: quote.branchId ?? '', customerId: quote.customerId, quotationDate: quote.quotationDate, validUntil: quote.validUntil, currencyId: quote.currencyId, priceListId: quote.lines[0]?.priceListId ?? '', paymentTermId: quote.paymentTerm?.id ?? '', exchangeRateId: quote.exchangeRateEvidence?.exchangeRateId ?? '', customerContactId: quote.customerContactId ?? '', notes: quote.notes ?? '', customerReference: quote.customerReference ?? '', lines: quote.lines.map(line => ({ productId: line.productId, unitOfMeasureId: line.unitOfMeasureId, quantity: line.quantity, unitPriceOverride: line.manualPriceApplied ? line.unitPrice : null, discountPercent: line.discountPercent, taxId: line.taxId ?? '', notes: line.notes ?? '' })) }; }
  private draftFromOrder(order: SalesOrderResponse): QuotationDraft { const now = new Date().toISOString().slice(0, 10); return { companyId: order.companyId, branchId: order.branchId ?? '', customerId: order.customerId, quotationDate: now, validUntil: now, currencyId: order.currencyId, priceListId: order.lines[0]?.priceListId ?? '', paymentTermId: order.paymentTerm?.id ?? '', exchangeRateId: order.exchangeRateEvidence?.exchangeRateId ?? '', customerContactId: '', notes: '', customerReference: '', lines: order.lines.map(line => ({ productId: line.productId, unitOfMeasureId: line.unitOfMeasureId, quantity: line.quantity, unitPriceOverride: line.manualPriceApplied ? line.unitPrice : null, discountPercent: line.discountPercent, taxId: line.taxId ?? '', notes: line.notes ?? '' })) }; }
  private emptyDraft(): QuotationDraft { const now = new Date(); const valid = new Date(now); valid.setDate(valid.getDate() + 30); return { companyId: '', branchId: '', customerId: '', quotationDate: now.toISOString().slice(0, 10), validUntil: valid.toISOString().slice(0, 10), currencyId: '', priceListId: '', paymentTermId: '', exchangeRateId: '', customerContactId: '', notes: '', customerReference: '', lines: [this.emptyLine()] }; }
}
