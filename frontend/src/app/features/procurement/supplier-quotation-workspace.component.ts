import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { LanguageService, TranslationKey } from '../../core/i18n/language.service';
import { MasterDataService } from '../master-data/master-data.service';
import { CurrencyRecord, MasterDataRecord, PaymentTermRecord, SupplierRecord, TaxRecord } from '../master-data/master-data.models';
import { PurchaseRequestLineResponse, PurchaseRequestListItemResponse, PurchaseRequestOrganizationScopeResponse, PurchaseRequestResponse } from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';
import {
  SupplierQuotationAuditResponse,
  SupplierQuotationComparisonItemResponse,
  SupplierQuotationComparisonResponse,
  SupplierQuotationEvidenceReferenceWriteRequest,
  SupplierQuotationHistoryResponse,
  SupplierQuotationListItemResponse,
  SupplierQuotationResponse,
  SupplierQuotationStatus,
  SupplierQuotationWriteRequest,
  SupplierSourceDecisionHistoryResponse,
  SupplierSourceDecisionResponse,
} from './supplier-quotation.model';
import { SupplierQuotationService } from './supplier-quotation.service';

type WorkspaceMode = 'list' | 'create' | 'edit' | 'detail';
type DetailTab = 'summary' | 'lines' | 'commercial' | 'evidence' | 'comparison' | 'history' | 'audit' | 'technical';
type LifecycleAction = 'submit' | 'withdraw' | 'disqualify';

interface QuotationListRow extends SupplierQuotationListItemResponse {
  purchaseRequestReference: string;
  organization: string;
  isSelected: boolean;
}

interface QuotationDraftLine {
  purchaseRequestLineId: string;
  productSku: string;
  productName: string;
  unitOfMeasureCode: string;
  requestedQuantity: number;
  requestedNeedByDate: string;
  purpose: string;
  quotedQuantity: number;
  unitPrice: number;
  discountAmount: number | null;
  discountPercentage: number | null;
  taxId: string | null;
  taxReference: string;
  taxRatePercentage: number | null;
  taxAmount: number | null;
  offeredDeliveryDate: string;
  offeredDeliveryLeadTime: string;
  notes: string;
}

interface EvidenceDraft extends SupplierQuotationEvidenceReferenceWriteRequest {
  referenceId: string;
  fileName: string;
  contentType: string;
  description: string;
  source: string;
  externalReference: string;
}

interface QuotationDraft {
  purchaseRequestId: string;
  supplierId: string;
  supplierQuotationReference: string;
  offerDate: string;
  validUntil: string;
  currencyId: string;
  paymentTermId: string;
  deliveryTerms: string;
  offeredDeliveryDate: string;
  offeredDeliveryLeadTime: string;
  notes: string;
  lines: QuotationDraftLine[];
  evidence: EvidenceDraft[];
}

@Component({
  selector: 'app-supplier-quotation-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, FormsModule, RouterLink],
  template: `
    @if (mode() === 'list') {
      <section class="ui-page quotation-page" data-testid="supplier-quotation-list">
        <app-page-header class="page-header">
          <div page-header-copy>
            <p class="eyebrow">{{ language.text('supplierQuotationListKicker') }}</p>
            <h1>{{ language.text('supplierQuotations') }}</h1>
            <p class="lede">{{ language.text('supplierQuotationsLead') }}</p>
          </div>
          <div page-header-actions>
          <a class="button button--primary" routerLink="/app/procurement/supplier-quotations/new" data-testid="new-supplier-quotation">＋ {{ language.text('newSupplierQuotation') }}</a>
        
          </div>
        </app-page-header>ate-card--error" role="alert"><strong>{{ language.text('supplierQuotationListLoadFailed') }}</strong><p>{{ errorText(error) }}</p><button class="button button--secondary" type="button" (click)="loadList()">{{ language.text('retry') }}</button></section>
        } @else {
          <section class="ui-surface ledger-panel">
            <div class="filter-toolbar">
              <label class="filter-search"><span aria-hidden="true">⌕</span><input type="search" [value]="search()" (input)="search.set($any($event.target).value)" [placeholder]="language.text('supplierQuotationSearch')" [attr.aria-describedby]="'quotation-search-hint'" data-testid="quotation-search" /><span class="sr-only">{{ language.text('supplierQuotationSearch') }}</span></label>
              <label class="filter-field"><span>{{ language.text('supplierQuotationStatusFilter') }}</span><select [value]="statusFilter()" (change)="statusFilter.set($any($event.target).value)"><option value="">{{ language.text('supplierQuotationAllStatuses') }}</option><option value="Draft">{{ statusLabel('Draft') }}</option><option value="Submitted">{{ statusLabel('Submitted') }}</option><option value="Withdrawn">{{ statusLabel('Withdrawn') }}</option><option value="Disqualified">{{ statusLabel('Disqualified') }}</option><option value="Superseded">{{ statusLabel('Superseded') }}</option></select></label>
              <label class="filter-field"><span>{{ language.text('supplierQuotationCurrencyFilter') }}</span><select [value]="currencyFilter()" (change)="currencyFilter.set($any($event.target).value)"><option value="">{{ language.text('supplierQuotationAllCurrencies') }}</option>@for (currency of listCurrencies(); track currency) {<option [value]="currency">{{ currency }}</option>}</select></label>
              <p id="quotation-search-hint" class="filter-note">{{ language.text('supplierQuotationFilterNote') }}</p>
            </div>
            @if (filteredRecords().length === 0) {
              <div class="empty-ledger"><span aria-hidden="true">◌</span><h2>{{ language.text('supplierQuotationNoRecords') }}</h2><p>{{ language.text('supplierQuotationNoRecordsLead') }}</p></div>
            } @else {
              <app-data-grid [caption]="language.text('supplierQuotations')" [rows]="filteredRecords()" [columns]="gridColumns()" [language]="language.language()" [pageSize]="filteredRecords().length || 1" [showPager]="false" [rowActions]="gridActions()" [countLabel]="language.text('recordCount')" [rowActionsLabel]="language.text('supplierQuotationActionsColumn')" (rowAction)="onGridAction($event)" />
            }
          </section>
        }
      </section>
    } @else if (mode() === 'create' || mode() === 'edit') {
      <section class="ui-page quotation-page" data-testid="supplier-quotation-form">
        <app-page-header class="page-header"><div page-header-copy><p class="eyebrow">{{ language.text('supplierQuotationListKicker') }}</p><h1>{{ mode() === 'create' ? language.text('newSupplierQuotation') : language.text('supplierQuotationDetail') }}</h1><p class="lede">{{ mode() === 'create' ? language.text('supplierQuotationCreateLead') : language.text('supplierQuotationEditLead') }}</p></div>
          <div page-header-actions><a class="button button--secondary" routerLink="/app/procurement/supplier-quotations">{{ language.text('supplierQuotationBack') }}</a>
          </div>
        </app-page-header>)="reloadForm()">{{ language.text('retry') }}</button></section>
        } @else {
          @if (referenceError()) {<div class="inline-alert" role="alert">{{ language.text('referenceDataUnavailable') }}</div>}
          <div class="form-stack">
            <section class="ui-surface form-section"><div class="section-heading"><div><p class="section-kicker">01 / {{ language.text('supplierQuotationContextSection') }}</p><h2>{{ language.text('supplierQuotationContextSection') }}</h2><p>{{ language.text('supplierQuotationContextLead') }}</p></div></div><div class="field-grid field-grid--context"><label class="field field--wide"><span class="field__label">{{ language.text('supplierQuotationApprovedRequest') }} *</span>@if (mode() === 'create') {<select [value]="draft.purchaseRequestId" (change)="selectPurchaseRequest($any($event.target).value)" data-testid="quotation-request-select"><option value="">{{ language.text('supplierQuotationApprovedRequestHint') }}</option>@for (request of approvedRequests(); track request.id) {<option [value]="request.id">{{ formatReference(request.id, 'PR') }} · {{ request.purpose || language.text('purpose') }}</option>}</select>} @else {<div class="readonly-control"><span class="mono-ref">{{ formatReference(draft.purchaseRequestId, 'PR') }}</span><small>{{ language.text('supplierQuotationReadOnlyHint') }}</small></div>}<small class="field__hint">{{ language.text('supplierQuotationApprovedRequestHint') }}</small></label><div class="field"><span class="field__label">{{ language.text('supplierQuotationOrganizationColumn') }}</span><div class="readonly-control">{{ organizationForRequest(selectedRequest()) }}</div></div><div class="field"><span class="field__label">{{ language.text('supplierQuotationRequestPurpose') }}</span><div class="readonly-control">{{ selectedRequest()?.purpose || '—' }}</div></div></div>@if (selectedRequest(); as request) {<div class="lineage-strip"><strong>{{ language.text('supplierQuotationReadOnlyHint') }}</strong><span>{{ statusLabel(request.status) }}</span><span>•</span><span>{{ request.lines.length }} {{ language.text('requestLines') }}</span><span>•</span><span>{{ formatDate(request.approvedAt || request.updatedAt) }}</span></div>} @else {<div class="empty-inline">{{ language.text('supplierQuotationNoApprovedRequests') }}</div>}</section>
            <section class="ui-surface form-section"><div class="section-heading"><div><p class="section-kicker">02 / {{ language.text('supplierQuotationSupplierField') }}</p><h2>{{ language.text('supplierQuotationSupplierField') }} &amp; commercial terms</h2></div></div><div class="field-grid"><label class="field"><span class="field__label">{{ language.text('supplierQuotationSupplierField') }} *</span><select [value]="draft.supplierId" (change)="draft.supplierId = $any($event.target).value"><option value="">{{ language.text('supplierQuotationSupplierField') }}</option>@for (supplier of suppliers(); track supplier.id) {<option [value]="supplier.id">{{ supplier.code }} · {{ masterName(supplier) }}</option>}</select></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationReferenceColumn') }} *</span><input type="text" [value]="draft.supplierQuotationReference" (input)="draft.supplierQuotationReference = $any($event.target).value" [placeholder]="language.text('supplierQuotationReferencePlaceholder')" data-testid="quotation-reference-input" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationCurrencyField') }} *</span><select [value]="draft.currencyId" (change)="draft.currencyId = $any($event.target).value"><option value="">{{ language.text('supplierQuotationCurrencyField') }}</option>@for (currency of currencies(); track currency.id) {<option [value]="currency.id">{{ currency.code }} · {{ masterName(currency) }}</option>}</select></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationPaymentTermField') }}</span><select [value]="draft.paymentTermId" (change)="draft.paymentTermId = $any($event.target).value"><option value="">{{ language.text('supplierQuotationPaymentTermField') }}</option>@for (term of paymentTerms(); track term.id) {<option [value]="term.id">{{ term.code }} · {{ masterName(term) }} · v{{ term.currentVersionNumber }}</option>}</select></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationOfferDateField') }} *</span><input type="date" [value]="draft.offerDate" (input)="draft.offerDate = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationValidUntilField') }}</span><input type="date" [value]="draft.validUntil" (input)="draft.validUntil = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationDeliveryTermsField') }}</span><input type="text" [value]="draft.deliveryTerms" (input)="draft.deliveryTerms = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationOfferedDeliveryDateField') }}</span><input type="date" [value]="draft.offeredDeliveryDate" (input)="draft.offeredDeliveryDate = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationOfferedLeadTimeField') }}</span><input type="text" [value]="draft.offeredDeliveryLeadTime" (input)="draft.offeredDeliveryLeadTime = $any($event.target).value" /></label><label class="field field--wide"><span class="field__label">{{ language.text('supplierQuotationNotesField') }}</span><textarea [value]="draft.notes" (input)="draft.notes = $any($event.target).value" rows="3"></textarea></label></div></section>
            <section class="ui-surface form-section"><div class="section-heading"><div><p class="section-kicker">03 / {{ language.text('supplierQuotationCommercialLines') }}</p><h2>{{ language.text('supplierQuotationCommercialLines') }}</h2><p>{{ language.text('supplierQuotationCommercialLinesLead') }}</p></div></div>@if (draft.lines.length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationApprovedRequestHint') }}</div>} @else {<div class="line-stack">@for (line of draft.lines; track line.purchaseRequestLineId; let i = $index) {<article class="line-card"><div class="line-identity"><span>{{ i + 1 }}</span><div><strong>{{ line.productSku }} · {{ line.productName }}</strong><small>{{ line.unitOfMeasureCode }} · {{ line.purpose || '—' }}</small></div></div><div class="line-facts"><span>{{ language.text('supplierQuotationRequestedQuantity') }} <b>{{ formatQuantity(line.requestedQuantity) }}</b></span><span>{{ language.text('supplierQuotationRequestNeedBy') }} <b>{{ formatDate(line.requestedNeedByDate) }}</b></span></div><div class="line-inputs"><label class="field"><span class="field__label">{{ language.text('supplierQuotationQuotedQuantity') }} *</span><input type="number" min="0.000001" step="0.001" [value]="line.quotedQuantity" (input)="line.quotedQuantity = numberValue($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationUnitPrice') }} *</span><input type="number" min="0" step="0.01" [value]="line.unitPrice" (input)="line.unitPrice = numberValue($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationDiscountAmount') }}</span><input type="number" min="0" step="0.01" [value]="line.discountAmount ?? ''" (input)="line.discountAmount = nullableNumber($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationDiscountPercentage') }}</span><input type="number" min="0" max="100" step="0.01" [value]="line.discountPercentage ?? ''" (input)="line.discountPercentage = nullableNumber($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationTax') }}</span><select [value]="line.taxId ?? ''" (change)="line.taxId = nullableSelect($event)"><option value="">{{ language.text('supplierQuotationTax') }}</option>@for (tax of taxes(); track tax.id) {<option [value]="tax.id">{{ tax.code }} · {{ masterName(tax) }}</option>}</select></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationTaxRate') }}</span><input type="number" min="0" max="100" step="0.01" [value]="line.taxRatePercentage ?? ''" (input)="line.taxRatePercentage = nullableNumber($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationTaxAmount') }}</span><input type="number" min="0" step="0.01" [value]="line.taxAmount ?? ''" (input)="line.taxAmount = nullableNumber($event)" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationLineDeliveryDate') }}</span><input type="date" [value]="line.offeredDeliveryDate" (input)="line.offeredDeliveryDate = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationLineLeadTime') }}</span><input type="text" [value]="line.offeredDeliveryLeadTime" (input)="line.offeredDeliveryLeadTime = $any($event.target).value" /></label><label class="field field--wide"><span class="field__label">{{ language.text('supplierQuotationLineNotes') }}</span><input type="text" [value]="line.notes" (input)="line.notes = $any($event.target).value" /></label></div></article>}</div>}</section>
            <section class="ui-surface form-section"><div class="section-heading"><div><p class="section-kicker">04 / {{ language.text('supplierQuotationEvidenceSection') }}</p><h2>{{ language.text('supplierQuotationEvidenceSection') }}</h2><p>{{ language.text('supplierQuotationEvidenceLead') }}</p></div><button class="button button--secondary" type="button" (click)="addEvidence()">＋ {{ language.text('supplierQuotationAddEvidence') }}</button></div>@if (draft.evidence.length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationNoHistory') }}</div>} @else {<div class="evidence-stack">@for (evidence of draft.evidence; track $index; let i = $index) {<article class="evidence-card"><div class="evidence-head"><span>{{ i + 1 }}</span><button class="button button--quiet button--danger" type="button" (click)="removeEvidence(i)">{{ language.text('supplierQuotationRemoveEvidence') }}</button></div><div class="field-grid"><label class="field"><span class="field__label">{{ language.text('supplierQuotationEvidenceReference') }} *</span><input type="text" [value]="evidence.referenceId" (input)="evidence.referenceId = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationEvidenceFile') }}</span><input type="text" [value]="evidence.fileName" (input)="evidence.fileName = $any($event.target).value" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationEvidenceType') }}</span><input type="text" [value]="evidence.contentType" (input)="evidence.contentType = $any($event.target).value" placeholder="application/pdf" /></label><label class="field"><span class="field__label">{{ language.text('supplierQuotationEvidenceSource') }}</span><input type="text" [value]="evidence.source" (input)="evidence.source = $any($event.target).value" /></label><label class="field field--wide"><span class="field__label">{{ language.text('supplierQuotationEvidenceDescription') }}</span><input type="text" [value]="evidence.description" (input)="evidence.description = $any($event.target).value" /></label><label class="field field--wide"><span class="field__label">{{ language.text('supplierQuotationEvidenceExternal') }}</span><input type="text" [value]="evidence.externalReference" (input)="evidence.externalReference = $any($event.target).value" /></label></div></article>}</div>}</section>
            @if (formValidationError()) {<div class="inline-alert inline-alert--error" role="alert">{{ language.text('validationError') }}</div>}<div class="form-actions"><a class="button button--secondary" routerLink="/app/procurement/supplier-quotations">{{ language.text('cancel') }}</a><button class="button button--primary" type="button" [disabled]="saving()" (click)="saveDraft()">{{ saving() ? language.text('loading') : language.text('supplierQuotationSave') }}</button></div>
          </div>
        }
      </section>
    } @else {
      @if (detailLoading()) {
        <section class="ui-page quotation-page"><section class="ui-surface state-card"><span class="spinner" aria-hidden="true"></span><h2>{{ language.text('loadingSupplierQuotations') }}</h2></section></section>
      } @else if (detailError(); as error) {
        <section class="ui-page quotation-page"><section class="ui-surface state-card state-card--error" role="alert"><strong>{{ language.text('supplierQuotationDetail') }}</strong><p>{{ errorText(error) }}</p><button class="button button--secondary" type="button" (click)="reloadDetail()">{{ language.text('retry') }}</button></section></section>
      } @else if (detail(); as quotation) {
        <section class="ui-page quotation-page" data-testid="supplier-quotation-detail">
          <app-page-header class="page-header"><div page-header-copy><p class="eyebrow">{{ language.text('supplierQuotationListKicker') }} · {{ formatReference(quotation.id, 'SQ') }}</p><div class="detail-title"><h1>{{ quotation.supplierQuotationReference || language.text('supplierQuotationUntitled') }}</h1><span class="status-badge" [class]="statusClass(quotation.status)"><span aria-hidden="true"></span>{{ statusLabel(quotation.status) }}</span></div><p class="lede">{{ quotation.supplier.name }} · {{ formatReference(quotation.purchaseRequestId, 'PR') }} · {{ quotation.currency.code }}</p></div>
          <div page-header-actions><div class="header-actions"><a class="button button--secondary" routerLink="/app/procurement/supplier-quotations">{{ language.text('supplierQuotationBack') }}</a>@if (quotation.canEdit) {<a class="button button--secondary" [routerLink]="editLink(quotation.id)">{{ language.text('supplierQuotationEdit') }}</a>}@if (quotation.canSubmit) {<button class="button button--primary" type="button" (click)="openAction('submit')">{{ language.text('supplierQuotationSubmit') }}</button>}@if (quotation.canWithdraw) {<button class="button button--secondary" type="button" (click)="openAction('withdraw')">{{ language.text('supplierQuotationWithdraw') }}</button>}@if (quotation.canDisqualify) {<button class="button button--danger" type="button" (click)="openAction('disqualify')">{{ language.text('supplierQuotationDisqualify') }}</button>}</div>
          </div>
        </app-page-header>nDecisionLead') : language.text('supplierQuotationNoCurrentSelection') }}</p></section><section class="ui-surface detail-card"><p class="section-kicker">{{ language.text('supplierQuotationContextSection') }}</p><h2>{{ language.text('supplierQuotationContextSection') }}</h2><div class="fact-grid"><div><span>{{ language.text('supplierQuotationPurchaseRequestColumn') }}</span><strong class="mono-ref">{{ formatReference(quotation.purchaseRequestId, 'PR') }}</strong></div><div><span>{{ language.text('supplierQuotationOrganizationColumn') }}</span><strong>{{ organizationForRequest(selectedRequest()) }}</strong></div><div><span>{{ language.text('supplierQuotationSupplierColumn') }}</span><strong>{{ quotation.supplier.code }} · {{ quotation.supplier.name }}</strong></div><div><span>{{ language.text('supplierQuotationCurrencyColumn') }}</span><strong>{{ quotation.currency.code }} · {{ quotation.currency.name }}</strong></div><div><span>{{ language.text('supplierQuotationOfferDateColumn') }}</span><strong>{{ formatDate(quotation.offerDate) }}</strong></div><div><span>{{ language.text('supplierQuotationValidUntilField') }}</span><strong>{{ quotation.validUntil ? formatDate(quotation.validUntil) : '—' }}</strong></div></div><p class="detail-copy">{{ selectedRequest()?.purpose || '—' }}</p></section><section class="ui-surface detail-card"><p class="section-kicker">{{ language.text('supplierQuotationServerTotal') }}</p><h2 class="hero-number">{{ formatMoney(serverTotal(quotation), quotation.currency.code) }}</h2><div class="fact-grid"><div><span>{{ language.text('supplierQuotationCoverageColumn') }}</span><strong>{{ quotation.lines.length }}/{{ quotation.lines.length }}</strong></div><div><span>{{ language.text('supplierQuotationEvidenceColumn') }}</span><strong>{{ quotation.evidence.length }}</strong></div><div><span>{{ language.text('supplierQuotationCreatedAt') }}</span><strong>{{ formatDateTime(quotation.createdAt) }}</strong></div><div><span>{{ language.text('supplierQuotationUpdatedAt') }}</span><strong>{{ formatDateTime(quotation.updatedAt) }}</strong></div></div></section></section>
          }
          @if (activeTab() === 'lines') {
            <section class="ui-surface detail-card" role="tabpanel"><p class="section-kicker">{{ language.text('supplierQuotationTabsLines') }}</p><h2>{{ language.text('supplierQuotationTabsLines') }}</h2><div class="ui-grid-shell"><table class="ui-grid detail-grid"><thead><tr><th>{{ language.text('supplierQuotationPurchaseRequestColumn') }}</th><th>{{ language.text('supplierQuotationQuotedQuantity') }}</th><th>{{ language.text('supplierQuotationUnitPrice') }}</th><th>{{ language.text('supplierQuotationTax') }}</th><th>{{ language.text('supplierQuotationRequestNeedBy') }}</th><th>{{ language.text('supplierQuotationLineDeliveryDate') }}</th></tr></thead><tbody>@for (line of quotation.lines; track line.id) {<tr><td><strong>{{ line.productSku }} · {{ line.productName }}</strong><small>{{ line.unitOfMeasureCode }} · {{ formatQuantity(line.requestedQuantity) }}</small></td><td class="numeric">{{ formatQuantity(line.quotedQuantity) }}</td><td class="numeric money">{{ formatMoney(line.unitPrice, quotation.currency.code) }}</td><td>{{ line.taxCode || '—' }}<small>{{ line.taxRatePercentage === null ? '—' : formatQuantity(line.taxRatePercentage) + '%' }}</small></td><td class="numeric">{{ formatDate(line.requestedNeedByDate) }}</td><td class="numeric">{{ line.offeredDeliveryDate ? formatDate(line.offeredDeliveryDate) : '—' }}</td></tr>}</tbody></table></div></section>
          }
          @if (activeTab() === 'commercial') {
            <section class="detail-layout" role="tabpanel"><section class="ui-surface detail-card"><p class="section-kicker">{{ language.text('supplierQuotationTabsCommercial') }}</p><h2>{{ language.text('supplierQuotationTabsCommercial') }}</h2><div class="fact-grid"><div><span>{{ language.text('supplierQuotationPaymentTermField') }}</span><strong>{{ quotation.paymentTerm ? quotation.paymentTerm.code + ' · ' + quotation.paymentTerm.name : '—' }}</strong></div><div><span>{{ language.text('supplierQuotationDeliveryTermsField') }}</span><strong>{{ quotation.deliveryTerms || '—' }}</strong></div><div><span>{{ language.text('supplierQuotationOfferedDeliveryDateField') }}</span><strong>{{ quotation.offeredDeliveryDate ? formatDate(quotation.offeredDeliveryDate) : '—' }}</strong></div><div><span>{{ language.text('supplierQuotationOfferedLeadTimeField') }}</span><strong>{{ quotation.offeredDeliveryLeadTime || '—' }}</strong></div></div><p class="detail-copy">{{ quotation.notes || '—' }}</p></section><section class="ui-surface detail-card"><p class="section-kicker">{{ language.text('supplierQuotationServerTotal') }}</p><h2 class="hero-number">{{ formatMoney(serverTotal(quotation), quotation.currency.code) }}</h2><p>{{ language.text('supplierQuotationTechnicalHint') }}</p></section></section>
          }
          @if (activeTab() === 'evidence') {
            <section class="ui-surface detail-card" role="tabpanel"><p class="section-kicker">{{ language.text('supplierQuotationTabsEvidence') }}</p><h2>{{ language.text('supplierQuotationTabsEvidence') }}</h2>@if (quotation.evidence.length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationNoHistory') }}</div>} @else {<div class="evidence-read-list">@for (evidence of quotation.evidence; track evidence.id) {<article><strong>{{ evidence.referenceId }}</strong><small>{{ evidence.fileName || '—' }} · {{ evidence.contentType || '—' }}</small><p>{{ evidence.description || '—' }}</p><span>{{ evidence.source }}</span></article>}</div>}</section>
          }
          @if (activeTab() === 'comparison') {
            <section class="comparison-workspace" role="tabpanel">
              <div class="comparison-heading"><div><p class="section-kicker">{{ language.text('supplierQuotationCompare') }}</p><h2>{{ language.text('supplierQuotationCompare') }}</h2><p>{{ language.text('supplierQuotationDecisionLead') }}</p></div><span class="comparison-seal" aria-hidden="true">◎</span></div>
              @if (comparison(); as comparisonValue) {
                <div class="comparison-meta"><span><b>{{ language.text('supplierQuotationComparisonBasis') }}</b> {{ comparisonValue.comparisonBasis }}</span>@if (comparisonValue.hasMixedCurrencies) {<span class="warning-pill">{{ language.text('supplierQuotationMixedCurrencies') }}</span>}<span>{{ language.text('supplierQuotationNoFx') }}</span></div>
                <div class="comparison-groups">
                  @for (group of comparisonValue.currencyGroups; track group.currencyId) {
                    <section class="comparison-group"><header><div><span class="section-kicker">{{ group.currencyCode }}</span><h3>{{ group.supplierQuotationIds.length }} {{ language.text('supplierQuotations') }}</h3></div><span class="comparison-group__state">{{ group.directlyComparableWithinGroup ? language.text('supplierQuotationDirectlyComparable') : language.text('supplierQuotationNotComparable') }}</span></header><div class="comparison-candidates">
                      @for (item of itemsForGroup(group.supplierQuotationIds, comparisonValue); track item.supplierQuotationId) {
                        <article class="comparison-candidate" [class.is-current]="currentDecisionId() === item.supplierQuotationId"><div class="candidate-heading"><div><strong>{{ item.supplier.name }}</strong><small>{{ item.supplierQuotationReference }} · {{ statusLabel(item.status) }}</small></div>@if (currentDecisionId() === item.supplierQuotationId) {<span class="selection-mark">● {{ language.text('supplierQuotationCurrentBadge') }}</span>}</div><div class="candidate-metrics"><span><small>{{ language.text('supplierQuotationServerTotal') }}</small><b>{{ formatMoney(item.commercialTotal, item.currency.code) }}</b></span><span><small>{{ language.text('supplierQuotationCoverageColumn') }}</small><b>{{ item.coveredLineCount }}/{{ item.requestedLineCount }}</b></span><span><small>{{ language.text('supplierQuotationEvidenceColumn') }}</small><b>{{ item.hasEvidence ? '✓' : '—' }}</b></span></div><div class="qualification"><strong>{{ language.text('supplierQuotationQualification') }}</strong>@if (item.qualificationIssues.length === 0) {<span>{{ language.text('supplierQuotationNoQualificationIssues') }}</span>} @else {<ul>@for (issue of item.qualificationIssues; track issue) {<li>{{ issue }}</li>}</ul>}</div><details><summary>{{ language.text('supplierQuotationTabsLines') }}</summary><div class="ui-grid-shell"><table class="ui-grid compact-grid"><thead><tr><th>{{ language.text('supplierQuotationPurchaseRequestColumn') }}</th><th>{{ language.text('supplierQuotationQuotedQuantity') }}</th><th>{{ language.text('supplierQuotationUnitPrice') }}</th></tr></thead><tbody>@for (line of item.lines; track line.purchaseRequestLineId) {<tr><td>{{ line.productSku }} · {{ line.productName }}</td><td class="numeric">{{ line.quotedQuantity === null ? '—' : formatQuantity(line.quotedQuantity) }}</td><td class="numeric">{{ line.unitPrice === null ? '—' : formatMoney(line.unitPrice, item.currency.code) }}</td></tr>}</tbody></table></div></details></article>
                      }
                    </div></section>
                  }
                </div>
                <section class="decision-rail"><div><p class="section-kicker">{{ language.text('supplierQuotationSourceSelection') }}</p><h3>{{ language.text('supplierQuotationCurrentSelection') }}</h3><p>{{ language.text('supplierQuotationDecisionLead') }}</p></div><div class="decision-current">@if (currentDecision(); as decision) {<strong>{{ decision.supplier.name }} · {{ decision.supplierQuotationReference }}</strong><small>{{ language.text('supplierQuotationSelectedAt') }} {{ formatDateTime(decision.selectedAt) }}</small><p>{{ decision.rationale }}</p>} @else {<span>{{ language.text('supplierQuotationNoCurrentSelection') }}</span>}</div><div class="decision-form"><fieldset><legend>{{ language.text('supplierQuotationSourceCandidate') }}</legend>@for (item of comparisonValue.quotations; track item.supplierQuotationId) {@if (item.status === 'Submitted') {<label class="decision-option"><input type="radio" name="source-quotation" [checked]="selectedDecisionId() === item.supplierQuotationId" (change)="selectDecisionCandidate(item.supplierQuotationId)" /><span><strong>{{ item.supplier.name }}</strong><small>{{ item.supplierQuotationReference }} · {{ item.currency.code }}</small></span></label>}}</fieldset><label class="field"><span class="field__label">{{ language.text('supplierQuotationRationale') }} *</span><textarea [value]="decisionRationale" (input)="decisionRationale = $any($event.target).value" rows="4" [placeholder]="language.text('supplierQuotationRationaleHint')"></textarea></label>@if (decisionValidationError()) {<div class="field-error" role="alert">{{ language.text('supplierQuotationDecisionRequired') }}</div>}<button class="button button--primary" type="button" [disabled]="savingDecision()" (click)="recordDecision()">{{ savingDecision() ? language.text('loading') : language.text('supplierQuotationRecordDecision') }}</button></div><div class="decision-history"><p class="section-kicker">{{ language.text('supplierQuotationDecisionHistory') }}</p><h3>{{ language.text('supplierQuotationDecisionHistory') }}</h3>@if (sourceDecisionHistory().length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationNoHistory') }}</div>} @else {<ol>@for (entry of sourceDecisionHistory(); track entry.id) {<li [class.is-current]="entry.selectedQuotationId === currentDecisionId()"><strong>{{ quotationLabel(entry.selectedQuotationId, comparisonValue) }}</strong><small>{{ entry.previousSelectedQuotationId ? language.text('supplierQuotationPreviousSelection') : '' }}</small><time>{{ formatDateTime(entry.selectedAt) }}</time><p>{{ entry.rationale }}</p><code>{{ entry.comparisonSnapshotReference }}</code></li>}</ol>}</div></section>
              } @else {
                <section class="ui-surface state-card state-card--error"><strong>{{ language.text('supplierQuotationCompare') }}</strong><p>{{ comparisonError() ? errorText(comparisonError()!) : language.text('requestError') }}</p></section>
              }
            </section>
          }
          @if (activeTab() === 'history') {
            <section class="ui-surface detail-card" role="tabpanel"><p class="section-kicker">{{ language.text('supplierQuotationTabsHistory') }}</p><h2>{{ language.text('supplierQuotationTabsHistory') }}</h2>@if (historyEntries().length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationNoHistory') }}</div>} @else {<ol class="timeline">@for (entry of historyEntries(); track entry.evidenceId) {<li><strong>{{ statusLabel(entry.fromStatus) }} → {{ statusLabel(entry.toStatus) }}</strong><small>{{ entry.action }} · {{ formatDateTime(entry.occurredAt) }}</small><p>{{ entry.reason || '—' }}</p></li>}</ol>}</section>
          }
          @if (activeTab() === 'audit') {
            <section class="ui-surface detail-card" role="tabpanel"><p class="section-kicker">{{ language.text('supplierQuotationTabsAudit') }}</p><h2>{{ language.text('supplierQuotationTabsAudit') }}</h2>@if (auditEntries().length === 0) {<div class="empty-inline">{{ language.text('supplierQuotationNoAudit') }}</div>} @else {<div class="audit-list">@for (entry of auditEntries(); track entry.evidenceId) {<article><strong>{{ entry.operationId }}</strong><small>{{ entry.decision }} · {{ formatDateTime(entry.occurredAt) }}</small><p>{{ entry.reason || entry.afterSummary || '—' }}</p><code>{{ entry.correlationId }}</code></article>}</div>}</section>
          }
          @if (activeTab() === 'technical') {
            <section class="ui-surface detail-card" role="tabpanel"><p class="section-kicker">{{ language.text('supplierQuotationTabsTechnical') }}</p><h2>{{ language.text('supplierQuotationTabsTechnical') }}</h2><p>{{ language.text('supplierQuotationTechnicalHint') }}</p><dl class="technical-list"><div><dt>{{ language.text('technicalReference') }}</dt><dd>{{ quotation.id }}</dd></div><div><dt>{{ language.text('tenantWorkspace') }}</dt><dd>{{ quotation.tenantId }}</dd></div><div><dt>{{ language.text('supplierQuotationPurchaseRequestColumn') }}</dt><dd>{{ quotation.purchaseRequestId }}</dd></div><div><dt>ETag / version</dt><dd>{{ quotation.version }}</dd></div></dl></section>
          }
        </section>
      }
    }

    @if (dialogAction(); as action) {
      <div class="dialog-backdrop" role="presentation" (click)="closeAction()"><section class="action-dialog" role="dialog" aria-modal="true" aria-labelledby="quotation-action-title" (click)="$event.stopPropagation()"><p class="section-kicker">{{ language.text('supplierQuotationListKicker') }}</p><h2 id="quotation-action-title">{{ actionTitle(action) }}</h2><p>{{ actionLead(action) }}</p>@if (action !== 'submit') {<label class="field"><span class="field__label">{{ language.text('supplierQuotationReason') }} @if (action === 'disqualify') { * }</span><textarea [value]="actionReason" (input)="actionReason = $any($event.target).value" rows="4" [placeholder]="language.text('supplierQuotationReasonHint')"></textarea></label>}@if (actionError()) {<div class="field-error" role="alert">{{ language.text('validationError') }}</div>}<div class="dialog-actions"><button class="button button--secondary" type="button" (click)="closeAction()">{{ language.text('cancel') }}</button><button class="button" [class.button--danger]="action === 'disqualify'" [class.button--primary]="action === 'submit'" type="button" [disabled]="savingAction()" (click)="confirmAction()">{{ savingAction() ? language.text('loading') : actionLabel(action) }}</button></div></section></div>
    }
  `,
  styles: `
    :host { display: block; }
    .quotation-page { --quotation-soft: color-mix(in srgb, var(--accent-soft) 70%, var(--surface-raised)); }
    
    
    .button { display: inline-flex; align-items: center; justify-content: center; gap: .4rem; min-height: 2.4rem; border: 1px solid transparent; border-radius: var(--radius-sm); padding: .52rem .82rem; color: var(--ink); background: var(--surface-raised); font-size: .74rem; font-weight: 800; text-decoration: none; cursor: pointer; }
    .button:hover:not(:disabled) { transform: translateY(-1px); }
    .button:disabled { cursor: wait; opacity: .55; }
    .button--primary { border-color: var(--accent-strong); color: var(--ink-strong); background: var(--accent); }
    .button--secondary { border-color: var(--line-strong); }
    .button--quiet { min-height: 1.9rem; border-color: transparent; padding: .3rem .45rem; color: var(--accent-strong); background: transparent; }
    .button--danger { border-color: color-mix(in srgb, var(--danger) 45%, var(--line)); color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .boundary-note { display: flex; align-items: flex-start; gap: .6rem; border-inline-start: 3px solid var(--support); padding: .72rem .9rem; color: var(--ink-muted); background: var(--support-soft); font-size: .76rem; line-height: 1.5; }
    .boundary-note > span:first-child { color: var(--support); font-size: 1rem; }
    .state-card { display: grid; justify-items: start; align-content: center; gap: .55rem; min-height: 12rem; }
    .state-card h2, .state-card p { margin: 0; }
    .state-card p { color: var(--ink-muted); font-size: .8rem; }
    .state-card--error { border-color: color-mix(in srgb, var(--danger) 32%, var(--line)); }
    .spinner { width: 2rem; height: 2rem; border: 3px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: quotation-spin 1s linear infinite; }
    @keyframes quotation-spin { to { transform: rotate(360deg); } }
    .ledger-panel { padding: 0; overflow: hidden; }
    .filter-toolbar { display: flex; align-items: end; flex-wrap: wrap; gap: .7rem; padding: .85rem 1rem; border-bottom: 1px solid var(--line); background: var(--quotation-soft); }
    .filter-search { display: flex; align-items: center; gap: .4rem; min-width: min(100%, 18rem); flex: 1 1 16rem; border: 1px solid var(--line-strong); border-radius: var(--radius-sm); padding-inline: .6rem; background: var(--surface-raised); color: var(--ink-muted); }
    .filter-search input { width: 100%; min-height: 2.25rem; border: 0; outline: 0; color: var(--ink); background: transparent; font-size: .76rem; }
    .filter-field { display: grid; gap: .25rem; min-width: 9rem; color: var(--ink-muted); font-size: .64rem; font-weight: 900; letter-spacing: .06em; text-transform: uppercase; }
    .filter-field select { min-height: 2.4rem; border: 1px solid var(--line-strong); border-radius: var(--radius-sm); padding: .4rem .5rem; color: var(--ink); background: var(--surface-raised); font-size: .75rem; text-transform: none; letter-spacing: normal; }
    .filter-note { flex: 1 1 100%; margin: 0; color: var(--ink-muted); font-size: .66rem; line-height: 1.4; }
    .quotation-grid-shell { border: 0; border-radius: 0; }
    .quotation-grid { min-width: 70rem; }
    .quotation-grid th, .quotation-grid td { padding: .68rem .6rem; }
    .quotation-grid td small, .detail-grid td small { display: block; margin-top: .16rem; color: var(--ink-muted); font-size: .66rem; }
    .record-link { color: var(--ink); font-weight: 900; text-decoration: none; }
    .record-link:hover { color: var(--accent-strong); text-decoration: underline; }
    .mono-ref, code, .technical-list dd { font-family: var(--font-mono); font-size: .66rem; letter-spacing: .02em; }
    .currency-badge { display: inline-flex; border: 1px solid color-mix(in srgb, var(--accent-strong) 28%, var(--line)); border-radius: 99px; padding: .24rem .45rem; color: var(--accent-strong); background: var(--accent-soft); font-size: .63rem; font-weight: 900; }
    .numeric { font-variant-numeric: tabular-nums; }
    .money { color: var(--ink-strong); font-weight: 800; }
    .centered { text-align: center !important; }
    .status-badge { display: inline-flex; align-items: center; gap: .35rem; border: 1px solid var(--line); border-radius: 99px; padding: .28rem .5rem; color: var(--ink-muted); background: var(--surface); font-size: .63rem; font-weight: 900; white-space: nowrap; }
    .status-badge > span { width: .4rem; height: .4rem; border-radius: 50%; background: currentColor; }
    .status-badge--draft { color: var(--support); background: var(--support-soft); }
    .status-badge--submitted { color: var(--success); background: var(--accent-soft); }
    .status-badge--withdrawn, .status-badge--disqualified, .status-badge--superseded { background: var(--surface-tint); }
    .row-actions { display: flex; flex-wrap: wrap; gap: .12rem; min-width: 7.7rem; }
    .empty-ledger { display: grid; justify-items: start; align-content: center; gap: .45rem; min-height: 17rem; padding: 2rem; }
    .empty-ledger > span { color: var(--accent-strong); font-size: 2.3rem; }
    .empty-ledger h2, .empty-ledger p { margin: 0; }
    .empty-ledger p { max-width: 34rem; color: var(--ink-muted); font-size: .8rem; line-height: 1.5; }
    .form-stack { display: grid; gap: 1rem; }
    .form-section { display: grid; gap: 1.1rem; }
    .section-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .section-heading h2, .section-heading p { margin: 0; }
    .section-heading h2 { font: 800 1.05rem/1.15 var(--font-display); }
    .section-heading p:not(.section-kicker) { margin-top: .35rem; color: var(--ink-muted); font-size: .73rem; line-height: 1.5; }
    .section-kicker { margin: 0 0 .28rem; color: var(--accent-strong); font-size: .62rem; font-weight: 900; letter-spacing: .12em; text-transform: uppercase; }
    .field-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .75rem; }
    .field-grid--context { grid-template-columns: minmax(0, 2fr) repeat(2, minmax(0, 1fr)); }
    .field { display: grid; align-content: start; gap: .3rem; min-width: 0; }
    .field--wide { grid-column: 1 / -1; }
    .field__label { color: var(--ink-muted); font-size: .7rem; font-weight: 900; }
    .field__hint { color: var(--ink-muted); font-size: .66rem; }
    .field input, .field select, .field textarea { width: 100%; border: 1px solid var(--line-strong); border-radius: var(--radius-sm); padding: .58rem .62rem; color: var(--ink); background: var(--surface); font-size: .76rem; }
    .field textarea { min-height: 4.6rem; resize: vertical; line-height: 1.45; }
    .readonly-control { display: flex; align-items: center; justify-content: space-between; gap: .5rem; min-height: 2.45rem; border: 1px dashed var(--line-strong); border-radius: var(--radius-sm); padding: .5rem .6rem; background: var(--surface-tint); font-size: .74rem; }
    .readonly-control small { color: var(--ink-muted); font-size: .62rem; text-align: end; }
    .lineage-strip { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; border-top: 1px solid var(--line); padding-top: .75rem; color: var(--ink-muted); font-size: .7rem; }
    .lineage-strip strong { color: var(--accent-strong); }
    .empty-inline { border: 1px dashed var(--line-strong); padding: .9rem; color: var(--ink-muted); background: var(--surface-tint); font-size: .75rem; }
    .line-stack, .evidence-stack { display: grid; gap: .7rem; }
    .line-card, .evidence-card { display: grid; gap: .75rem; border: 1px solid var(--line); border-radius: var(--radius-md); padding: .9rem; background: var(--surface); }
    .line-identity { display: flex; align-items: flex-start; gap: .65rem; }
    .line-identity > span, .evidence-head > span { display: grid; place-items: center; flex: none; width: 1.8rem; height: 1.8rem; border-radius: .4rem; color: var(--ink); background: var(--accent); font: 800 .68rem var(--font-mono); }
    .line-identity strong, .line-identity small { display: block; }
    .line-identity small { margin-top: .2rem; color: var(--ink-muted); font-size: .67rem; }
    .line-facts { display: flex; flex-wrap: wrap; gap: .65rem 1.5rem; border-block: 1px solid var(--line); padding-block: .5rem; color: var(--ink-muted); font-size: .69rem; }
    .line-facts b { color: var(--ink); }
    .line-inputs { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .65rem; }
    .evidence-head { display: flex; align-items: center; justify-content: space-between; }
    .inline-alert { display: flex; align-items: center; flex-wrap: wrap; gap: .65rem; border: 1px solid color-mix(in srgb, var(--support) 32%, var(--line)); padding: .72rem .85rem; color: var(--support); background: var(--support-soft); font-size: .76rem; }
    .inline-alert--error { border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .inline-alert--success { border-color: color-mix(in srgb, var(--success) 35%, var(--line)); color: var(--success); background: var(--accent-soft); }
    .form-actions, .header-actions, .dialog-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: .55rem; }
    .detail-title { display: flex; align-items: center; flex-wrap: wrap; gap: .7rem; }
    .detail-title h1 { margin: 0; }
    .detail-layout { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
    .detail-card { min-width: 0; }
    .detail-card--accent { border-color: color-mix(in srgb, var(--accent-strong) 32%, var(--line)); background: linear-gradient(135deg, var(--accent-soft), var(--surface-raised) 72%); }
    .detail-card h2 { margin: 0 0 .7rem; font: 800 1.1rem/1.15 var(--font-display); }
    .detail-card--accent h2 { font-size: 1.2rem; }
    .detail-card p:not(.section-kicker) { color: var(--ink-muted); font-size: .76rem; line-height: 1.5; }
    .fact-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .7rem; }
    .fact-grid > div { display: grid; gap: .2rem; min-width: 0; }
    .fact-grid span { color: var(--ink-muted); font-size: .64rem; font-weight: 800; }
    .fact-grid strong { overflow-wrap: anywhere; font-size: .76rem; }
    .detail-copy { margin: .9rem 0 0; white-space: pre-wrap; }
    .hero-number { color: var(--ink-strong); font: 800 clamp(1.7rem, 4vw, 2.4rem)/1 var(--font-display) !important; letter-spacing: -.06em; }
    .detail-tabs { display: flex; gap: .12rem; overflow-x: auto; border-bottom: 1px solid var(--line); }
    .detail-tabs button { border: 0; border-bottom: 2px solid transparent; padding: .75rem .65rem; color: var(--ink-muted); background: transparent; font-size: .7rem; font-weight: 900; white-space: nowrap; cursor: pointer; }
    .detail-tabs button.is-active { border-bottom-color: var(--accent-strong); color: var(--ink); }
    .evidence-read-list, .audit-list { display: grid; gap: .5rem; }
    .evidence-read-list article, .audit-list article { display: grid; grid-template-columns: minmax(10rem, 1fr) minmax(10rem, 2fr) minmax(7rem, .7fr); gap: .8rem; align-items: center; border-bottom: 1px solid var(--line); padding: .65rem 0; }
    .evidence-read-list article:last-child, .audit-list article:last-child { border-bottom: 0; }
    .evidence-read-list small, .audit-list small { display: block; margin-top: .2rem; color: var(--ink-muted); font-size: .65rem; }
    .evidence-read-list p, .audit-list p { margin: 0; color: var(--ink-muted); font-size: .72rem; }
    .comparison-workspace { display: grid; gap: .9rem; }
    .comparison-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .comparison-heading h2 { margin: 0; font: 800 1.3rem/1.1 var(--font-display); }
    .comparison-heading p:not(.section-kicker) { max-width: 48rem; margin: .4rem 0 0; color: var(--ink-muted); font-size: .76rem; line-height: 1.5; }
    .comparison-seal { display: grid; place-items: center; flex: none; width: 2.6rem; height: 2.6rem; border: 1px solid var(--line-strong); border-radius: .7rem; color: var(--accent-strong); font-size: 1.4rem; }
    .comparison-meta { display: flex; flex-wrap: wrap; gap: .5rem 1rem; border-inline-start: 3px solid var(--support); padding: .68rem .85rem; color: var(--ink-muted); background: var(--support-soft); font-size: .7rem; line-height: 1.4; }
    .comparison-meta b { color: var(--ink); }
    .warning-pill { border: 1px solid color-mix(in srgb, var(--support) 45%, var(--line)); border-radius: 99px; padding: .23rem .45rem; color: var(--support); font-weight: 900; }
    .comparison-groups { display: grid; gap: .8rem; }
    .comparison-group { border: 1px solid var(--line); border-radius: var(--radius-md); overflow: hidden; background: var(--surface-raised); }
    .comparison-group > header { display: flex; align-items: center; justify-content: space-between; gap: .8rem; border-bottom: 1px solid var(--line); padding: .75rem .9rem; background: var(--grid-header); }
    .comparison-group h3 { margin: 0; font: 800 .95rem/1.15 var(--font-display); }
    .comparison-group__state { color: var(--accent-strong); font-size: .65rem; font-weight: 900; text-align: end; }
    .comparison-candidates { display: grid; grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr)); gap: .7rem; padding: .7rem; }
    .comparison-candidate { display: grid; align-content: start; gap: .7rem; border: 1px solid var(--line); border-radius: var(--radius-sm); padding: .8rem; background: var(--surface); }
    .comparison-candidate.is-current { border-color: var(--accent-strong); box-shadow: inset 3px 0 var(--accent-strong); }
    .candidate-heading, .candidate-metrics { display: flex; justify-content: space-between; gap: .6rem; }
    .candidate-heading strong, .candidate-heading small { display: block; }
    .candidate-heading small { margin-top: .2rem; color: var(--ink-muted); font-size: .64rem; }
    .candidate-metrics { border-block: 1px solid var(--line); padding-block: .55rem; }
    .candidate-metrics span { display: grid; gap: .2rem; }
    .candidate-metrics small { color: var(--ink-muted); font-size: .6rem; }
    .candidate-metrics b { font-size: .7rem; }
    .qualification { display: grid; gap: .3rem; color: var(--ink-muted); font-size: .68rem; line-height: 1.4; }
    .qualification strong { color: var(--ink); font-size: .63rem; letter-spacing: .06em; text-transform: uppercase; }
    .qualification ul { margin: 0; padding-inline-start: 1rem; }
    details summary { color: var(--accent-strong); cursor: pointer; font-size: .68rem; font-weight: 900; }
    .compact-grid { font-size: .68rem; }
    .compact-grid th, .compact-grid td { padding: .45rem; }
    .decision-rail { display: grid; grid-template-columns: minmax(14rem, 1fr) minmax(12rem, 1fr) minmax(18rem, 1.3fr); gap: .9rem; border: 1px solid color-mix(in srgb, var(--accent-strong) 35%, var(--line)); border-radius: var(--radius-md); padding: .9rem; background: linear-gradient(130deg, var(--accent-soft), var(--surface-raised) 66%); }
    .decision-rail h3, .decision-rail p { margin: 0; }
    .decision-rail h3 { font: 800 .95rem/1.2 var(--font-display); }
    .decision-rail p:not(.section-kicker) { margin-top: .35rem; color: var(--ink-muted); font-size: .7rem; line-height: 1.45; }
    .decision-current { display: grid; align-content: start; gap: .3rem; border-inline-start: 1px solid var(--line-strong); padding-inline-start: .9rem; font-size: .73rem; }
    .decision-current small { color: var(--ink-muted); font-size: .64rem; }
    .decision-current p { padding: .5rem; background: color-mix(in srgb, var(--surface-raised) 70%, transparent); white-space: pre-wrap; }
    .decision-form { display: grid; gap: .55rem; }
    .decision-form fieldset { display: grid; gap: .35rem; border: 0; margin: 0; padding: 0; }
    .decision-form legend { margin-bottom: .2rem; color: var(--ink-muted); font-size: .63rem; font-weight: 900; text-transform: uppercase; }
    .decision-option { display: flex; align-items: center; gap: .5rem; border: 1px solid var(--line); border-radius: .4rem; padding: .4rem .5rem; background: var(--surface-raised); cursor: pointer; }
    .decision-option:has(input:checked) { border-color: var(--accent-strong); background: var(--accent-soft); }
    .decision-option small { display: block; margin-top: .15rem; color: var(--ink-muted); font-size: .62rem; }
    .decision-history { grid-column: 1 / -1; border-top: 1px solid var(--line); padding-top: .8rem; }
    .decision-history h3 { margin-bottom: .5rem; }
    .decision-history ol { display: grid; gap: .45rem; margin: 0; padding: 0; list-style: none; }
    .decision-history li { display: grid; grid-template-columns: 1fr auto; gap: .3rem .8rem; border-inline-start: 3px solid var(--line-strong); padding: .5rem .7rem; background: color-mix(in srgb, var(--surface-raised) 70%, transparent); }
    .decision-history li.is-current { border-inline-start-color: var(--accent-strong); }
    .decision-history li small, .decision-history li time { display: block; color: var(--ink-muted); font-size: .63rem; }
    .decision-history li p, .decision-history li code { grid-column: 1 / -1; margin: 0; overflow-wrap: anywhere; }
    .timeline { display: grid; gap: .55rem; margin: 0; padding: 0; list-style: none; }
    .timeline li { display: grid; gap: .2rem; border-inline-start: 3px solid var(--line-strong); padding: .55rem .7rem; }
    .timeline small { color: var(--ink-muted); font-size: .65rem; }
    .timeline p { margin: 0; }
    .technical-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 1rem; margin: 0; border-top: 1px solid var(--line); }
    .technical-list div { display: grid; gap: .3rem; border-bottom: 1px solid var(--line); padding: .65rem 0; }
    .technical-list dt { color: var(--ink-muted); font-size: .63rem; font-weight: 900; text-transform: uppercase; }
    .technical-list dd { margin: 0; overflow-wrap: anywhere; }
    .dialog-backdrop { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 1rem; background: rgb(16 39 37 / 45%); }
    .action-dialog { width: min(100%, 31rem); display: grid; gap: .7rem; border: 1px solid var(--line); border-radius: var(--radius-lg); padding: 1.2rem; background: var(--surface-raised); box-shadow: var(--shadow-card); }
    .action-dialog h2, .action-dialog p { margin: 0; }
    .action-dialog h2 { font: 800 1.2rem/1.15 var(--font-display); }
    .action-dialog > p:not(.section-kicker) { color: var(--ink-muted); font-size: .76rem; line-height: 1.5; }
    .field-error { color: var(--danger); font-size: .7rem; font-weight: 800; }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); white-space: nowrap; }
    @media (max-width: 980px) { .field-grid, .field-grid--context { grid-template-columns: repeat(2, minmax(0, 1fr)); } .line-inputs { grid-template-columns: repeat(3, minmax(0, 1fr)); } .detail-layout, .decision-rail { grid-template-columns: 1fr; } .decision-current { border-inline-start: 0; border-block-start: 1px solid var(--line-strong); padding-block-start: .7rem; padding-inline-start: 0; } .decision-history { grid-column: auto; } }
    @media (max-width: 620px) { .section-heading { align-items: flex-start; flex-direction: column; } .field-grid, .field-grid--context, .line-inputs, .fact-grid, .technical-list { grid-template-columns: 1fr; } .field--wide { grid-column: auto; } .form-actions, .header-actions { justify-content: stretch; } .form-actions .button, .header-actions .button { flex: 1 1 100%; } .evidence-read-list article, .audit-list article { grid-template-columns: 1fr; gap: .35rem; } .candidate-metrics { flex-wrap: wrap; } .candidate-metrics span { flex: 1 1 40%; } .decision-history li { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { .button, .spinner { animation: none; transition: none; } }
  `,
})
export class SupplierQuotationWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly quotations = inject(SupplierQuotationService);
  private readonly purchaseRequests = inject(PurchaseRequestService);
  private readonly masterData = inject(MasterDataService);

  readonly mode = signal<WorkspaceMode>('list');
  readonly loading = signal(false);
  readonly listError = signal<SafeUiError | null>(null);
  readonly records = signal<QuotationListRow[]>([]);
  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly currencyFilter = signal('');
  readonly filteredRecords = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return this.records().filter((row) => (!this.statusFilter() || row.status === this.statusFilter()) && (!this.currencyFilter() || row.currency.code === this.currencyFilter()) && (!query || [row.supplierQuotationReference, row.supplier.code, row.supplier.name, row.purchaseRequestReference, row.organization].some((value) => value.toLocaleLowerCase().includes(query))));
  });
  readonly listCurrencies = computed(() => [...new Set(this.records().map((row) => row.currency.code))].sort());

  gridColumns(): DataGridColumn<QuotationListRow>[] { return [
    { key: 'reference', label: this.language.text('supplierQuotationReferenceColumn'), value: (row) => row.supplierQuotationReference || this.language.text('supplierQuotationUntitled'), secondaryText: (row) => this.formatReference(row.id, 'SQ'), link: (row) => this.detailLink(row.id).join('/'), filter: 'text' },
    { key: 'supplier', label: this.language.text('supplierQuotationSupplierColumn'), value: (row) => row.supplier.code, secondaryText: (row) => row.supplier.name, filter: 'text' },
    { key: 'request', label: this.language.text('supplierQuotationPurchaseRequestColumn'), value: (row) => row.purchaseRequestReference, filter: 'text' },
    { key: 'organization', label: this.language.text('supplierQuotationOrganizationColumn'), value: (row) => row.organization, filter: 'text' },
    { key: 'currency', label: this.language.text('supplierQuotationCurrencyColumn'), value: (row) => row.currency.code, filter: 'text' },
    { key: 'amount', label: this.language.text('supplierQuotationAmountColumn'), value: (row) => row.commercialTotal, display: (row) => this.formatMoney(row.commercialTotal, row.currency.code), filter: 'number-range', align: 'end' },
    { key: 'coverage', label: this.language.text('supplierQuotationCoverageColumn'), value: (row) => `${row.coveredLineCount}/${row.requestedLineCount}`, filter: 'text', align: 'end' },
    { key: 'status', label: this.language.text('supplierQuotationStatusColumn'), value: (row) => row.status, display: (row) => this.statusLabel(row.status), badge: true, filter: 'select', filterOptions: ['Draft', 'Submitted', 'Withdrawn', 'Disqualified', 'Superseded'].map((status) => ({ value: status, label: this.statusLabel(status as SupplierQuotationStatus) })) },
    { key: 'offerDate', label: this.language.text('supplierQuotationOfferDateColumn'), value: (row) => row.offerDate, display: (row) => this.formatDate(row.offerDate), filter: 'date-range' },
    { key: 'evidence', label: this.language.text('supplierQuotationEvidenceColumn'), value: (row) => row.hasEvidence ? 'Yes' : 'No', filter: 'select', filterOptions: [{ value: 'Yes', label: this.language.language() === 'ar' ? 'نعم' : 'Yes' }, { value: 'No', label: this.language.language() === 'ar' ? 'لا' : 'No' }] },
    { key: 'selected', label: this.language.text('supplierQuotationSelectedColumn'), value: (row) => row.isSelected ? 'Selected' : 'Not selected', filter: 'select', filterOptions: [{ value: 'Selected', label: this.language.text('supplierQuotationCurrentBadge') }, { value: 'Not selected', label: this.language.language() === 'ar' ? 'غير محدد' : 'Not selected' }] },
  ]; }

  gridActions(): readonly { key: string; label: string; visible?: (row: QuotationListRow) => boolean }[] { return [
    { key: 'view', label: this.language.text('supplierQuotationDetail') },
    { key: 'edit', label: this.language.text('supplierQuotationEdit'), visible: (row) => row.status === 'Draft' },
  ]; }
  onGridAction(event: { action: string; row: QuotationListRow }): void {
    void this.router.navigate(event.action === 'edit' ? this.editLink(event.row.id) : this.detailLink(event.row.id));
  }

  readonly formLoading = signal(false);
  readonly formErrorState = signal<SafeUiError | null>(null);
  readonly formValidationError = signal(false);
  readonly referenceError = signal(false);
  readonly saving = signal(false);
  readonly approvedRequests = signal<PurchaseRequestListItemResponse[]>([]);
  readonly organizationScopes = signal<PurchaseRequestOrganizationScopeResponse[]>([]);
  readonly selectedRequest = signal<PurchaseRequestResponse | null>(null);
  readonly suppliers = signal<SupplierRecord[]>([]);
  readonly currencies = signal<CurrencyRecord[]>([]);
  readonly paymentTerms = signal<PaymentTermRecord[]>([]);
  readonly taxes = signal<TaxRecord[]>([]);

  readonly detailLoading = signal(false);
  readonly detailError = signal<SafeUiError | null>(null);
  readonly detail = signal<SupplierQuotationResponse | null>(null);
  readonly mutationError = signal<SafeUiError | null>(null);
  readonly successNotice = signal<string | null>(null);
  readonly activeTab = signal<DetailTab>('summary');
  readonly comparison = signal<SupplierQuotationComparisonResponse | null>(null);
  readonly comparisonError = signal<SafeUiError | null>(null);
  readonly historyEntries = signal<SupplierQuotationHistoryResponse[]>([]);
  readonly auditEntries = signal<SupplierQuotationAuditResponse[]>([]);
  readonly currentDecision = signal<SupplierSourceDecisionResponse | null>(null);
  readonly sourceDecisionHistory = signal<SupplierSourceDecisionHistoryResponse[]>([]);
  readonly selectedDecisionId = signal('');
  readonly savingDecision = signal(false);
  readonly decisionValidationError = signal(false);
  readonly dialogAction = signal<LifecycleAction | null>(null);
  readonly savingAction = signal(false);
  readonly actionError = signal(false);

  draft: QuotationDraft = this.emptyDraft();
  decisionRationale = '';
  private actionReasonValue = '';
  private routeSequence = 0;
  private requestSequence = 0;

  readonly detailTabs: Array<{ key: DetailTab; label: TranslationKey }> = [
    { key: 'summary', label: 'supplierQuotationTabsSummary' }, { key: 'lines', label: 'supplierQuotationTabsLines' }, { key: 'commercial', label: 'supplierQuotationTabsCommercial' }, { key: 'evidence', label: 'supplierQuotationTabsEvidence' }, { key: 'comparison', label: 'supplierQuotationTabsComparison' }, { key: 'history', label: 'supplierQuotationTabsHistory' }, { key: 'audit', label: 'supplierQuotationTabsAudit' }, { key: 'technical', label: 'supplierQuotationTabsTechnical' },
  ];

  ngOnInit(): void { this.route.url.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.applyRoute()); }

  applyRoute(): void {
    const segments = this.route.snapshot.url.map((segment) => segment.path);
    const id = this.route.snapshot.paramMap.get('id');
    const nextMode: WorkspaceMode = segments.at(-1) === 'new' ? 'create' : segments.at(-1) === 'edit' ? 'edit' : id ? 'detail' : 'list';
    this.mode.set(nextMode);
    const sequence = ++this.routeSequence;
    if (nextMode === 'list') void this.loadList();
    else if (nextMode === 'create') void this.loadCreate(sequence);
    else if (id) void this.loadDetail(id, nextMode === 'edit', sequence);
  }

  async loadList(): Promise<void> {
    const sequence = ++this.routeSequence;
    this.loading.set(true); this.listError.set(null);
    try {
      const requests = await firstValueFrom(this.purchaseRequests.list('Approved'));
      const scopes = await firstValueFrom(this.purchaseRequests.organizationScopes());
      this.organizationScopes.set(scopes);
      const scopeMap = new Map(scopes.map((scope) => [`${scope.companyId}:${scope.branchId ?? ''}`, scope.displayName]));
      const batches = await Promise.all(requests.map(async (request) => {
        const items = await firstValueFrom(this.quotations.list(request.id));
        let selectedId: string | null = null;
        try { selectedId = (await firstValueFrom(this.quotations.comparison(request.id))).currentSourceDecision?.selectedQuotationId ?? null; } catch { selectedId = null; }
        return items.map((item) => ({ ...item, purchaseRequestReference: this.formatReference(request.id, 'PR'), organization: scopeMap.get(`${request.companyId}:${request.branchId ?? ''}`) ?? this.language.text('supplierQuotationOrganizationUnavailable'), isSelected: item.id === selectedId }));
      }));
      if (sequence === this.routeSequence) this.records.set(batches.flat());
    } catch (error: unknown) { if (sequence === this.routeSequence) this.listError.set(toSafeUiError(error)); }
    finally { if (sequence === this.routeSequence) this.loading.set(false); }
  }

  private async loadCreate(sequence: number): Promise<void> {
    this.formLoading.set(true); this.formErrorState.set(null); this.formValidationError.set(false); this.referenceError.set(false); this.draft = this.emptyDraft(); this.selectedRequest.set(null);
    try { await Promise.all([this.loadApprovedRequests(), this.loadReferenceData(), this.loadOrganizationScopes()]); }
    catch (error: unknown) { if (sequence === this.routeSequence) this.formErrorState.set(toSafeUiError(error)); }
    finally { if (sequence === this.routeSequence) this.formLoading.set(false); }
  }

  private async loadDetail(id: string, editMode: boolean, sequence: number): Promise<void> {
    this.detailLoading.set(true); this.detailError.set(null); this.mutationError.set(null); this.successNotice.set(null); this.comparison.set(null); this.comparisonError.set(null); this.historyEntries.set([]); this.auditEntries.set([]); this.sourceDecisionHistory.set([]);
    try {
      const quotation = await firstValueFrom(this.quotations.get(id));
      if (sequence !== this.routeSequence) return;
      this.detail.set(quotation); this.draft = this.toDraft(quotation);
      try { this.selectedRequest.set(await firstValueFrom(this.purchaseRequests.get(quotation.purchaseRequestId))); } catch { this.selectedRequest.set(null); }
      await this.loadOrganizationScopes();
      if (editMode && !quotation.canEdit) { await this.router.navigate(this.detailLink(id)); return; }
      await this.loadSupportingData(quotation.purchaseRequestId, quotation);
    } catch (error: unknown) { if (sequence === this.routeSequence) this.detailError.set(toSafeUiError(error)); }
    finally { if (sequence === this.routeSequence) this.detailLoading.set(false); }
  }

  private async loadSupportingData(purchaseRequestId: string, quotation: SupplierQuotationResponse): Promise<void> {
    try { this.comparison.set(await firstValueFrom(this.quotations.comparison(purchaseRequestId))); this.currentDecision.set(this.comparison()?.currentSourceDecision ?? null); } catch (error: unknown) { this.comparisonError.set(toSafeUiError(error)); }
    try { const decision = await firstValueFrom(this.quotations.sourceDecision(purchaseRequestId)); this.currentDecision.set(decision); } catch { /* Empty decision is a supported state. */ }
    this.selectedDecisionId.set(this.currentDecision()?.selectedQuotationId ?? ''); this.decisionRationale = this.currentDecision()?.rationale ?? '';
    try { this.sourceDecisionHistory.set(await firstValueFrom(this.quotations.sourceDecisionHistory(purchaseRequestId))); } catch { this.sourceDecisionHistory.set([]); }
    try { this.historyEntries.set(await firstValueFrom(this.quotations.history(quotation.id))); } catch { this.historyEntries.set([]); }
    try { this.auditEntries.set(await firstValueFrom(this.quotations.audit(quotation.id))); } catch { this.auditEntries.set([]); }
  }

  async reloadList(): Promise<void> { await this.loadList(); }
  async reloadDetail(): Promise<void> { const id = this.detail()?.id ?? this.route.snapshot.paramMap.get('id'); if (id) await this.loadDetail(id, this.mode() === 'edit', ++this.routeSequence); }
  async reloadForm(): Promise<void> { this.applyRoute(); }
  async loadApprovedRequests(): Promise<void> { this.approvedRequests.set(await firstValueFrom(this.purchaseRequests.list('Approved'))); }

  async loadOrganizationScopes(): Promise<void> { this.organizationScopes.set(await firstValueFrom(this.purchaseRequests.organizationScopes())); }

  async loadReferenceData(): Promise<void> {
    try {
      const [suppliers, currencies, paymentTerms, taxes] = await Promise.all([firstValueFrom(this.masterData.list('suppliers')), firstValueFrom(this.masterData.list('currencies')), firstValueFrom(this.masterData.list('payment-terms')), firstValueFrom(this.masterData.list('taxes'))]);
      this.suppliers.set((suppliers as SupplierRecord[]).filter((record) => record.lifecycleState === 'Active')); this.currencies.set((currencies as CurrencyRecord[]).filter((record) => record.lifecycleState === 'Active')); this.paymentTerms.set((paymentTerms as PaymentTermRecord[]).filter((record) => record.lifecycleState === 'Active')); this.taxes.set((taxes as TaxRecord[]).filter((record) => record.lifecycleState === 'Active'));
    } catch { this.referenceError.set(true); this.suppliers.set([]); this.currencies.set([]); this.paymentTerms.set([]); this.taxes.set([]); }
  }

  async selectPurchaseRequest(id: string): Promise<void> {
    this.draft.purchaseRequestId = id; this.selectedRequest.set(null); this.draft.lines = []; if (!id) return;
    const sequence = ++this.requestSequence;
    try { const request = await firstValueFrom(this.purchaseRequests.get(id)); if (sequence === this.requestSequence && request.status === 'Approved') { this.selectedRequest.set(request); this.draft.lines = request.lines.map((line) => this.newDraftLine(line)); } }
    catch (error: unknown) { this.formErrorState.set(toSafeUiError(error)); }
  }

  async saveDraft(): Promise<void> {
    this.formValidationError.set(false); if (!this.isDraftValid()) { this.formValidationError.set(true); return; }
    const requestId = this.draft.purchaseRequestId || this.detail()?.purchaseRequestId; if (!requestId) { this.formValidationError.set(true); return; }
    this.saving.set(true); this.mutationError.set(null);
    try { const payload = this.toPayload(); const saved = this.mode() === 'create' ? await this.quotations.create(requestId, payload) : await this.quotations.edit(this.detail()!.id, payload, this.detail()!.version); await this.router.navigate(this.detailLink(saved.id)); }
    catch (error: unknown) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.saving.set(false); }
  }

  openAction(action: LifecycleAction): void { this.actionReasonValue = ''; this.actionError.set(false); this.dialogAction.set(action); }
  closeAction(): void { this.dialogAction.set(null); this.actionReasonValue = ''; this.actionError.set(false); }
  get actionReason(): string { return this.actionReasonValue; }
  set actionReason(value: string) { this.actionReasonValue = value; }

  async confirmAction(): Promise<void> {
    const quotation = this.detail(); const action = this.dialogAction(); if (!quotation || !action) return;
    if (action === 'disqualify' && !this.actionReasonValue.trim()) { this.actionError.set(true); return; }
    this.savingAction.set(true); this.mutationError.set(null);
    try { const updated = action === 'submit' ? await this.quotations.submit(quotation.id, quotation.version) : action === 'withdraw' ? await this.quotations.withdraw(quotation.id, quotation.version, this.actionReasonValue) : await this.quotations.disqualify(quotation.id, quotation.version, this.actionReasonValue); this.detail.set(updated); this.draft = this.toDraft(updated); this.closeAction(); this.successNotice.set(this.language.text('recordSaved')); await this.loadSupportingData(updated.purchaseRequestId, updated); }
    catch (error: unknown) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.savingAction.set(false); }
  }

  async recordDecision(): Promise<void> {
    const quotation = this.detail(); const request = this.selectedRequest(); if (!quotation || !request || !this.selectedDecisionId() || !this.decisionRationale.trim()) { this.decisionValidationError.set(true); return; }
    this.decisionValidationError.set(false); this.savingDecision.set(true); this.mutationError.set(null);
    try {
      const expectedVersion = this.currentDecision()?.version ?? request.version;
      const decision = await this.quotations.recordSourceDecision(request.id, this.selectedDecisionId(), this.decisionRationale.trim(), expectedVersion);
      this.currentDecision.set(decision);
      this.successNotice.set(this.language.text('supplierQuotationDecisionSaved'));
      await this.loadSupportingData(request.id, quotation);
    }
    catch (error: unknown) { this.mutationError.set(toSafeUiError(error)); }
    finally { this.savingDecision.set(false); }
  }

  setTab(tab: DetailTab): void { this.activeTab.set(tab); }
  selectDecisionCandidate(id: string): void { this.selectedDecisionId.set(id); this.decisionValidationError.set(false); }
  addEvidence(): void { this.draft.evidence = [...this.draft.evidence, { referenceId: '', fileName: '', contentType: '', description: '', source: 'buyer-recorded', externalReference: '' }]; }
  removeEvidence(index: number): void { this.draft.evidence = this.draft.evidence.filter((_, itemIndex) => itemIndex !== index); }

  statusLabel(status: SupplierQuotationStatus): string { if (status === 'Draft') return this.language.text('supplierQuotationStatusDraft'); if (status === 'Submitted') return this.language.text('supplierQuotationStatusSubmitted'); if (status === 'Withdrawn') return this.language.text('supplierQuotationStatusWithdrawn'); if (status === 'Disqualified') return this.language.text('supplierQuotationStatusDisqualified'); if (status === 'Superseded') return this.language.text('supplierQuotationStatusSuperseded'); return status; }
  statusClass(status: SupplierQuotationStatus): string { return `status-badge--${status.toLocaleLowerCase()}`; }
  errorText(error: SafeUiError): string { if (error.code === 'access_denied' || error.code === 'permission_denied') return this.language.text('accessDenied'); if (error.code === 'concurrency_conflict') return this.language.text('prConcurrencyConflictError'); if (error.code === 'network_error') return this.language.text('networkError'); if (error.code === 'validation_failed') return this.language.text('validationError'); return this.language.text('requestError'); }
  detailLink(id: string): string[] { return ['/app/procurement/supplier-quotations', id]; }
  editLink(id: string): string[] { return ['/app/procurement/supplier-quotations', id, 'edit']; }
  formatReference(id: string, prefix: string): string { return `${prefix}-${id.replaceAll('-', '').slice(0, 8).toUpperCase()}`; }
  formatDate(value: string | null): string { return value ? new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium' }).format(new Date(`${value.slice(0, 10)}T00:00:00`)) : '—'; }
  formatDateTime(value: string): string { return new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  formatMoney(value: number, currency: string): string {
    const language = this.language.language();
    const safeCurrency = (currency || '').trim();
    if (safeCurrency) {
      try {
        return new Intl.NumberFormat(language, {
          style: 'currency',
          currency: safeCurrency,
          maximumFractionDigits: 2,
          minimumFractionDigits: 2,
        }).format(value);
      } catch {
        // Fall back safely if Intl rejects non-ISO or unrecognized currency code
      }
    }
    const formatted = new Intl.NumberFormat(language, {
      maximumFractionDigits: 2,
      minimumFractionDigits: 2,
    }).format(value);
    return safeCurrency ? `${formatted} ${safeCurrency}` : formatted;
  }
  formatQuantity(value: number): string { return new Intl.NumberFormat(this.language.language(), { maximumFractionDigits: 3 }).format(value); }
  numberValue(event: Event): number { const value = Number((event.target as HTMLInputElement).value); return Number.isFinite(value) ? value : 0; }
  nullableNumber(event: Event): number | null { const raw = (event.target as HTMLInputElement).value; if (!raw.trim()) return null; const value = Number(raw); return Number.isFinite(value) ? value : null; }
  nullableSelect(event: Event): string | null { const value = (event.target as HTMLSelectElement).value; return value || null; }
  masterName(record: MasterDataRecord): string { const arabic = this.language.language() === 'ar'; const value = 'englishLegalName' in record ? (arabic ? record.arabicTradingName || record.arabicLegalName : record.englishTradingName || record.englishLegalName) : 'englishName' in record ? (arabic ? record.arabicName : record.englishName) : ''; return value || ('code' in record ? record.code : ''); }
  organizationForRequest(request: PurchaseRequestResponse | null): string { if (!request) return this.language.text('supplierQuotationOrganizationUnavailable'); return this.organizationScopes().find((scope) => scope.companyId === request.companyId && scope.branchId === request.branchId)?.displayName ?? this.language.text('supplierQuotationOrganizationUnavailable'); }
  serverTotal(quotation: SupplierQuotationResponse): number { return this.comparison()?.quotations.find((item) => item.supplierQuotationId === quotation.id)?.commercialTotal ?? 0; }
  currentDecisionId(): string { return this.currentDecision()?.selectedQuotationId ?? ''; }
  itemsForGroup(ids: string[], comparison: SupplierQuotationComparisonResponse): SupplierQuotationComparisonItemResponse[] { return ids.map((id) => comparison.quotations.find((item) => item.supplierQuotationId === id)).filter((item): item is SupplierQuotationComparisonItemResponse => item !== undefined); }
  quotationLabel(id: string, comparison: SupplierQuotationComparisonResponse): string { const item = comparison.quotations.find((candidate) => candidate.supplierQuotationId === id); return item ? `${item.supplier.name} · ${item.supplierQuotationReference}` : this.formatReference(id, 'SQ'); }
  actionTitle(action: LifecycleAction): string { return this.language.text(action === 'submit' ? 'supplierQuotationActionSubmitTitle' : action === 'withdraw' ? 'supplierQuotationActionWithdrawTitle' : 'supplierQuotationActionDisqualifyTitle'); }
  actionLead(action: LifecycleAction): string { return this.language.text(action === 'submit' ? 'supplierQuotationActionSubmitLead' : action === 'withdraw' ? 'supplierQuotationActionWithdrawLead' : 'supplierQuotationActionDisqualifyLead'); }
  actionLabel(action: LifecycleAction): string { return this.language.text(action === 'submit' ? 'supplierQuotationSubmit' : action === 'withdraw' ? 'supplierQuotationWithdraw' : 'supplierQuotationDisqualify'); }

  private async loadCreateReferences(): Promise<void> { await Promise.all([this.loadApprovedRequests(), this.loadReferenceData()]); }
  private toDraft(quotation: SupplierQuotationResponse): QuotationDraft { return { purchaseRequestId: quotation.purchaseRequestId, supplierId: quotation.supplier.id, supplierQuotationReference: quotation.supplierQuotationReference, offerDate: quotation.offerDate, validUntil: quotation.validUntil ?? '', currencyId: quotation.currency.id, paymentTermId: quotation.paymentTerm?.id ?? '', deliveryTerms: quotation.deliveryTerms ?? '', offeredDeliveryDate: quotation.offeredDeliveryDate ?? '', offeredDeliveryLeadTime: quotation.offeredDeliveryLeadTime ?? '', notes: quotation.notes ?? '', lines: quotation.lines.map((line) => ({ purchaseRequestLineId: line.purchaseRequestLineId, productSku: line.productSku, productName: line.productName, unitOfMeasureCode: line.unitOfMeasureCode, requestedQuantity: line.requestedQuantity, requestedNeedByDate: line.requestedNeedByDate, purpose: '', quotedQuantity: line.quotedQuantity, unitPrice: line.unitPrice, discountAmount: line.discountAmount, discountPercentage: line.discountPercentage, taxId: line.taxId, taxReference: line.taxReference ?? '', taxRatePercentage: line.taxRatePercentage, taxAmount: line.taxAmount, offeredDeliveryDate: line.offeredDeliveryDate ?? '', offeredDeliveryLeadTime: line.offeredDeliveryLeadTime ?? '', notes: line.notes ?? '' })), evidence: quotation.evidence.map((item) => ({ referenceId: item.referenceId, fileName: item.fileName ?? '', contentType: item.contentType ?? '', description: item.description ?? '', source: item.source, externalReference: item.externalReference ?? '' })) }; }
  private emptyDraft(): QuotationDraft { return { purchaseRequestId: '', supplierId: '', supplierQuotationReference: '', offerDate: new Date().toISOString().slice(0, 10), validUntil: '', currencyId: '', paymentTermId: '', deliveryTerms: '', offeredDeliveryDate: '', offeredDeliveryLeadTime: '', notes: '', lines: [], evidence: [] }; }
  private newDraftLine(line: PurchaseRequestLineResponse): QuotationDraftLine { return { purchaseRequestLineId: line.id, productSku: line.productSku, productName: line.productName, unitOfMeasureCode: line.unitOfMeasureCode, requestedQuantity: line.quantity, requestedNeedByDate: line.needByDate, purpose: line.purpose, quotedQuantity: line.quantity, unitPrice: 0, discountAmount: null, discountPercentage: null, taxId: null, taxReference: '', taxRatePercentage: null, taxAmount: null, offeredDeliveryDate: '', offeredDeliveryLeadTime: '', notes: '' }; }
  private isDraftValid(): boolean { return Boolean(this.draft.purchaseRequestId && this.draft.supplierId && this.draft.supplierQuotationReference.trim() && this.draft.offerDate && this.draft.currencyId && this.draft.lines.length > 0 && this.draft.lines.every((line) => line.quotedQuantity > 0 && line.unitPrice >= 0 && !(line.discountAmount !== null && line.discountPercentage !== null)) && this.draft.evidence.every((item) => item.referenceId.trim() || item.externalReference.trim())); }
  private toPayload(): SupplierQuotationWriteRequest { return { supplierId: this.draft.supplierId, supplierQuotationReference: this.draft.supplierQuotationReference.trim(), offerDate: this.draft.offerDate, validUntil: this.draft.validUntil || null, currencyId: this.draft.currencyId, paymentTermId: this.draft.paymentTermId || null, deliveryTerms: this.draft.deliveryTerms.trim() || null, offeredDeliveryDate: this.draft.offeredDeliveryDate || null, offeredDeliveryLeadTime: this.draft.offeredDeliveryLeadTime.trim() || null, notes: this.draft.notes.trim() || null, lines: this.draft.lines.map((line) => ({ purchaseRequestLineId: line.purchaseRequestLineId, quotedQuantity: line.quotedQuantity, unitPrice: line.unitPrice, discountAmount: line.discountAmount, discountPercentage: line.discountPercentage, taxId: line.taxId, taxReference: line.taxReference.trim() || null, taxRatePercentage: line.taxRatePercentage, taxAmount: line.taxAmount, offeredDeliveryDate: line.offeredDeliveryDate || null, offeredDeliveryLeadTime: line.offeredDeliveryLeadTime.trim() || null, notes: line.notes.trim() || null })), evidence: this.draft.evidence.map((item) => ({ referenceId: item.referenceId.trim() || null, fileName: item.fileName.trim() || null, contentType: item.contentType.trim() || null, description: item.description.trim() || null, source: item.source.trim() || null, externalReference: item.externalReference.trim() || null })) }; }
}
