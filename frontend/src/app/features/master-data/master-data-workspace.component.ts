import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import {
  CategoryDraft,
  CategoryRecord,
  ContactDraft,
  CustomerRecord,
  CurrencyDraft,
  CurrencyRecord,
  ExchangeRateDraft,
  ExchangeRateRecord,
  ExchangeRateReferenceResponse,
  MasterDataAuditEntry,
  MasterDataDraft,
  MasterDataRecord,
  MasterDataResourceKey,
  MasterDataWritePayload,
  PartyDraft,
  PaymentTermDraft,
  PaymentTermInstallmentDraft,
  PaymentTermRecord,
  ProductDraft,
  ProductRecord,
  RESOURCE_DEFINITIONS,
  SupplierRecord,
  TaxCalculationRequest,
  TaxCalculationResponse,
  TaxDraft,
  TaxRateVersionRecord,
  TaxRecord,
  UnitDraft,
  UnitOfMeasureRecord,
  isMasterDataResourceKey,
  resourceDefinition,
} from './master-data.models';
import { MasterDataService } from './master-data.service';

type DetailMode = 'view' | 'edit' | 'create';
type StatusFilter = 'all' | 'Active' | 'Inactive';
type LifecycleAction = 'deactivate' | 'reactivate';

@Component({
  selector: 'app-master-data-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, DatePipe, FormsModule, NgTemplateOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="master-data" aria-labelledby="master-data-title">
      <app-page-header class="page-header" [pageLabelOverride]="language.text(currentDefinition().labelKey)">
        <div page-header-copy>
          <p class="eyebrow">{{ language.text('masterData') }} / {{ language.text('tenantCatalog') }}</p>
          <h1 id="master-data-title">{{ language.text(currentDefinition().labelKey) }}</h1>
          <p class="hero-lede">{{ language.text('masterDataLead') }} {{ language.text(currentDefinition().leadKey) }}</p>
        </div>
          <div page-header-actions>
        
        @if (!detailMode()) {
          <div class="header-actions">
            <button class="button button--quiet" type="button" (click)="loadList()" [disabled]="loading()" [attr.aria-label]="language.text('refresh')">↻ <span>{{ language.text('refresh') }}</span></button>
            <button class="button button--primary" type="button" (click)="startCreate()" [disabled]="!canMutate()" [title]="canMutate() ? '' : language.text('accessUnavailable')">＋ {{ language.text('newRecord') }}</button>
          </div>
        }
      
          </div>
        </app-page-header>/span>
        </div>
        <p class="master-data__note" role="note">{{ language.text('serverAuthority') }} · {{ language.text('tenantWide') }} · {{ language.text('lifecycle') }} · {{ language.text('noDraftDelete') }}</p>

        <div class="workspace-panel">
          @if (detailMode()) {
            <ng-container *ngTemplateOutlet="detailView" />
          } @else {
            <ng-container *ngTemplateOutlet="listView" />
          }
        </div>
      </div>
    </section>

    <ng-template #listView>
      <section class="list-view" aria-labelledby="resource-title" aria-describedby="resource-description">
        <h2 id="resource-title" class="sr-only">{{ language.text(currentDefinition().labelKey) }}</h2>
        <p id="resource-description" class="sr-only">{{ language.text(currentDefinition().leadKey) }}</p>

        <div class="toolbar" role="search">
          <label class="search-field">
            <span class="sr-only">{{ language.text('searchRecords') }}</span>
            <span class="search-field__icon" aria-hidden="true">⌕</span>
            <input type="search" [value]="filterQuery()" (input)="onSearch($event)" [placeholder]="language.text('searchPlaceholder')" />
          </label>
          <label class="filter-field">
            <span class="sr-only">{{ language.text('statusFilter') }}</span>
            <select [value]="statusFilter()" (change)="onStatusChange($event)">
              <option value="all">{{ language.text('allStatuses') }}</option>
              <option value="Active">{{ language.text('activeStatus') }}</option>
              <option value="Inactive">{{ language.text('inactiveStatus') }}</option>
            </select>
          </label>
          <span class="toolbar__count">{{ filteredRecords().length }} {{ language.text('recordCount') }}</span>
        </div>

        @if (loading()) {
          <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><div><b>{{ language.text('loadingRecords') }}</b><p>{{ language.text('serverAuthority') }}</p></div></div>
        } @else if (listError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(listError()) }}</b><p>{{ language.text('listLoadFailed') }}</p><button class="text-button" type="button" (click)="loadList()">{{ language.text('retry') }} ↗</button></div></div>
        } @else if (filteredRecords().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ records().length === 0 ? language.text('noRecords') : language.text('noSearchMatches') }}</b><p>{{ records().length === 0 ? language.text('noRecordsLead') : language.text('noSearchMatchesLead') }}</p></div></div>
        } @else {
          <app-data-grid
            [caption]="language.text(currentDefinition().labelKey)"
            [rows]="filteredRecords()"
            [columns]="recordColumns()"
            [language]="language.language()"
            [rowActions]="gridRowActions()"
            [pageSize]="pageSize"
            [countLabel]="language.text('recordCount')"
            [loadingLabel]="language.text('loadingRecords')"
            [emptyLabel]="language.text('noRecords')"
            [emptyHint]="language.text('noRecordsLead')"
            [noMatchesLabel]="language.text('noSearchMatches')"
            [clearFiltersLabel]="language.language() === 'ar' ? 'مسح عوامل التصفية' : 'Clear filters'"
            [filterInputLabel]="language.text('searchRecords')"
            [allValuesLabel]="language.text('allStatuses')"
            [rowActionsLabel]="language.text('viewRecord')"
            (rowAction)="onGridAction($event)"
          />
        }
      </section>
    </ng-template>

    <ng-template #detailView>
      <section class="detail-view" aria-labelledby="detail-title">
        <div class="detail-topline"><button class="back-link" type="button" (click)="backToList()">← {{ language.text('masterData') }}</button><span class="detail-scope">{{ language.text('tenantWide') }}</span></div>
        @if (detailLoading()) {
          <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loadingRecord') }}</b></div>
        } @else if (detailError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(detailError()) }}</b><p>{{ language.text('detailLoadFailed') }}</p><button class="text-button" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') }} ↗</button></div></div>
        } @else {
          <div class="detail-heading">
            <div><p class="eyebrow eyebrow--soft">{{ detailMode() === 'create' ? language.text('newRecord') : language.text('viewRecord') }}</p><h2 id="detail-title">{{ detailMode() === 'create' ? language.text('createRecord') : recordName(selectedRecord()) }}</h2><p>{{ language.text(currentDefinition().leadKey) }}</p></div>
            <div class="detail-heading__actions">
              @if (selectedRecord() && detailMode() === 'view') {
                <span class="status-pill" [class.status-pill--inactive]="!isActive(selectedRecord()!)"><i aria-hidden="true"></i>{{ statusLabel(selectedRecord()!.lifecycleState) }}</span>
                <button class="button button--quiet" type="button" (click)="startEdit()" [disabled]="!canMutate()">{{ language.text('editRecord') }}</button>
                @if (isActive(selectedRecord()!)) { <button class="button button--danger" type="button" (click)="openLifecycle('deactivate')" [disabled]="!canMutate()">{{ language.text('deactivate') }}</button> } @else { <button class="button button--primary" type="button" (click)="openLifecycle('reactivate')" [disabled]="!canMutate()">{{ language.text('reactivate') }}</button> }
              }
            </div>
          </div>

          @if (mutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b><span>{{ mutationError()?.code === 'concurrency_conflict' ? language.text('conflictLead') : language.text('requestError') }}</span></div> }
          @if (formNotice()) { <div class="inline-alert inline-alert--success" role="status">{{ formNotice() }}</div> }

          @if (detailMode() === 'view' && selectedRecord()) {
            <div class="detail-grid">
              <article class="detail-card detail-card--main"><div class="card-kicker">{{ language.text('masterData') }} / {{ recordCode(selectedRecord()!) }}</div><ng-container *ngTemplateOutlet="readOnlyFields" /></article>
              <aside class="detail-card detail-card--rail"><div class="card-kicker">{{ language.text('lifecycle') }}</div><div class="fact-stack"><div><span>{{ language.text('version') }}</span><b>{{ versionLabel(selectedRecord()!.version) }}</b></div><div><span>{{ language.text('serverAuthority') }}</span><b>{{ language.text('tenantWide') }}</b></div><div><span>{{ language.text('noDraftDelete') }}</span><b>Active / Inactive</b></div></div></aside>
            </div>
            <section class="audit-panel" aria-labelledby="audit-title"><div class="audit-heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('audit') }}</p><h3 id="audit-title">{{ language.text('audit') }}</h3></div><span class="audit-count">{{ auditEntries().length }}</span></div>@if (auditLoading()) { <p class="muted-line">{{ language.text('loading') }}…</p> } @else if (auditError()) { <p class="muted-line">{{ language.text('auditUnavailable') }}</p> } @else if (auditEntries().length === 0) { <p class="muted-line">{{ language.text('auditEmpty') }}</p> } @else { <div class="audit-table-wrap"><table class="audit-table"><thead><tr><th>{{ language.text('auditWhen') }}</th><th>{{ language.text('auditAction') }}</th><th>{{ language.text('auditDecision') }}</th><th>{{ language.text('auditReason') }}</th></tr></thead><tbody>@for (entry of auditEntries(); track entry.evidenceId) { <tr><td>{{ entry.occurredAt | date:'medium' }}</td><td>{{ entry.operation }}</td><td>{{ entry.decision }}</td><td><span>{{ entry.reason }}</span>@if (entry.afterSummary) { <small>{{ entry.afterSummary }}</small> }</td></tr> }</tbody></table></div> }</section>
          } @else {
            <form class="edit-card" (ngSubmit)="save()" novalidate>
              @if (formError()) { <div class="form-summary" role="alert">{{ language.text('validationSummary') }}</div> }
              <ng-container *ngTemplateOutlet="editableFields" />
              <div class="form-actions"><button class="button button--quiet" type="button" (click)="cancelEdit()">{{ language.text('cancel') }}</button><button class="button button--primary" type="submit" [disabled]="saving()">{{ saving() ? language.text('actionInProgress') : (detailMode() === 'create' ? language.text('createRecord') : language.text('saveRecord')) }}</button></div>
            </form>
          }
        }
      </section>
    </ng-template>

    <ng-template #readOnlyFields>
      @if (exchangeRateRecord(); as record) {
        @let current = exchangeRateCurrentVersion(record);
        <div class="field-read-grid">
          <div><span>{{ language.text('sourceCurrency') }}</span><b>{{ record.sourceCurrencyCode }} / {{ referenceName('currencies', record.sourceCurrencyId) }}</b></div>
          <div><span>{{ language.text('targetCurrency') }}</span><b>{{ record.targetCurrencyCode }} / {{ referenceName('currencies', record.targetCurrencyId) }}</b></div>
          <div><span>{{ language.text('exchangeRateVersion') }}</span><b>v{{ record.currentVersionNumber }}</b></div>
          <div><span>{{ language.text('rate') }}</span><b>{{ current?.rate ?? language.text('emptyValue') }}</b></div>
          <div><span>{{ language.text('rateScale') }}</span><b>{{ current?.rateScale ?? language.text('emptyValue') }}</b></div>
          <div><span>{{ language.text('provenance') }}</span><b>{{ current?.provenance ?? language.text('emptyValue') }}</b></div>
          <div><span>{{ language.text('effectiveFrom') }}</span><b>{{ current?.effectiveFrom ?? language.text('emptyValue') }}</b></div>
          <div><span>{{ language.text('effectiveTo') }}</span><b>{{ current?.effectiveTo ?? language.text('emptyValue') }}</b></div>
          <div class="field-read-grid__wide"><span>{{ language.text('effectiveHistory') }}</span><b>{{ record.versions.length }} {{ language.text('versionCount') }} / {{ exchangeRateHistoryLabel(record) }}</b></div>
          <div class="field-read-grid__wide"><span>{{ language.text('sourceNotes') }}</span><b>{{ valueOrEmpty(current?.sourceNotes) }}</b></div>
        </div>
        <p class="boundary-note">{{ language.text('exchangeRateBoundary') }}</p>
        <section class="exchange-reference-panel" aria-labelledby="exchange-reference-title">
          <div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('referenceEvidence') }}</p><h3 id="exchange-reference-title">{{ language.text('referenceEvidence') }}</h3></div><span>{{ language.text('serverAuthority') }}</span></div>
          <p class="term-hint">{{ language.text('exchangeReferenceLead') }}</p>
          <div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('effectiveOn') }}</span><input type="date" [ngModel]="exchangeReferenceDate()" (ngModelChange)="setExchangeReferenceDate($event)" name="exchangeReferenceDate" /></label><div class="form-field form-field--action"><span>&nbsp;</span><button class="button button--primary" type="button" (click)="resolveExchangeReference()" [disabled]="exchangeReferenceLoading() || !exchangeReferenceDate()">{{ exchangeReferenceLoading() ? language.text('resolvingReference') : language.text('resolveReference') }}</button></div></div>
          @if (exchangeReferenceLoading()) { <p class="muted-line" role="status">{{ language.text('resolvingReference') }}</p> } @else if (exchangeReferenceError()) { <p class="inline-alert" role="alert">{{ language.text('exchangeReferenceUnavailable') }}</p> } @else if (exchangeReference(); as reference) { <div class="exchange-reference-result" role="status"><div><span>{{ language.text('referenceValue') }}</span><b>{{ reference.referenceValue }}</b></div><div><span>{{ language.text('rate') }}</span><b>{{ reference.rate }}</b></div><div><span>{{ language.text('provenance') }}</span><b>{{ reference.provenance }}</b></div><div><span>{{ language.text('version') }}</span><b>v{{ reference.versionNumber }} / {{ reference.effectiveFrom }}{{ reference.effectiveTo ? ' - ' + reference.effectiveTo : '' }}</b></div><div class="field-read-grid__wide"><span>{{ language.text('sourceNotes') }}</span><b>{{ valueOrEmpty(reference.sourceNotes) }}</b></div></div> } @else { <p class="muted-line">{{ language.text('exchangeReferenceLead') }}</p> }
        </section>
      }
      @if (taxRecord(); as record) { @let current = taxCurrentVersion(record); <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('taxCategoryCode') }}</span><b>{{ record.categoryCode }}</b></div><div><span>{{ language.text('taxCategoryEnglishName') }}</span><b>{{ valueOrEmpty(record.categoryEnglishName) }}</b></div><div dir="rtl"><span>{{ language.text('taxCategoryArabicName') }}</span><b>{{ valueOrEmpty(record.categoryArabicName) }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div dir="rtl"><span>{{ language.text('arabicName') }}</span><b>{{ valueOrEmpty(record.arabicName) }}</b></div><div><span>{{ language.text('applicability') }}</span><b>{{ taxDirectionLabel(record.applicability) }}</b></div><div><span>{{ language.text('taxVersion') }}</span><b>v{{ record.currentVersionNumber }}</b></div><div><span>{{ language.text('taxRate') }}</span><b>{{ current?.ratePercentage ?? language.text('emptyValue') }}%</b></div><div class="field-read-grid__wide"><span>{{ language.text('effectiveHistory') }}</span><b>{{ record.rateVersions.length }} {{ language.text('versionCount') }} Â· {{ taxHistoryLabel(record) }}</b></div></div><p class="boundary-note">{{ language.text('taxBoundary') }}</p><section class="tax-engine-panel" aria-labelledby="tax-engine-title"><div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">{{ language.text('taxCalculationPreview') }}</p><h3 id="tax-engine-title">{{ language.text('taxCalculationPreview') }}</h3></div><span>{{ language.text('serverAuthority') }}</span></div><p class="term-hint">{{ language.text('taxCalculationLead') }}</p><div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('effectiveOn') }}</span><input type="date" [ngModel]="taxCalculationDraft.effectiveOn" (ngModelChange)="setTaxCalculationField('effectiveOn', $event)" name="taxCalculationEffectiveOn" /></label><label class="form-field"><span>{{ language.text('transactionDirection') }}</span><select [ngModel]="taxCalculationDraft.transactionDirection" (ngModelChange)="setTaxCalculationField('transactionDirection', $event)" name="taxCalculationDirection"><option value="Sales">{{ language.text('sales') }}</option><option value="Purchase">{{ language.text('purchase') }}</option></select></label><label class="form-field"><span>{{ language.text('taxableBase') }}</span><input type="number" min="0" step="0.01" [ngModel]="taxCalculationDraft.taxableBase" (ngModelChange)="setTaxCalculationField('taxableBase', $event)" name="taxCalculationTaxableBase" /></label><label class="form-field"><span>{{ language.text('currencyCode') }}</span><input [ngModel]="taxCalculationDraft.currencyCode" (ngModelChange)="setTaxCalculationField('currencyCode', $event)" name="taxCalculationCurrency" /></label><label class="form-field"><span>{{ language.text('roundingScale') }}</span><input type="number" min="0" max="6" [ngModel]="taxCalculationDraft.roundingScale" (ngModelChange)="setTaxCalculationField('roundingScale', $event)" name="taxCalculationRoundingScale" /></label><label class="form-field"><span>{{ language.text('roundingMode') }}</span><select [ngModel]="taxCalculationDraft.roundingMode" (ngModelChange)="setTaxCalculationField('roundingMode', $event)" name="taxCalculationRoundingMode"><option value="AwayFromZero">{{ language.text('awayFromZero') }}</option><option value="ToEven">{{ language.text('toEven') }}</option></select></label></div><label class="form-field form-field--full"><span>{{ language.text('sourceLineage') }}</span><input [ngModel]="taxCalculationDraft.sourceLineage" (ngModelChange)="setTaxCalculationField('sourceLineage', $event)" name="taxCalculationSourceLineage" /></label><button class="button button--primary" type="button" (click)="calculateTax()" [disabled]="taxCalculationLoading()">{{ taxCalculationLoading() ? language.text('calculatingTax') : language.text('calculateTax') }}</button>@if (taxCalculationError()) { <p class="inline-alert" role="alert">{{ language.text('taxCalculationUnavailable') }}</p> } @if (taxCalculation(); as result) { <div class="tax-result" role="status"><div><span>{{ language.text('taxRate') }}</span><b>{{ result.ratePercentage }}%</b></div><div><span>{{ language.text('taxableBase') }}</span><b>{{ result.taxableBase }} {{ result.currencyCode }}</b></div><div><span>{{ language.text('taxAmount') }}</span><b>{{ result.taxAmount }} {{ result.currencyCode }}</b></div><div><span>{{ language.text('referenceValue') }}</span><b>{{ result.referenceValue }}</b></div></div> }<p class="boundary-note">{{ language.text('taxCalculationBoundary') }}</p></section> }
      @if (categoryRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div><div><span>{{ language.text('parentCategory') }}</span><b>{{ record.parentCategoryId ? referenceName('categories', record.parentCategoryId) : language.text('noParent') }}</b></div><div><span>{{ language.text('trackingDefault') }}</span><b>{{ record.trackingDefaultEnabled ? language.text('trackingEnabled') : language.text('trackingDisabled') }}</b></div></div> }
      @if (unitRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div></div> }
      @if (productRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('sku') }}</span><b>{{ record.sku }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div><div><span>{{ language.text('category') }}</span><b>{{ referenceName('categories', record.categoryId) }}</b></div><div><span>{{ language.text('baseUnit') }}</span><b>{{ referenceName('units', record.baseUnitOfMeasureId) }}</b></div><div><span>{{ language.text('trackingConfiguration') }}</span><b>{{ record.trackingEnabled ? language.text('trackingEnabled') : language.text('trackingDisabled') }}</b></div><div class="field-read-grid__wide"><span>{{ language.text('description') }}</span><b>{{ valueOrEmpty(record.description) }}</b></div><div class="field-read-grid__wide"><span>{{ language.text('barcodes') }}</span><b>{{ record.barcodes.length ? record.barcodes.map((barcode) => barcode.value).join(' · ') : language.text('emptyValue') }}</b></div></div><p class="boundary-note">{{ language.text('productIdentityNote') }}</p> }
      @if (supplierRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('legalName') }}</span><b>{{ localizedPartyName(record.englishLegalName, record.arabicLegalName) }}</b></div><div><span>{{ language.text('tradingName') }}</span><b>{{ localizedPartyName(record.englishTradingName, record.arabicTradingName) }}</b></div><div><span>{{ language.text('registrationReference') }}</span><b>{{ valueOrEmpty(record.registrationReference) }}</b></div></div><ng-container *ngTemplateOutlet="contactReadOnly; context: { contacts: record.contacts }" /><p class="boundary-note">{{ language.text('supplierBoundary') }}</p> }
      @if (customerRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('legalName') }}</span><b>{{ localizedPartyName(record.englishLegalName, record.arabicLegalName) }}</b></div><div><span>{{ language.text('tradingName') }}</span><b>{{ localizedPartyName(record.englishTradingName, record.arabicTradingName) }}</b></div></div><ng-container *ngTemplateOutlet="contactReadOnly; context: { contacts: record.contacts }" /><p class="boundary-note">{{ language.text('customerBoundary') }}</p> }
      @if (currencyRecord(); as record) { <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div><div><span>{{ language.text('revision') }}</span><b>{{ record.revision }}</b></div></div><p class="boundary-note">{{ language.text('currencyBoundary') }}</p> }
      @if (paymentTermRecord(); as record) { @let current = paymentTermCurrentVersion(record); <div class="field-read-grid"><div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div><div><span>{{ language.text('englishName') }}</span><b>{{ valueOrEmpty(record.englishName) }}</b></div><div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div><div><span>{{ language.text('paymentTermVersion') }}</span><b>v{{ record.currentVersionNumber }}</b></div><div><span>{{ language.text('baseDateRule') }}</span><b>{{ current?.baseDateRule || language.text('emptyValue') }}</b></div><div><span>{{ language.text('scheduleMode') }}</span><b>{{ current?.scheduleMode || language.text('emptyValue') }}</b></div><div class="field-read-grid__wide"><span>{{ language.text('effectiveHistory') }}</span><b>{{ record.versions.length }} {{ language.text('versionCount') }} · {{ paymentTermHistoryLabel(record) }}</b></div></div><p class="boundary-note">{{ language.text('paymentTermBoundary') }}</p> }
    </ng-template>

    <ng-template #contactReadOnly let-contacts="contacts"><div class="contacts-read"><span>{{ language.text('contacts') }}</span>@if (contacts.length === 0) { <b>{{ language.text('emptyValue') }}</b> } @else { @for (contact of contacts; track contact.id) { <div class="contact-line"><b>{{ contact.name }}</b><small>{{ contact.email || language.text('emptyValue') }} · {{ contact.phone || language.text('emptyValue') }}</small></div> } }</div></ng-template>

    <ng-template #exchangeEditableFields>
      @if (resource() === 'exchange-rates') {
        @let draft = exchangeRateDraft();
        <div class="exchange-edit-panel">
          <div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">02 / {{ language.text('exchangeRates') }}</p><h3>{{ language.text('exchangeRates') }}</h3></div><span>{{ language.text('exchangeRateBoundary') }}</span></div>
          <div class="form-grid">
            <label class="form-field" [class.has-error]="invalid('sourceCurrencyId')"><span>{{ language.text('sourceCurrency') }} <em>*</em></span><select [ngModel]="draft.sourceCurrencyId" (ngModelChange)="setDraftField('sourceCurrencyId', $event)" name="sourceCurrencyId" [disabled]="detailMode() === 'edit'" [attr.aria-invalid]="invalid('sourceCurrencyId')"><option value="">{{ language.text('selectCurrency') }}</option>@for (currency of currencyChoices(); track currency.id) { <option [value]="currency.id">{{ currencyOptionLabel(currency) }}</option> }</select><small>{{ invalid('sourceCurrencyId') ? language.text('required') : language.text('exchangeCurrencyHint') }}</small></label>
            <label class="form-field" [class.has-error]="invalid('targetCurrencyId')"><span>{{ language.text('targetCurrency') }} <em>*</em></span><select [ngModel]="draft.targetCurrencyId" (ngModelChange)="setDraftField('targetCurrencyId', $event)" name="targetCurrencyId" [disabled]="detailMode() === 'edit'" [attr.aria-invalid]="invalid('targetCurrencyId')"><option value="">{{ language.text('selectCurrency') }}</option>@for (currency of currencyChoices(); track currency.id) { <option [value]="currency.id">{{ currencyOptionLabel(currency) }}</option> }</select><small>{{ invalid('targetCurrencyId') ? language.text('required') : language.text('exchangeCurrencyHint') }}</small></label>
            <label class="form-field" [class.has-error]="invalid('effectiveFrom')"><span>{{ language.text('effectiveFrom') }} <em>*</em></span><input type="date" [ngModel]="draft.effectiveFrom" (ngModelChange)="setDraftField('effectiveFrom', $event)" name="exchangeEffectiveFrom" [attr.aria-invalid]="invalid('effectiveFrom')" /><small>{{ invalid('effectiveFrom') ? language.text('required') : language.text('effectiveHistoryHint') }}</small></label>
            <label class="form-field"><span>{{ language.text('effectiveTo') }}</span><input type="date" [ngModel]="draft.effectiveTo" (ngModelChange)="setDraftField('effectiveTo', $event)" name="exchangeEffectiveTo" /><small>{{ language.text('effectiveHistoryHint') }}</small></label>
            <label class="form-field" [class.has-error]="invalid('rate')"><span>{{ language.text('rate') }} <em>*</em></span><input type="number" min="0.000000000001" step="0.000000000001" [ngModel]="draft.rate" (ngModelChange)="setDraftField('rate', $event)" name="exchangeRate" [attr.aria-invalid]="invalid('rate')" /><small>{{ invalid('rate') ? language.text('validationSummary') : language.text('positiveRateHint') }}</small></label>
            <label class="form-field" [class.has-error]="invalid('rateScale')"><span>{{ language.text('rateScale') }} <em>*</em></span><input type="number" min="0" max="12" step="1" [ngModel]="draft.rateScale" (ngModelChange)="setDraftField('rateScale', $event)" name="exchangeRateScale" [attr.aria-invalid]="invalid('rateScale')" /><small>{{ invalid('rateScale') ? language.text('validationSummary') : language.text('rateScaleHint') }}</small></label>
            <label class="form-field"><span>{{ language.text('provenance') }}</span><select [ngModel]="draft.provenance" (ngModelChange)="setDraftField('provenance', $event)" name="exchangeProvenance"><option value="Manual">{{ language.text('manualProvenance') }}</option><option value="Configured">{{ language.text('configuredProvenance') }}</option></select><small>{{ language.text('provenanceHint') }}</small></label>
          </div>
          <label class="form-field form-field--full"><span>{{ language.text('sourceNotes') }}</span><textarea [ngModel]="draft.sourceNotes" (ngModelChange)="setDraftField('sourceNotes', $event)" name="exchangeSourceNotes" rows="3" maxlength="1024"></textarea><small>{{ language.text('sourceNotesHint') }}</small></label>
          <p class="boundary-note">{{ language.text('exchangeRateBoundary') }}</p>
        </div>
      }
    </ng-template>

    <ng-template #editableFields>
      <ng-container *ngTemplateOutlet="exchangeEditableFields" />
      <div class="form-section"><div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">01 / {{ language.text('masterData') }}</p><h3>{{ language.text('masterData') }}</h3></div><span>{{ language.text('serverAuthority') }}</span></div>
        @if (resource() === 'categories' || resource() === 'units') { <div class="form-grid"><label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draftText('code')" (ngModelChange)="setDraftField('code', $event)" name="code" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : '' }}</small></label><label class="form-field"><span>{{ language.text('englishName') }}</span><input [ngModel]="draftText('englishName')" (ngModelChange)="setDraftField('englishName', $event)" name="englishName" /><small></small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draftText('arabicName')" (ngModelChange)="setDraftField('arabicName', $event)" name="arabicName" /><small></small></label></div> }
        @if (resource() === 'currencies') { <div class="form-grid"><label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draftText('code')" (ngModelChange)="setDraftField('code', $event)" name="currencyCode" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : language.text('currencyCodeHint') }}</small></label><label class="form-field"><span>{{ language.text('englishName') }}</span><input [ngModel]="draftText('englishName')" (ngModelChange)="setDraftField('englishName', $event)" name="currencyEnglishName" /><small></small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draftText('arabicName')" (ngModelChange)="setDraftField('arabicName', $event)" name="currencyArabicName" /><small></small></label></div><p class="boundary-note">{{ language.text('currencyBoundary') }}</p> }
        @if (resource() === 'payment-terms') { @let draft = paymentTermDraft(); <div class="form-grid"><label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draft.code" (ngModelChange)="setDraftField('code', $event)" name="paymentTermCode" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : '' }}</small></label><label class="form-field"><span>{{ language.text('englishName') }}</span><input [ngModel]="draft.englishName" (ngModelChange)="setDraftField('englishName', $event)" name="paymentTermEnglishName" /><small></small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draft.arabicName" (ngModelChange)="setDraftField('arabicName', $event)" name="paymentTermArabicName" /><small></small></label><label class="form-field"><span>{{ language.text('effectiveFrom') }} <em>*</em></span><input type="date" [ngModel]="draft.effectiveFrom" (ngModelChange)="setDraftField('effectiveFrom', $event)" name="effectiveFrom" [attr.aria-invalid]="invalid('effectiveFrom')" /><small>{{ invalid('effectiveFrom') ? language.text('required') : language.text('effectiveHistoryHint') }}</small></label><label class="form-field"><span>{{ language.text('effectiveTo') }}</span><input type="date" [ngModel]="draft.effectiveTo" (ngModelChange)="setDraftField('effectiveTo', $event)" name="effectiveTo" /><small>{{ language.text('effectiveHistoryHint') }}</small></label><label class="form-field"><span>{{ language.text('baseDateRule') }}</span><select [ngModel]="draft.baseDateRule" (ngModelChange)="setDraftField('baseDateRule', $event)" name="baseDateRule"><option value="DocumentDate">{{ language.text('documentDate') }}</option><option value="InvoiceDate">{{ language.text('invoiceDate') }}</option><option value="DeliveryDate">{{ language.text('deliveryDate') }}</option><option value="ReceiptDate">{{ language.text('receiptDate') }}</option></select><small>{{ language.text('baseDateRuleHint') }}</small></label><label class="form-field"><span>{{ language.text('scheduleMode') }}</span><select [ngModel]="draft.scheduleMode" (ngModelChange)="setDraftField('scheduleMode', $event)" name="scheduleMode"><option value="SingleDueDate">{{ language.text('singleDueDate') }}</option><option value="Installments">{{ language.text('installments') }}</option></select><small>{{ language.text('scheduleModeHint') }}</small></label></div>@if (draft.scheduleMode === 'SingleDueDate') { <div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('dueOffsetDays') }}</span><input type="number" min="0" [ngModel]="draft.dueOffsetDays" (ngModelChange)="setDraftField('dueOffsetDays', $event)" name="dueOffsetDays" /><small>{{ language.text('dayMonthOffsetHint') }}</small></label><label class="form-field"><span>{{ language.text('dueOffsetMonths') }}</span><input type="number" min="0" [ngModel]="draft.dueOffsetMonths" (ngModelChange)="setDraftField('dueOffsetMonths', $event)" name="dueOffsetMonths" /><small>{{ language.text('dayMonthOffsetHint') }}</small></label></div> } @else { <div class="term-config-block"><div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">02 / {{ language.text('installments') }}</p><h3>{{ language.text('installments') }}</h3></div><span>{{ language.text('installmentTotal') }}: {{ installmentTotal(draft) }}%</span></div><p class="term-hint">{{ language.text('installmentHint') }}</p>@for (installment of draft.installments; track $index; let index = $index) { <div class="term-installment-row"><label class="form-field"><span>{{ language.text('sequence') }}</span><input type="number" min="1" [ngModel]="installment.sequence" (ngModelChange)="setInstallmentField(index, 'sequence', $event)" [name]="'installment-sequence-' + index" /></label><label class="form-field"><span>{{ language.text('percentage') }}</span><input type="number" min="0" max="100" step="0.01" [ngModel]="installment.percentage" (ngModelChange)="setInstallmentField(index, 'percentage', $event)" [name]="'installment-percentage-' + index" /></label><label class="form-field"><span>{{ language.text('offsetDays') }}</span><input type="number" min="0" [ngModel]="installment.days" (ngModelChange)="setInstallmentField(index, 'days', $event)" [name]="'installment-days-' + index" /></label><label class="form-field"><span>{{ language.text('offsetMonths') }}</span><input type="number" min="0" [ngModel]="installment.months" (ngModelChange)="setInstallmentField(index, 'months', $event)" [name]="'installment-months-' + index" /></label><button class="icon-button icon-button--remove" type="button" (click)="removeInstallment(index)" [attr.aria-label]="language.text('removeInstallment')">×</button></div> }<button class="text-button" type="button" (click)="addInstallment()">＋ {{ language.text('addInstallment') }}</button></div> }<div class="term-config-block"><div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">03 / {{ language.text('earlySettlementDiscount') }}</p><h3>{{ language.text('earlySettlementDiscount') }}</h3></div><span>{{ language.text('financeDownstream') }}</span></div><label class="check-field"><input type="checkbox" [ngModel]="draft.earlySettlementDiscountEnabled" (ngModelChange)="setDraftField('earlySettlementDiscountEnabled', $event)" name="discountEnabled" /><span>{{ language.text('discountEnabled') }}</span></label>@if (draft.earlySettlementDiscountEnabled) { <div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('percentage') }}</span><input type="number" min="0" max="100" step="0.01" [ngModel]="draft.earlySettlementDiscountPercentage" (ngModelChange)="setDraftField('earlySettlementDiscountPercentage', $event)" name="discountPercentage" /></label><label class="form-field"><span>{{ language.text('offsetDays') }}</span><input type="number" min="0" [ngModel]="draft.earlySettlementDiscountDays" (ngModelChange)="setDraftField('earlySettlementDiscountDays', $event)" name="discountDays" /></label><label class="form-field"><span>{{ language.text('offsetMonths') }}</span><input type="number" min="0" [ngModel]="draft.earlySettlementDiscountMonths" (ngModelChange)="setDraftField('earlySettlementDiscountMonths', $event)" name="discountMonths" /></label></div> }</div><p class="boundary-note">{{ language.text('paymentTermBoundary') }}</p> }
        @if (resource() === 'categories') { <div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('parentCategory') }}</span><input list="category-options" [ngModel]="draftText('parentCategoryId')" (ngModelChange)="setDraftField('parentCategoryId', $event)" name="parentCategoryId" placeholder="GUID" /><small>{{ language.text('referenceChoicesUnavailable') }}</small></label><label class="check-field"><input type="checkbox" [ngModel]="draftBoolean('trackingDefaultEnabled')" (ngModelChange)="setDraftField('trackingDefaultEnabled', $event)" name="trackingDefaultEnabled" /><span>{{ language.text('trackingDefault') }}</span></label></div> }
        @if (resource() === 'products') { @let draft = productDraft(); <div class="form-grid"><label class="form-field" [class.has-error]="invalid('sku')"><span>{{ language.text('sku') }} <em>*</em></span><input [ngModel]="draftText('sku')" (ngModelChange)="setDraftField('sku', $event)" name="sku" autocomplete="off" [attr.aria-invalid]="invalid('sku')" /><small>{{ invalid('sku') ? language.text('required') : '' }}</small></label><label class="form-field" [class.has-error]="invalid('englishName') && invalid('arabicName')"><span>{{ language.text('englishName') }}</span><input [ngModel]="draftText('englishName')" (ngModelChange)="setDraftField('englishName', $event)" name="productEnglishName" /><small></small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draftText('arabicName')" (ngModelChange)="setDraftField('arabicName', $event)" name="productArabicName" /><small></small></label><label class="form-field"><span>{{ language.text('category') }} <em>*</em></span><input list="category-options" [ngModel]="draftText('categoryId')" (ngModelChange)="setDraftField('categoryId', $event)" name="categoryId" placeholder="GUID" [attr.aria-invalid]="invalid('categoryId')" /><small>{{ invalid('categoryId') ? language.text('required') : language.text('referenceChoicesUnavailable') }}</small></label><label class="form-field"><span>{{ language.text('baseUnit') }} <em>*</em></span><input list="unit-options" [ngModel]="draftText('baseUnitOfMeasureId')" (ngModelChange)="setDraftField('baseUnitOfMeasureId', $event)" name="baseUnitOfMeasureId" placeholder="GUID" [attr.aria-invalid]="invalid('baseUnitOfMeasureId')" /><small>{{ invalid('baseUnitOfMeasureId') ? language.text('required') : language.text('referenceChoicesUnavailable') }}</small></label></div><label class="form-field form-field--full"><span>{{ language.text('description') }}</span><textarea [ngModel]="draftText('description')" (ngModelChange)="setDraftField('description', $event)" name="description" rows="3"></textarea><small></small></label><div class="form-grid form-grid--secondary"><label class="form-field"><span>{{ language.text('trackingConfiguration') }}</span><select [ngModel]="draftText('trackingEnabledOverride')" (ngModelChange)="setDraftField('trackingEnabledOverride', $event)" name="trackingEnabledOverride"><option value="inherit">{{ language.text('trackingInherit') }}</option><option value="enabled">{{ language.text('trackingEnabled') }}</option><option value="disabled">{{ language.text('trackingDisabled') }}</option></select><small>{{ language.text('productIdentityNote') }}</small></label><label class="form-field"><span>{{ language.text('barcodes') }}</span><textarea [ngModel]="draftText('barcodes')" (ngModelChange)="setDraftField('barcodes', $event)" name="barcodes" rows="3"></textarea><small>{{ language.text('barcodesHint') }}</small></label></div><div class="check-row"><label class="check-field"><input type="checkbox" [ngModel]="draft.isSellable" (ngModelChange)="setDraftField('isSellable', $event)" name="isSellable" /><span>{{ language.text('sellable') }}</span></label><label class="check-field"><input type="checkbox" [ngModel]="draft.isPurchasable" (ngModelChange)="setDraftField('isPurchasable', $event)" name="isPurchasable" /><span>{{ language.text('purchasable') }}</span></label><label class="check-field"><input type="checkbox" [ngModel]="draft.isInventoryRelevant" (ngModelChange)="setDraftField('isInventoryRelevant', $event)" name="isInventoryRelevant" /><span>{{ language.text('inventoryRelevant') }}</span></label></div> }
        @if (resource() === 'suppliers' || resource() === 'customers') { <div class="form-grid"><label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draftText('code')" (ngModelChange)="setDraftField('code', $event)" name="partyCode" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : '' }}</small></label><label class="form-field" [class.has-error]="invalid('englishLegalName') && invalid('arabicLegalName')"><span>{{ language.text('legalName') }} <em>*</em></span><input [ngModel]="draftText('englishLegalName')" (ngModelChange)="setDraftField('englishLegalName', $event)" name="englishLegalName" /><small>{{ invalid('englishLegalName') && invalid('arabicLegalName') ? language.text('required') : '' }}</small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draftText('arabicLegalName')" (ngModelChange)="setDraftField('arabicLegalName', $event)" name="arabicLegalName" /><small></small></label><label class="form-field"><span>{{ language.text('tradingName') }}</span><input [ngModel]="draftText('englishTradingName')" (ngModelChange)="setDraftField('englishTradingName', $event)" name="englishTradingName" /><small></small></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }} / {{ language.text('tradingName') }}</span><input [ngModel]="draftText('arabicTradingName')" (ngModelChange)="setDraftField('arabicTradingName', $event)" name="arabicTradingName" /><small></small></label>@if (resource() === 'suppliers') { <label class="form-field"><span>{{ language.text('registrationReference') }}</span><input [ngModel]="draftText('registrationReference')" (ngModelChange)="setDraftField('registrationReference', $event)" name="registrationReference" /><small></small></label> }</div><div class="contacts-edit"><div class="contacts-edit__heading"><div><p class="eyebrow eyebrow--soft">02 / {{ language.text('contacts') }}</p><h3>{{ language.text('contacts') }}</h3></div><button class="text-button" type="button" (click)="addContact()">＋ {{ language.text('addContact') }}</button></div>@for (contact of contactsDraft(); track $index; let index = $index) { <div class="contact-edit-row"><label class="form-field"><span>{{ language.text('contactName') }}</span><input [ngModel]="contactValue(index, 'name')" (ngModelChange)="setContactValue(index, 'name', $event)" [name]="'contact-name-' + index" /></label><label class="form-field"><span>{{ language.text('email') }}</span><input type="email" [ngModel]="contactValue(index, 'email')" (ngModelChange)="setContactValue(index, 'email', $event)" [name]="'contact-email-' + index" /></label><label class="form-field"><span>{{ language.text('phone') }}</span><input [ngModel]="contactValue(index, 'phone')" (ngModelChange)="setContactValue(index, 'phone', $event)" [name]="'contact-phone-' + index" /></label><button class="icon-button icon-button--remove" type="button" (click)="removeContact(index)" [attr.aria-label]="language.text('removeContact')">×</button></div> } @if (contactsDraft().length === 0) { <p class="muted-line">{{ language.text('emptyValue') }}</p> }</div> }
        @if (resource() === 'taxes') { @let draft = taxDraft(); <div class="tax-edit-panel"><div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">02 / {{ language.text('taxes') }}</p><h3>{{ language.text('taxes') }}</h3></div><span>{{ language.text('taxBoundary') }}</span></div><div class="form-grid"><label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draft.code" (ngModelChange)="setDraftField('code', $event)" name="taxCode" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : '' }}</small></label><label class="form-field" [class.has-error]="invalid('categoryCode')"><span>{{ language.text('taxCategoryCode') }} <em>*</em></span><input [ngModel]="draft.categoryCode" (ngModelChange)="setDraftField('categoryCode', $event)" name="taxCategoryCode" [attr.aria-invalid]="invalid('categoryCode')" /><small>{{ invalid('categoryCode') ? language.text('required') : language.text('explicitInputHint') }}</small></label><label class="form-field"><span>{{ language.text('taxCategoryEnglishName') }}</span><input [ngModel]="draft.categoryEnglishName" (ngModelChange)="setDraftField('categoryEnglishName', $event)" name="taxCategoryEnglishName" /></label><label class="form-field" dir="rtl"><span>{{ language.text('taxCategoryArabicName') }}</span><input [ngModel]="draft.categoryArabicName" (ngModelChange)="setDraftField('categoryArabicName', $event)" name="taxCategoryArabicName" /></label><label class="form-field" [class.has-error]="invalid('englishName') && invalid('arabicName')"><span>{{ language.text('englishName') }}</span><input [ngModel]="draft.englishName" (ngModelChange)="setDraftField('englishName', $event)" name="taxEnglishName" [attr.aria-invalid]="invalid('englishName') && invalid('arabicName')" /></label><label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draft.arabicName" (ngModelChange)="setDraftField('arabicName', $event)" name="taxArabicName" /></label><label class="form-field"><span>{{ language.text('applicability') }}</span><select [ngModel]="draft.applicability" (ngModelChange)="setDraftField('applicability', $event)" name="taxApplicability"><option value="Sales">{{ language.text('sales') }}</option><option value="Purchase">{{ language.text('purchase') }}</option><option value="Both">{{ language.text('bothDirections') }}</option></select></label><label class="form-field"><span>{{ language.text('effectiveFrom') }} <em>*</em></span><input type="date" [ngModel]="draft.effectiveFrom" (ngModelChange)="setDraftField('effectiveFrom', $event)" name="taxEffectiveFrom" [attr.aria-invalid]="invalid('effectiveFrom')" /><small>{{ invalid('effectiveFrom') ? language.text('required') : language.text('effectiveHistoryHint') }}</small></label><label class="form-field"><span>{{ language.text('effectiveTo') }}</span><input type="date" [ngModel]="draft.effectiveTo" (ngModelChange)="setDraftField('effectiveTo', $event)" name="taxEffectiveTo" /></label><label class="form-field"><span>{{ language.text('taxRate') }} <em>*</em></span><input type="number" min="0" max="100" step="0.01" [ngModel]="draft.ratePercentage" (ngModelChange)="setDraftField('ratePercentage', $event)" name="taxRate" [attr.aria-invalid]="invalid('ratePercentage')" /><small>{{ invalid('ratePercentage') ? language.text('validationSummary') : language.text('taxRateHint') }}</small></label></div><p class="boundary-note">{{ language.text('taxBoundary') }}</p></div> }
      </div>
      <datalist id="category-options">@for (category of categoryChoices(); track category.id) { <option [value]="category.id">{{ recordName(category) }}</option> }</datalist><datalist id="unit-options">@for (unit of unitChoices(); track unit.id) { <option [value]="unit.id">{{ recordName(unit) }}</option> }</datalist>
    </ng-template>

    @if (lifecycleAction()) {
      <div class="dialog-backdrop" role="presentation" (click)="closeLifecycle()"><section class="lifecycle-dialog" role="dialog" aria-modal="true" aria-labelledby="lifecycle-title" (click)="$event.stopPropagation()"><p class="eyebrow eyebrow--soft">{{ language.text('lifecycle') }}</p><h2 id="lifecycle-title">{{ lifecycleAction() === 'deactivate' ? language.text('deactivateTitle') : language.text('reactivateTitle') }}</h2>@if (lifecycleAction() === 'deactivate' && requiresLifecycleReason()) { <label class="form-field"><span>{{ language.text('lifecycleReason') }} <em>*</em></span><textarea [value]="lifecycleReason()" (input)="onLifecycleReason($event)" rows="3" [placeholder]="language.text('lifecycleReasonHint')"></textarea><small>{{ language.text('lifecycleReasonHint') }}</small></label> }<div class="form-actions"><button class="button button--quiet" type="button" (click)="closeLifecycle()">{{ language.text('cancel') }}</button><button class="button button--primary" type="button" (click)="confirmLifecycle()" [disabled]="lifecycleSaving() || (lifecycleAction() === 'deactivate' && requiresLifecycleReason() && !lifecycleReason().trim())">{{ lifecycleSaving() ? language.text('actionInProgress') : (lifecycleAction() === 'deactivate' ? language.text('confirmDeactivate') : language.text('confirmReactivate')) }}</button></div></section></div>
    }
  `,
  styles: `
    :host { display: block; }
    .master-data { display: grid; gap: 1.35rem; }
    .eyebrow { margin: 0 0 .55rem; color: var(--accent); font-size: .68rem; font-weight: 800; letter-spacing: .14em; text-transform: uppercase; }
    .eyebrow--soft { color: var(--accent-strong); }
    h1, h2, h3, p { margin-block-start: 0; }
    .workspace-grid { display: grid; grid-template-columns: minmax(0, 1fr); gap: 1rem; }
    .workspace-panel { min-width: 0; border: 1px solid var(--line); border-radius: 1.15rem; background: var(--surface-raised); box-shadow: var(--shadow-soft); }
    .list-view, .detail-view { padding: clamp(1rem, 2.5vw, 1.65rem); }.section-heading, .detail-heading, .detail-topline, .toolbar, .pagination, .audit-heading, .form-section__heading, .contacts-edit__heading, .form-actions { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }.section-heading { align-items: flex-end; margin-block-end: 1.35rem; }.section-heading h2, .detail-heading h2 { margin: 0; color: var(--ink); font: 800 clamp(1.45rem, 3vw, 2.1rem)/1 var(--font-display); letter-spacing: -.045em; }.section-heading p:not(.eyebrow), .detail-heading p:not(.eyebrow) { max-width: 37rem; margin: .5rem 0 0; color: var(--ink-muted); font-size: .82rem; line-height: 1.5; }.section-heading__actions, .detail-heading__actions { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: .5rem; }.button { min-height: 2.35rem; border: 1px solid transparent; border-radius: .55rem; padding: .58rem .82rem; font-size: .76rem; font-weight: 800; cursor: pointer; }.button:disabled { cursor: not-allowed; opacity: .45; }.button--primary { color: #173b35; background: var(--accent); }.button--primary:hover:not(:disabled) { background: #c4ead1; }.button--quiet { border-color: var(--line); color: var(--ink-muted); background: transparent; }.button--quiet:hover:not(:disabled) { border-color: var(--line-strong); color: var(--ink); background: var(--canvas); }.button--danger { color: #fff; background: var(--danger); }.toolbar { align-items: stretch; margin-block-end: 1rem; border-block: 1px solid var(--line); padding-block: .75rem; }.search-field { display: flex; align-items: center; flex: 1 1 16rem; gap: .5rem; border: 1px solid var(--line); border-radius: .55rem; padding-inline: .7rem; background: var(--canvas); }.search-field:focus-within { border-color: var(--focus); box-shadow: 0 0 0 3px rgb(13 138 131 / 12%); }.search-field__icon { color: var(--accent-strong); font-size: 1.3rem; }.search-field input, .filter-field select { min-width: 0; width: 100%; border: 0; outline: 0; color: var(--ink); background: transparent; font-size: .8rem; }.filter-field { display: flex; align-items: center; min-width: 9rem; border: 1px solid var(--line); border-radius: .55rem; padding-inline: .6rem; background: var(--canvas); }.filter-field select { cursor: pointer; }.toolbar__count { align-self: center; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; white-space: nowrap; }.record-code { display: block; border: 0; padding: 0; color: var(--accent-strong); background: none; font: 800 .82rem/1.2 ui-monospace, monospace; cursor: pointer; }.record-code:hover { text-decoration: underline; }.record-name { display: block; color: var(--ink); font-weight: 700; }.status-pill { display: inline-flex; align-items: center; gap: .35rem; border-radius: 99px; padding: .3rem .5rem; color: var(--success); background: var(--accent-soft); font-size: .66rem; font-weight: 800; white-space: nowrap; }.status-pill i { width: .38rem; height: .38rem; border-radius: 50%; background: currentColor; }.status-pill--inactive { color: var(--support); background: var(--support-soft); }.table-action { text-align: end !important; }.icon-button { display: inline-grid; place-items: center; width: 2rem; height: 2rem; border: 1px solid var(--line); border-radius: .5rem; color: var(--accent-strong); background: transparent; cursor: pointer; }.icon-button:hover { border-color: var(--accent-strong); background: var(--accent-soft); }.pagination { margin-block-start: .8rem; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; }.pagination > div { display: flex; gap: .4rem; }.pager-button { border: 0; color: var(--accent-strong); background: transparent; font-size: .7rem; font-weight: 800; cursor: pointer; }.pager-button:disabled { color: var(--line-strong); cursor: not-allowed; }.state-card { display: flex; align-items: flex-start; gap: .8rem; border: 1px dashed var(--line-strong); border-radius: .8rem; padding: 1.35rem; background: var(--canvas); }.state-card b { color: var(--ink); font-size: .85rem; }.state-card p { margin: .3rem 0 0; color: var(--ink-muted); font-size: .75rem; line-height: 1.5; }.state-card--error { border-style: solid; border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); background: color-mix(in srgb, var(--danger) 5%, var(--surface-raised)); }.state-card--empty { min-height: 10rem; align-items: center; }.state-icon { display: grid; flex: 0 0 1.8rem; place-items: center; width: 1.8rem; height: 1.8rem; border-radius: .5rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); font-weight: 900; }.state-card--empty .state-icon { color: var(--accent-strong); background: var(--accent-soft); }.loader { width: 1.2rem; height: 1.2rem; border: 2px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: spin .8s linear infinite; }.text-button, .back-link { border: 0; padding: 0; color: var(--accent-strong); background: transparent; font-size: .74rem; font-weight: 800; cursor: pointer; }.text-button { display: block; margin-block-start: .7rem; }.detail-topline { margin-block-end: 1.25rem; }.back-link { color: var(--ink-muted); }.back-link:hover { color: var(--accent-strong); }.detail-scope { color: var(--ink-muted); font: 700 .65rem/1 ui-monospace, monospace; }.detail-heading { align-items: flex-end; margin-block-end: 1.25rem; }.detail-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(13rem, 18rem); gap: 1rem; }.detail-card, .audit-panel, .edit-card { border: 1px solid var(--line); border-radius: .8rem; padding: 1rem; background: #fcfdfb; }.detail-card--main { min-width: 0; }.detail-card--rail { background: var(--canvas); }.card-kicker { color: var(--ink-muted); font: 800 .65rem/1 ui-monospace, monospace; letter-spacing: .08em; text-transform: uppercase; }.field-read-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .9rem 1.2rem; margin-block-start: 1.3rem; }.field-read-grid > div { min-width: 0; border-block-start: 2px solid var(--line); padding-block-start: .5rem; }.field-read-grid span, .fact-stack span, .contacts-read > span { display: block; color: var(--ink-muted); font-size: .67rem; font-weight: 700; }.field-read-grid b { display: block; margin-block-start: .3rem; overflow-wrap: anywhere; color: var(--ink); font-size: .8rem; line-height: 1.4; }.field-read-grid__wide { grid-column: 1 / -1; }.fact-stack { display: grid; gap: 1rem; margin-block-start: 1.3rem; }.fact-stack b { display: block; margin-block-start: .28rem; color: var(--ink); font-size: .75rem; line-height: 1.4; }.boundary-note { margin: 1rem 0 0; border-inline-start: 3px solid var(--accent); padding-inline-start: .7rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.5; }.contacts-read { display: grid; gap: .55rem; margin-block-start: 1.35rem; border-block-start: 1px solid var(--line); padding-block-start: .85rem; }.contact-line { display: flex; justify-content: space-between; gap: 1rem; border-radius: .5rem; padding: .55rem; background: var(--canvas); }.contact-line b { font-size: .75rem; }.contact-line small { margin: 0; color: var(--ink-muted); font-size: .68rem; }.audit-panel { margin-block-start: 1rem; }.audit-heading { align-items: flex-end; margin-block-end: .7rem; }.audit-heading h3 { margin: 0; font: 800 1.1rem/1 var(--font-display); }.audit-count { display: grid; place-items: center; min-width: 1.75rem; height: 1.75rem; border-radius: 50%; color: var(--accent-strong); background: var(--accent-soft); font: 800 .7rem/1 ui-monospace, monospace; }.audit-table th, .audit-table td { padding: .65rem .45rem; font-size: .7rem; }.audit-table th { font-size: .6rem; }.muted-line { margin: 0; color: var(--ink-muted); font-size: .75rem; }.edit-card { padding: 0; overflow: hidden; }.form-section { padding: 1rem; }.form-section__heading, .contacts-edit__heading { align-items: flex-start; margin-block-end: 1rem; }.form-section__heading h3, .contacts-edit__heading h3 { margin: 0; font: 800 1.05rem/1 var(--font-display); }.form-section__heading > span { color: var(--ink-muted); font-size: .68rem; }.form-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .9rem; }.form-grid--secondary { margin-block-start: .9rem; }.form-field { display: grid; gap: .35rem; min-width: 0; }.form-field > span, .check-field span { color: var(--ink-muted); font-size: .7rem; font-weight: 800; }.form-field em { color: var(--danger); font-style: normal; }.form-field input, .form-field select, .form-field textarea { width: 100%; border: 1px solid var(--line); border-radius: .45rem; padding: .6rem .65rem; color: var(--ink); background: var(--surface-raised); font-size: .78rem; }.form-field textarea { resize: vertical; }.form-field input:focus, .form-field select:focus, .form-field textarea:focus { border-color: var(--focus); outline: 0; box-shadow: 0 0 0 3px rgb(13 138 131 / 10%); }.form-field.has-error input { border-color: var(--danger); }.form-field small { min-height: 1rem; color: var(--danger); font-size: .62rem; line-height: 1.35; }.form-field:not(.has-error) small { color: var(--ink-muted); }.check-field { display: flex; align-items: center; gap: .5rem; align-self: center; min-height: 2.4rem; border: 1px solid var(--line); border-radius: .45rem; padding: .55rem .65rem; background: var(--canvas); cursor: pointer; }.check-field input { accent-color: var(--accent-strong); }.form-field--full { margin-block-start: .9rem; }.check-row { display: flex; flex-wrap: wrap; gap: .55rem; margin-block-start: .25rem; }.contacts-edit { margin-block-start: 1.25rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }.contact-edit-row { display: grid; grid-template-columns: 1.1fr 1.1fr 1fr auto; align-items: end; gap: .6rem; margin-block-end: .6rem; }.icon-button--remove { margin-block-end: 1rem; color: var(--danger); }.form-summary, .inline-alert { margin: 1rem 1rem 0; border-radius: .55rem; padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .74rem; }.inline-alert { display: flex; gap: .5rem; justify-content: space-between; margin-block-end: 1rem; }.inline-alert span { color: var(--ink-muted); }.inline-alert--success { color: var(--success); background: var(--accent-soft); }.form-actions { justify-content: flex-end; border-block-start: 1px solid var(--line); padding: .85rem 1rem; background: var(--canvas); }.dialog-backdrop { display: grid; position: fixed; z-index: 5; inset: 0; place-items: center; padding: 1rem; background: rgb(16 39 37 / 48%); }.lifecycle-dialog { width: min(100%, 27rem); border: 1px solid var(--line); border-radius: 1rem; padding: 1.35rem; background: var(--surface-raised); box-shadow: var(--shadow-card); }.lifecycle-dialog h2 { margin: 0 0 1rem; font: 800 1.35rem/1.05 var(--font-display); }.lifecycle-dialog .form-actions { margin: 1.25rem -1.35rem -1.35rem; }.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    .term-config-block { margin-block-start: 1rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }
    .tax-engine-panel, .tax-edit-panel, .exchange-reference-panel, .exchange-edit-panel { margin-block-start: 1rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }
    .exchange-reference-result { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .6rem; margin-block-start: 1rem; border: 1px solid var(--line); border-radius: .6rem; padding: .75rem; background: var(--canvas); }
    .exchange-reference-result span, .exchange-reference-result b { display: block; }
    .exchange-reference-result span { color: var(--ink-muted); font-size: .64rem; font-weight: 700; }
    .exchange-reference-result b { margin-block-start: .25rem; color: var(--ink); font-size: .78rem; overflow-wrap: anywhere; }
    .tax-result { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .6rem; margin-block-start: 1rem; border: 1px solid var(--line); border-radius: .6rem; padding: .75rem; background: var(--canvas); }
    .tax-result span, .tax-result b { display: block; }
    .tax-result span { color: var(--ink-muted); font-size: .64rem; font-weight: 700; }
    .tax-result b { margin-block-start: .25rem; color: var(--ink); font-size: .78rem; overflow-wrap: anywhere; }
    .term-hint { margin: -.35rem 0 .85rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.45; }
    .page-header__note { margin: 0; color: var(--ink-muted); font-size: .75rem; line-height: 1.4; }
    .workspace-grid { display: grid; grid-template-columns: minmax(0, 1fr); gap: 1rem; }
    .resource-tabs-wrap { display: flex; min-width: 0; align-items: center; gap: .8rem; border: 1px solid color-mix(in srgb, var(--accent) 12%, var(--line)); border-radius: var(--radius-card); padding: .35rem .8rem; background: linear-gradient(145deg, var(--surface-glass), var(--surface-raised)); box-shadow: var(--shadow-soft); }
    .resource-tabs { display: flex; min-width: 0; flex: 1; gap: .2rem; overflow-x: auto; overscroll-behavior-inline: contain; scrollbar-width: none; mask-image: linear-gradient(90deg, transparent, #000 1.1rem, #000 calc(100% - 1.1rem), transparent); }
    .resource-tabs::-webkit-scrollbar { display: none; }
    .resource-tabs__link { position: relative; display: inline-flex; min-height: 2.7rem; flex: none; align-items: center; border-radius: .6rem; padding: .5rem .75rem; color: var(--ink-muted); font-size: .78rem; font-weight: 750; text-decoration: none; white-space: nowrap; transition: color var(--motion-fast) ease, background var(--motion-fast) ease; }
    .resource-tabs__link:hover, .resource-tabs__link.is-active { color: var(--accent-strong); background: var(--accent-soft); }
    .resource-tabs__link::after { position: absolute; inset-inline: .75rem; inset-block-end: .15rem; height: 2px; border-radius: 2px; background: var(--accent); content: ''; transform: scaleX(0); transform-origin: center; transition: transform var(--motion-fast) ease; }
    .resource-tabs__link.is-active::after { transform: scaleX(1); }
    .resource-tabs__count { flex: none; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; white-space: nowrap; }
    @media (max-width: 680px) { .resource-tabs-wrap { align-items: stretch; flex-direction: column; gap: .2rem; padding: .4rem; }.resource-tabs__count { padding-inline: .65rem; } }
    @media (prefers-reduced-motion: reduce) { .resource-tabs__link, .resource-tabs__link::after { transition: none; } }
    .term-installment-row { display: grid; grid-template-columns: 1fr 1.2fr 1fr 1fr auto; align-items: end; gap: .6rem; margin-block-end: .65rem; }
    .term-installment-row .icon-button--remove { margin-block-end: 1rem; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (max-width: 680px) { .term-installment-row { grid-template-columns: repeat(2, minmax(0, 1fr)); } .term-installment-row .icon-button--remove { margin-block-end: 0; justify-self: start; } }
    @media (max-width: 460px) { .term-installment-row { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; } }
    @media (max-width: 980px) {.workspace-grid { display: grid; grid-template-columns: minmax(0, 1fr); gap: 1rem; }.detail-grid { grid-template-columns: 1fr; }.detail-card--rail { display: none; } }
    @media (max-width: 680px) { .section-heading, .detail-heading, .toolbar { align-items: stretch; flex-direction: column; }.section-heading__actions, .detail-heading__actions { justify-content: flex-start; }.toolbar__count { align-self: flex-start; }.form-grid { grid-template-columns: 1fr; }.contact-edit-row { grid-template-columns: 1fr 1fr; }.contact-edit-row .icon-button { margin-block-end: 0; }.field-read-grid { grid-template-columns: 1fr; }.field-read-grid__wide { grid-column: auto; }.tax-result, .exchange-reference-result { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 460px) {.list-view, .detail-view { padding: .8rem; }.button span { display: none; }.contact-edit-row { grid-template-columns: 1fr; }.contact-edit-row .icon-button { justify-self: start; }.contact-line { align-items: flex-start; flex-direction: column; gap: .25rem; }.form-actions { flex-wrap: wrap; }.form-actions .button { flex: 1; } }
  
    .master-data__note { margin: -.8rem 0 0; color: var(--ink-muted); font-size: .75rem; }
`,
})
export class MasterDataWorkspaceComponent implements OnInit {
  readonly definitions = RESOURCE_DEFINITIONS;
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  private readonly data = inject(MasterDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly resource = signal<MasterDataResourceKey>('categories');
  readonly currentDefinition = computed(() => resourceDefinition(this.resource()));
  readonly records = signal<MasterDataRecord[]>([]);
  readonly loading = signal(false);
  readonly listError = signal<SafeUiError | null>(null);
  readonly filterQuery = signal('');
  readonly statusFilter = signal<StatusFilter>('all');
  readonly pageSize = 8;
  readonly detailMode = signal<DetailMode | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<SafeUiError | null>(null);
  readonly selectedRecord = signal<MasterDataRecord | null>(null);
  readonly auditEntries = signal<MasterDataAuditEntry[]>([]);
  readonly auditLoading = signal(false);
  readonly auditError = signal<SafeUiError | null>(null);
  readonly mutationError = signal<SafeUiError | null>(null);
  readonly formError = signal(false);
  readonly fieldErrors = signal<ReadonlySet<string>>(new Set());
  readonly formNotice = signal<string | null>(null);
  readonly saving = signal(false);
  readonly lifecycleAction = signal<LifecycleAction | null>(null);
  readonly lifecycleReason = signal('');
  readonly lifecycleSaving = signal(false);
  readonly categoryChoices = signal<CategoryRecord[]>([]);
  readonly unitChoices = signal<UnitOfMeasureRecord[]>([]);
  readonly currencyChoices = signal<CurrencyRecord[]>([]);
  readonly referenceLoadFailed = signal(false);
  readonly taxCalculation = signal<TaxCalculationResponse | null>(null);
  readonly taxCalculationLoading = signal(false);
  readonly taxCalculationError = signal<SafeUiError | null>(null);
  readonly exchangeReference = signal<ExchangeRateReferenceResponse | null>(null);
  readonly exchangeReferenceDate = signal(new Date().toISOString().slice(0, 10));
  readonly exchangeReferenceLoading = signal(false);
  readonly exchangeReferenceError = signal<SafeUiError | null>(null);
  readonly filteredRecords = computed(() => {
    const query = this.filterQuery().trim().toLocaleLowerCase();
    const status = this.statusFilter();
    return this.records().filter((record) => {
      const matchesStatus = status === 'all' || record.lifecycleState === status;
      const searchable = [this.recordCode(record), this.recordName(record), this.recordSecondary(record), record.id].join(' ').toLocaleLowerCase();
      return matchesStatus && (!query || searchable.includes(query));
    });
  });
  readonly recordColumns = computed<DataGridColumn<MasterDataRecord>[]>(() => [
    { key: 'code', label: this.language.text('code'), value: (record) => this.recordCode(record), secondaryText: (record) => this.recordSecondary(record), link: (record) => `/app/master-data/${this.resource()}/${record.id}`, filter: 'text' },
    { key: 'name', label: this.language.text('englishName'), value: (record) => this.recordName(record), secondaryText: (record) => record.id, filter: 'text' },
    { key: 'lifecycle', label: this.language.text('lifecycle'), value: (record) => record.lifecycleState, display: (record) => this.statusLabel(record.lifecycleState), badge: true, filter: 'select', filterOptions: [{ value: 'Active', label: this.language.text('activeStatus') }, { value: 'Inactive', label: this.language.text('inactiveStatus') }] },
  ]);
  draft: MasterDataDraft = this.emptyDraft('categories');
  taxCalculationDraft: TaxCalculationRequest = this.emptyTaxCalculationDraft();
  private loadSequence = 0;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const candidate = params.get('resource');
      const nextResource: MasterDataResourceKey = candidate && isMasterDataResourceKey(candidate) ? candidate : 'categories';
      const id = params.get('id');
      this.resource.set(nextResource);
      this.resetTaxCalculation();
      this.filterQuery.set('');
      this.statusFilter.set('all');
      this.loadList(nextResource);
      if (id === 'new') {
        this.prepareCreate(nextResource);
      } else if (id) {
        void this.loadDetail(nextResource, id);
      } else {
        this.detailMode.set(null);
        this.selectedRecord.set(null);
        this.detailError.set(null);
      }
    });
  }

  ngOnInit(): void {
    const candidate = this.route.snapshot.paramMap.get('resource');
    if (!candidate || !isMasterDataResourceKey(candidate)) {
      void this.router.navigate(['/app/master-data/categories']);
    }
  }

  loadList(resource: MasterDataResourceKey = this.resource()): void {
    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.listError.set(null);
    void firstValueFrom(this.data.list(resource))
      .then((records) => {
        if (sequence !== this.loadSequence) return;
        this.records.set(records ?? []);
        if (resource === 'products' || resource === 'exchange-rates') void this.loadReferences(sequence, resource);
      })
      .catch((error: unknown) => {
        if (sequence === this.loadSequence) this.listError.set(toSafeUiError(error));
      })
      .finally(() => {
        if (sequence === this.loadSequence) this.loading.set(false);
      });
  }

  private async loadReferences(sequence: number, resource: MasterDataResourceKey): Promise<void> {
    this.referenceLoadFailed.set(false);
    try {
      const [categories, units, currencies] = await Promise.all([
        resource === 'products' ? firstValueFrom(this.data.list('categories')) : Promise.resolve([] as MasterDataRecord[]),
        resource === 'products' ? firstValueFrom(this.data.list('units')) : Promise.resolve([] as MasterDataRecord[]),
        resource === 'exchange-rates' ? firstValueFrom(this.data.list('currencies')) : Promise.resolve([] as MasterDataRecord[]),
      ]);
      if (sequence !== this.loadSequence) return;
      this.categoryChoices.set((categories ?? []).filter((record): record is CategoryRecord => this.isCategory(record)));
      this.unitChoices.set((units ?? []).filter((record): record is UnitOfMeasureRecord => this.isUnit(record)));
      this.currencyChoices.set((currencies ?? []).filter((record): record is CurrencyRecord => this.isCurrency(record)));
    } catch {
      if (sequence === this.loadSequence) this.referenceLoadFailed.set(true);
    }
  }

  openRecord(id: string): void {
    void this.router.navigate(['/app/master-data', this.resource(), id]);
  }

  startCreate(): void {
    if (!this.canMutate()) return;
    void this.router.navigate(['/app/master-data', this.resource(), 'new']);
  }

  private prepareCreate(resource: MasterDataResourceKey): void {
    this.resetTaxCalculation();
    this.detailMode.set('create');
    this.selectedRecord.set(null);
    this.draft = this.emptyDraft(resource);
    this.detailLoading.set(false);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.formNotice.set(null);
    this.auditEntries.set([]);
    this.resetExchangeReference();
  }

  private async loadDetail(resource: MasterDataResourceKey, id: string): Promise<void> {
    this.resetTaxCalculation();
    this.detailMode.set('view');
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.formNotice.set(null);
    this.resetExchangeReference();
    try {
      const record = await firstValueFrom(this.data.get(resource, id));
      if (resource !== this.resource()) return;
      this.selectedRecord.set(record);
      this.replaceRecord(record);
      await this.loadAudit(resource, id);
    } catch (error: unknown) {
      this.detailError.set(toSafeUiError(error));
    } finally {
      this.detailLoading.set(false);
    }
  }

  private async loadAudit(resource: MasterDataResourceKey, id: string): Promise<void> {
    this.auditLoading.set(true);
    this.auditError.set(null);
    try {
      this.auditEntries.set((await firstValueFrom(this.data.audit(resource, id))) ?? []);
    } catch (error: unknown) {
      this.auditEntries.set([]);
      this.auditError.set(toSafeUiError(error));
    } finally {
      this.auditLoading.set(false);
    }
  }

  reloadDetail(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') void this.loadDetail(this.resource(), id);
  }

  backToList(): void {
    void this.router.navigate(['/app/master-data', this.resource()]);
  }

  startEdit(): void {
    const record = this.selectedRecord();
    if (!record || !this.canMutate()) return;
    this.draft = this.toDraft(this.resource(), record);
    this.detailMode.set('edit');
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.mutationError.set(null);
    this.formNotice.set(null);
  }

  cancelEdit(): void {
    if (this.detailMode() === 'create') {
      this.backToList();
      return;
    }
    const record = this.selectedRecord();
    if (record) this.draft = this.toDraft(this.resource(), record);
    this.detailMode.set('view');
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.mutationError.set(null);
  }

  async save(): Promise<void> {
    this.formNotice.set(null);
    this.mutationError.set(null);
    if (!this.validateDraft()) {
      this.formError.set(true);
      return;
    }
    const resource = this.resource();
    const existing = this.selectedRecord();
    const mode = this.detailMode();
    this.saving.set(true);
    try {
      const payload = this.toPayload(resource, this.draft);
      const saved = mode === 'create'
        ? await this.data.create(resource, payload)
        : existing
          ? await this.data.edit(resource, existing.id, payload, existing.version)
          : null;
      if (!saved) return;
      this.selectedRecord.set(saved);
      this.replaceRecord(saved);
      this.resetTaxCalculation();
      this.draft = this.toDraft(resource, saved);
      this.detailMode.set('view');
      this.formError.set(false);
      this.formNotice.set(mode === 'create' ? this.language.text('recordCreated') : this.language.text('recordSaved'));
      if (mode === 'create') await this.router.navigate(['/app/master-data', resource, saved.id]);
      await this.loadAudit(resource, saved.id);
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  openLifecycle(action: LifecycleAction): void {
    if (!this.selectedRecord() || !this.canMutate()) return;
    this.lifecycleAction.set(action);
    this.lifecycleReason.set('');
  }

  closeLifecycle(): void {
    if (this.lifecycleSaving()) return;
    this.lifecycleAction.set(null);
    this.lifecycleReason.set('');
  }

  async confirmLifecycle(): Promise<void> {
    const record = this.selectedRecord();
    const action = this.lifecycleAction();
    if (!record || !action || (action === 'deactivate' && this.requiresLifecycleReason() && !this.lifecycleReason().trim())) return;
    this.lifecycleSaving.set(true);
    this.mutationError.set(null);
    try {
      const updated = await this.data.lifecycle(this.resource(), record.id, action, record.version, this.lifecycleReason());
      this.selectedRecord.set(updated);
      this.replaceRecord(updated);
      this.detailMode.set('view');
      this.formNotice.set(this.language.text('lifecycleSaved'));
      this.closeLifecycle();
      await this.loadAudit(this.resource(), updated.id);
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.lifecycleSaving.set(false);
    }
  }

  canMutate(): boolean {
    return this.auth.status() === 'authenticated' && this.auth.session()?.selectedContextId !== null && this.auth.session()?.selectedContextId !== undefined;
  }

  requiresLifecycleReason(): boolean {
    return this.resource() === 'products' || this.resource() === 'suppliers' || this.resource() === 'customers';
  }

  onSearch(event: Event): void {
    this.filterQuery.set((event.target as HTMLInputElement).value);
  }

  onStatusChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.statusFilter.set(value === 'Active' || value === 'Inactive' ? value : 'all');
  }

  onLifecycleReason(event: Event): void {
    this.lifecycleReason.set((event.target as HTMLTextAreaElement).value);
  }

  gridRowActions(): readonly { key: string; label: string }[] { return [{ key: 'view', label: this.language.text('viewRecord') }]; }

  onGridAction(event: { action: string; row: MasterDataRecord }): void { if (event.action === 'view') this.openRecord(event.row.id); }

  recordCode(record: MasterDataRecord | null): string {
    if (!record) return this.language.text('createRecord');
    if ('sku' in record) return record.sku;
    if (this.isExchangeRate(record)) return `${record.sourceCurrencyCode} → ${record.targetCurrencyCode}`;
    return record.code;
  }

  categoryRecord(): CategoryRecord | null {
    const record = this.selectedRecord();
    return record && this.isCategory(record) ? record : null;
  }

  unitRecord(): UnitOfMeasureRecord | null {
    const record = this.selectedRecord();
    return record && this.isUnit(record) ? record : null;
  }

  currencyRecord(): CurrencyRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'currencies' && 'revision' in record ? record : null;
  }

  paymentTermRecord(): PaymentTermRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'payment-terms' && 'versions' in record && 'code' in record ? record : null;
  }

  taxRecord(): TaxRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'taxes' && 'rateVersions' in record ? record : null;
  }

  exchangeRateRecord(): ExchangeRateRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'exchange-rates' && this.isExchangeRate(record) ? record : null;
  }

  exchangeRateCurrentVersion(record: ExchangeRateRecord): ExchangeRateRecord['versions'][number] | null {
    return record.versions.find((version) => version.versionNumber === record.currentVersionNumber)
      ?? record.versions[record.versions.length - 1]
      ?? null;
  }

  exchangeRateHistoryLabel(record: ExchangeRateRecord): string {
    return record.versions
      .map((version) => `${version.effectiveFrom} - ${version.effectiveTo ?? 'current'}`)
      .join(' · ');
  }

  exchangeRateDraft(): ExchangeRateDraft { return this.draft as ExchangeRateDraft; }

  setExchangeReferenceDate(value: string): void {
    this.exchangeReferenceDate.set(value);
    this.exchangeReference.set(null);
    this.exchangeReferenceError.set(null);
  }

  async resolveExchangeReference(): Promise<void> {
    const record = this.exchangeRateRecord();
    if (!record || !this.exchangeReferenceDate()) return;
    this.exchangeReferenceLoading.set(true);
    this.exchangeReferenceError.set(null);
    try {
      this.exchangeReference.set(await firstValueFrom(this.data.referenceExchangeRate(record.id, this.exchangeReferenceDate())));
    } catch (error: unknown) {
      this.exchangeReference.set(null);
      this.exchangeReferenceError.set(toSafeUiError(error));
    } finally {
      this.exchangeReferenceLoading.set(false);
    }
  }

  taxCurrentVersion(record: TaxRecord): TaxRateVersionRecord | null {
    return record.rateVersions.find((version) => version.versionNumber === record.currentVersionNumber)
      ?? record.rateVersions[record.rateVersions.length - 1]
      ?? null;
  }

  taxHistoryLabel(record: TaxRecord): string {
    return record.rateVersions
      .map((version) => `${version.effectiveFrom} - ${version.effectiveTo ?? 'current'}`)
      .join(' · ');
  }

  taxDirectionLabel(direction: string): string {
    if (direction === 'Purchase') return this.language.text('purchase');
    if (direction === 'Both') return this.language.text('bothDirections');
    return this.language.text('sales');
  }

  setTaxCalculationField(field: keyof TaxCalculationRequest, value: string | number): void {
    const numericField = field === 'taxableBase' || field === 'roundingScale';
    const normalized = numericField ? Number(value) : value;
    this.taxCalculationDraft = { ...this.taxCalculationDraft, [field]: normalized } as TaxCalculationRequest;
  }

  async calculateTax(): Promise<void> {
    const record = this.taxRecord();
    if (!record) return;
    this.taxCalculationLoading.set(true);
    this.taxCalculationError.set(null);
    try {
      this.taxCalculation.set(await firstValueFrom(this.data.calculateTax(record.id, this.taxCalculationDraft)));
    } catch (error: unknown) {
      this.taxCalculation.set(null);
      this.taxCalculationError.set(toSafeUiError(error));
    } finally {
      this.taxCalculationLoading.set(false);
    }
  }

  paymentTermCurrentVersion(record: PaymentTermRecord): PaymentTermRecord['versions'][number] | null {
    return record.versions.find((version) => version.versionNumber === record.currentVersionNumber)
      ?? record.versions[record.versions.length - 1]
      ?? null;
  }

  paymentTermHistoryLabel(record: PaymentTermRecord): string {
    return record.versions
      .map((version) => `${version.effectiveFrom} - ${version.effectiveTo ?? 'current'}`)
      .join(' · ');
  }

  paymentTermDraft(): PaymentTermDraft {
    return this.draft as PaymentTermDraft;
  }

  taxDraft(): TaxDraft {
    return this.draft as TaxDraft;
  }

  installmentTotal(draft: PaymentTermDraft): number {
    return Math.round(draft.installments.reduce((total, installment) => total + installment.percentage, 0) * 100) / 100;
  }

  setInstallmentField(index: number, field: keyof PaymentTermInstallmentDraft, value: number | string): void {
    const installment = this.paymentTermDraft().installments[index];
    const numericValue = typeof value === 'number' ? value : Number(value);
    if (installment && Number.isFinite(numericValue)) installment[field] = numericValue;
  }

  addInstallment(): void {
    const draft = this.paymentTermDraft();
    const nextSequence = draft.installments.length + 1;
    draft.installments.push({
      sequence: nextSequence,
      percentage: Math.max(0, Math.round((100 - this.installmentTotal(draft)) * 100) / 100),
      days: 30,
      months: 0,
    });
  }

  removeInstallment(index: number): void {
    const installments = this.paymentTermDraft().installments;
    if (installments.length > 1) installments.splice(index, 1);
  }

  productRecord(): ProductRecord | null {
    const record = this.selectedRecord();
    return record && 'sku' in record ? record : null;
  }

  supplierRecord(): SupplierRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'suppliers' && 'registrationReference' in record ? record : null;
  }

  customerRecord(): CustomerRecord | null {
    const record = this.selectedRecord();
    return record && this.resource() === 'customers' && 'contacts' in record && !('registrationReference' in record) ? record : null;
  }

  recordName(record: MasterDataRecord | null): string {
    if (!record) return this.language.text('createRecord');
    if (this.isExchangeRate(record)) return `${record.sourceCurrencyCode} → ${record.targetCurrencyCode}`;
    if ('sku' in record) return this.localizedName(record.englishName, record.arabicName);
    if ('englishLegalName' in record) return this.localizedPartyName(record.englishLegalName, record.arabicLegalName);
    return this.localizedName(record.englishName, record.arabicName);
  }

  recordSecondary(record: MasterDataRecord): string {
    if ('parentCategoryId' in record) return record.parentCategoryId ? this.referenceName('categories', record.parentCategoryId) : this.language.text('noParent');
    if ('baseUnitOfMeasureId' in record) return `Category ${record.categoryId}`;
    if ('registrationReference' in record) return record.registrationReference || `${record.contacts.length} ${this.language.text('contacts')}`;
    if ('contacts' in record) return `${record.contacts.length} ${this.language.text('contacts')}`;
    if ('revision' in record) return `Revision ${record.revision}`;
    if (this.isExchangeRate(record)) {
      const current = this.exchangeRateCurrentVersion(record);
      return current ? `${current.rate} · v${current.versionNumber}` : this.language.text('emptyValue');
    }
    if ('rateVersions' in record) {
      const current = this.taxCurrentVersion(record);
      return `${record.categoryCode} · ${current ? `${current.ratePercentage}% · v${current.versionNumber}` : this.language.text('emptyValue')}`;
    }
    if ('versions' in record) {
      const current = this.paymentTermCurrentVersion(record);
      return current ? `v${current.versionNumber} · ${current.baseDateRule}` : this.language.text('emptyValue');
    }
    return record.id;
  }

  statusLabel(status: string): string {
    if (status === 'Active') return this.language.text('activeStatus');
    if (status === 'Inactive') return this.language.text('inactiveStatus');
    return this.language.text('unknownState');
  }

  isActive(record: MasterDataRecord): boolean { return record.lifecycleState === 'Active'; }
  valueOrEmpty(value: string | null | undefined): string { return value?.trim() || this.language.text('emptyValue'); }
  versionLabel(version: string): string { return version.length > 12 ? `${version.slice(0, 10)}…` : version; }

  referenceName(resource: 'categories' | 'units' | 'currencies', id: string): string {
    const found = resource === 'categories'
      ? this.categoryChoices().find((item) => item.id === id)
      : resource === 'units'
        ? this.unitChoices().find((item) => item.id === id)
        : this.currencyChoices().find((item) => item.id === id);
    return found ? `${this.recordCode(found)} · ${this.recordName(found)}` : id;
  }

  currencyOptionLabel(currency: CurrencyRecord): string {
    return `${currency.code} · ${this.recordName(currency)}`;
  }

  draftText(field: string): string {
    const value = this.draftValue(field);
    return typeof value === 'string' ? value : '';
  }

  draftBoolean(field: string): boolean {
    const value = this.draftValue(field);
    return typeof value === 'boolean' ? value : false;
  }

  setDraftField(field: string, value: string | boolean | number | null): void {
    const draft = this.draft as unknown as Record<string, string | number | boolean | null | string[]>;
    draft[field] = value;
  }

  contactsDraft(): ContactDraft[] {
    return 'contacts' in this.draft ? (this.draft as PartyDraft).contacts : [];
  }

  contactValue(index: number, field: keyof ContactDraft): string { return this.contactsDraft()[index]?.[field] ?? ''; }
  setContactValue(index: number, field: keyof ContactDraft, value: string): void { const contact = this.contactsDraft()[index]; if (contact) contact[field] = value; }
  addContact(): void { if ('contacts' in this.draft) (this.draft as PartyDraft).contacts.push({ name: '', email: '', phone: '' }); }
  removeContact(index: number): void { if ('contacts' in this.draft) (this.draft as PartyDraft).contacts.splice(index, 1); }
  productDraft(): ProductDraft { return this.draft as ProductDraft; }
  invalid(field: string): boolean { return this.fieldErrors().has(field); }

  errorMessage(error: SafeUiError | null): string {
    if (!error) return this.language.text('requestError');
    if (error.code === 'access_denied' || error.code === 'authentication_failed') return this.language.text('accessUnavailable');
    if (error.code === 'concurrency_conflict') return this.language.text('conflictTitle');
    if (error.code === 'validation_failed') return this.language.text('validationSummary');
    if (error.code === 'network_error') return this.language.text('networkError');
    return this.language.text('requestError');
  }

  private validateDraft(): boolean {
    const errors = new Set<string>();
    const text = (field: string) => this.draftText(field).trim();
    if (this.resource() === 'exchange-rates') {
      const exchangeRate = this.exchangeRateDraft();
      if (!exchangeRate.sourceCurrencyId) errors.add('sourceCurrencyId');
      if (!exchangeRate.targetCurrencyId) errors.add('targetCurrencyId');
      if (exchangeRate.sourceCurrencyId && exchangeRate.sourceCurrencyId === exchangeRate.targetCurrencyId) { errors.add('sourceCurrencyId'); errors.add('targetCurrencyId'); }
      if (!exchangeRate.effectiveFrom) errors.add('effectiveFrom');
      if (exchangeRate.effectiveTo && exchangeRate.effectiveFrom && exchangeRate.effectiveTo < exchangeRate.effectiveFrom) errors.add('effectiveTo');
      if (!Number.isFinite(Number(exchangeRate.rate)) || Number(exchangeRate.rate) <= 0) errors.add('rate');
      if (!Number.isInteger(Number(exchangeRate.rateScale)) || Number(exchangeRate.rateScale) < 0 || Number(exchangeRate.rateScale) > 12) errors.add('rateScale');
    } else if (this.resource() === 'taxes') {
      const tax = this.taxDraft();
      if (!text('code')) errors.add('code');
      if (!text('categoryCode')) errors.add('categoryCode');
      if (!text('englishName') && !text('arabicName')) { errors.add('englishName'); errors.add('arabicName'); }
      if (!tax.effectiveFrom) errors.add('effectiveFrom');
      const rate = Number(tax.ratePercentage);
      if (!Number.isFinite(rate) || rate < 0 || rate > 100) errors.add('ratePercentage');
      if (tax.effectiveTo && tax.effectiveFrom && tax.effectiveTo < tax.effectiveFrom) errors.add('effectiveTo');
    } else if (this.resource() === 'products') {
      if (!text('sku')) errors.add('sku');
      if (!text('englishName') && !text('arabicName')) { errors.add('englishName'); errors.add('arabicName'); }
      if (!text('categoryId')) errors.add('categoryId');
      if (!text('baseUnitOfMeasureId')) errors.add('baseUnitOfMeasureId');
    } else {
      if (!text('code')) errors.add('code');
      const hasName = this.resource() === 'suppliers' || this.resource() === 'customers'
        ? Boolean(text('englishLegalName') || text('arabicLegalName'))
        : Boolean(text('englishName') || text('arabicName'));
      if (!hasName) { errors.add(this.resource() === 'suppliers' || this.resource() === 'customers' ? 'englishLegalName' : 'englishName'); }
      if (this.resource() === 'payment-terms') {
        const paymentTerm = this.paymentTermDraft();
        if (!paymentTerm.effectiveFrom) errors.add('effectiveFrom');
        if (paymentTerm.scheduleMode === 'Installments' && paymentTerm.installments.length === 0) errors.add('installments');
      }
    }
    this.fieldErrors.set(errors);
    return errors.size === 0;
  }

  private draftValue(field: string): string | number | boolean | null | string[] {
    const draft = this.draft as unknown as Record<string, string | number | boolean | null | string[]>;
    return draft[field] ?? '';
  }

  private emptyTaxCalculationDraft(): TaxCalculationRequest {
    return {
      effectiveOn: new Date().toISOString().slice(0, 10),
      transactionDirection: 'Sales',
      taxableBase: 100,
      currencyCode: 'SAR',
      roundingScale: 2,
      roundingMode: 'AwayFromZero',
      sourceLineage: 'master-data-tax-preview',
    };
  }

  private resetTaxCalculation(): void {
    this.taxCalculation.set(null);
    this.taxCalculationError.set(null);
    this.taxCalculationLoading.set(false);
    this.taxCalculationDraft = this.emptyTaxCalculationDraft();
  }

  private emptyDraft(resource: MasterDataResourceKey): MasterDataDraft {
    if (resource === 'categories') return { code: '', englishName: '', arabicName: '', parentCategoryId: '', trackingDefaultEnabled: false } satisfies CategoryDraft;
    if (resource === 'units') return { code: '', englishName: '', arabicName: '' } satisfies UnitDraft;
    if (resource === 'products') return { sku: '', englishName: '', arabicName: '', description: '', categoryId: '', baseUnitOfMeasureId: '', barcodes: '', trackingEnabledOverride: 'inherit', isSellable: false, isPurchasable: false, isInventoryRelevant: false } satisfies ProductDraft;
    if (resource === 'currencies') return { code: '', englishName: '', arabicName: '' } satisfies CurrencyDraft;
    if (resource === 'payment-terms') return { code: '', englishName: '', arabicName: '', effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: '', baseDateRule: 'InvoiceDate', scheduleMode: 'SingleDueDate', dueOffsetDays: 30, dueOffsetMonths: 0, installments: [{ sequence: 1, percentage: 100, days: 30, months: 0 }], earlySettlementDiscountEnabled: false, earlySettlementDiscountPercentage: 0, earlySettlementDiscountDays: 0, earlySettlementDiscountMonths: 0 } satisfies PaymentTermDraft;
    if (resource === 'taxes') return { code: '', categoryCode: '', categoryEnglishName: '', categoryArabicName: '', englishName: '', arabicName: '', applicability: 'Sales', effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: '', ratePercentage: 15 } satisfies TaxDraft;
    if (resource === 'exchange-rates') return { sourceCurrencyId: '', targetCurrencyId: '', effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: '', rate: 1, rateScale: 6, provenance: 'Manual', sourceNotes: '' } satisfies ExchangeRateDraft;
    return { code: '', englishLegalName: '', arabicLegalName: '', englishTradingName: '', arabicTradingName: '', registrationReference: '', contacts: [] } satisfies PartyDraft;
  }

  private toDraft(resource: MasterDataResourceKey, record: MasterDataRecord): MasterDataDraft {
    if (resource === 'categories') { const value = record as CategoryRecord; return { code: value.code, englishName: value.englishName ?? '', arabicName: value.arabicName ?? '', parentCategoryId: value.parentCategoryId ?? '', trackingDefaultEnabled: value.trackingDefaultEnabled }; }
    if (resource === 'units') { const value = record as UnitOfMeasureRecord; return { code: value.code, englishName: value.englishName ?? '', arabicName: value.arabicName ?? '' }; }
    if (resource === 'products') { const value = record as ProductRecord; return { sku: value.sku, englishName: value.englishName ?? '', arabicName: value.arabicName ?? '', description: value.description ?? '', categoryId: value.categoryId, baseUnitOfMeasureId: value.baseUnitOfMeasureId, barcodes: value.barcodes.map((barcode) => barcode.value).join('\n'), trackingEnabledOverride: value.trackingEnabledOverride === null ? 'inherit' : value.trackingEnabledOverride ? 'enabled' : 'disabled', isSellable: value.isSellable, isPurchasable: value.isPurchasable, isInventoryRelevant: value.isInventoryRelevant }; }
    if (resource === 'currencies') { const value = record as CurrencyRecord; return { code: value.code, englishName: value.englishName ?? '', arabicName: value.arabicName ?? '' }; }
    if (resource === 'payment-terms') { const value = record as PaymentTermRecord; const current = this.paymentTermCurrentVersion(value); return { code: value.code, englishName: current?.englishName ?? value.englishName ?? '', arabicName: current?.arabicName ?? value.arabicName ?? '', effectiveFrom: current?.effectiveFrom ?? new Date().toISOString().slice(0, 10), effectiveTo: current?.effectiveTo ?? '', baseDateRule: (current?.baseDateRule ?? 'InvoiceDate') as PaymentTermDraft['baseDateRule'], scheduleMode: (current?.scheduleMode ?? 'SingleDueDate') as PaymentTermDraft['scheduleMode'], dueOffsetDays: current?.dueOffsetDays ?? 30, dueOffsetMonths: current?.dueOffsetMonths ?? 0, installments: current?.installments.map((installment) => ({ sequence: installment.sequence, percentage: installment.percentage, days: installment.days, months: installment.months })) ?? [{ sequence: 1, percentage: 100, days: 30, months: 0 }], earlySettlementDiscountEnabled: current?.earlySettlementDiscountEnabled ?? false, earlySettlementDiscountPercentage: current?.earlySettlementDiscountPercentage ?? 0, earlySettlementDiscountDays: current?.earlySettlementDiscountDays ?? 0, earlySettlementDiscountMonths: current?.earlySettlementDiscountMonths ?? 0 }; }
    if (resource === 'taxes') { const value = record as TaxRecord; const current = this.taxCurrentVersion(value); return { code: value.code, categoryCode: value.categoryCode, categoryEnglishName: value.categoryEnglishName ?? '', categoryArabicName: value.categoryArabicName ?? '', englishName: value.englishName ?? '', arabicName: value.arabicName ?? '', applicability: value.applicability as TaxDraft['applicability'], effectiveFrom: current?.effectiveFrom ?? new Date().toISOString().slice(0, 10), effectiveTo: current?.effectiveTo ?? '', ratePercentage: current?.ratePercentage ?? 0 } satisfies TaxDraft; }
    if (resource === 'exchange-rates') { const value = record as ExchangeRateRecord; const current = this.exchangeRateCurrentVersion(value); return { sourceCurrencyId: value.sourceCurrencyId, targetCurrencyId: value.targetCurrencyId, effectiveFrom: current?.effectiveFrom ?? new Date().toISOString().slice(0, 10), effectiveTo: current?.effectiveTo ?? '', rate: current?.rate ?? 1, rateScale: current?.rateScale ?? 6, provenance: (current?.provenance === 'Configured' ? 'Configured' : 'Manual'), sourceNotes: current?.sourceNotes ?? '' } satisfies ExchangeRateDraft; }
    const value = record as SupplierRecord | CustomerRecord;
    return { code: value.code, englishLegalName: value.englishLegalName ?? '', arabicLegalName: value.arabicLegalName ?? '', englishTradingName: value.englishTradingName ?? '', arabicTradingName: value.arabicTradingName ?? '', registrationReference: 'registrationReference' in value ? value.registrationReference ?? '' : '', contacts: value.contacts.map((contact) => ({ name: contact.name, email: contact.email ?? '', phone: contact.phone ?? '' })) };
  }

  private toPayload(resource: MasterDataResourceKey, draft: MasterDataDraft): MasterDataWritePayload {
    if (resource === 'categories') { const value = draft as CategoryDraft; return { code: value.code.trim(), englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null, parentCategoryId: value.parentCategoryId.trim() || null, trackingDefaultEnabled: value.trackingDefaultEnabled }; }
    if (resource === 'units') { const value = draft as UnitDraft; return { code: value.code.trim(), englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null }; }
    if (resource === 'products') { const value = draft as ProductDraft; return { sku: value.sku.trim(), englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null, description: value.description.trim() || null, categoryId: value.categoryId.trim(), baseUnitOfMeasureId: value.baseUnitOfMeasureId.trim(), barcodes: value.barcodes.split(/\r?\n/).map((barcode) => barcode.trim()).filter(Boolean), trackingEnabledOverride: value.trackingEnabledOverride === 'inherit' ? null : value.trackingEnabledOverride === 'enabled', isSellable: value.isSellable, isPurchasable: value.isPurchasable, isInventoryRelevant: value.isInventoryRelevant }; }
    if (resource === 'currencies') { const value = draft as CurrencyDraft; return { code: value.code.trim(), englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null }; }
    if (resource === 'payment-terms') { const value = draft as PaymentTermDraft; return { code: value.code.trim(), englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null, effectiveFrom: value.effectiveFrom, effectiveTo: value.effectiveTo || null, baseDateRule: value.baseDateRule, scheduleMode: value.scheduleMode, dueOffset: { days: Number(value.dueOffsetDays) || 0, months: Number(value.dueOffsetMonths) || 0 }, installments: value.scheduleMode === 'Installments' ? value.installments.map((installment) => ({ sequence: Number(installment.sequence), percentage: Number(installment.percentage), days: Number(installment.days) || 0, months: Number(installment.months) || 0 })) : [], earlySettlementDiscount: { enabled: value.earlySettlementDiscountEnabled, percentage: value.earlySettlementDiscountEnabled ? Number(value.earlySettlementDiscountPercentage) || 0 : null, days: value.earlySettlementDiscountEnabled ? Number(value.earlySettlementDiscountDays) || 0 : 0, months: value.earlySettlementDiscountEnabled ? Number(value.earlySettlementDiscountMonths) || 0 : 0 } }; }
    if (resource === 'taxes') { const value = draft as TaxDraft; return { code: value.code.trim(), categoryCode: value.categoryCode.trim(), categoryEnglishName: value.categoryEnglishName.trim() || null, categoryArabicName: value.categoryArabicName.trim() || null, englishName: value.englishName.trim() || null, arabicName: value.arabicName.trim() || null, applicability: value.applicability, rateVersion: { effectiveFrom: value.effectiveFrom, effectiveTo: value.effectiveTo || null, ratePercentage: Number(value.ratePercentage) } }; }
    if (resource === 'exchange-rates') { const value = draft as ExchangeRateDraft; return { sourceCurrencyId: value.sourceCurrencyId, targetCurrencyId: value.targetCurrencyId, effectiveFrom: value.effectiveFrom, effectiveTo: value.effectiveTo || null, rate: Number(value.rate), rateScale: Number(value.rateScale), provenance: value.provenance, sourceNotes: value.sourceNotes.trim() || null }; }
    const value = draft as PartyDraft;
    const payload = { code: value.code.trim(), englishLegalName: value.englishLegalName.trim() || null, arabicLegalName: value.arabicLegalName.trim() || null, englishTradingName: value.englishTradingName.trim() || null, arabicTradingName: value.arabicTradingName.trim() || null, contacts: value.contacts.map((contact) => ({ name: contact.name.trim(), email: contact.email.trim() || null, phone: contact.phone.trim() || null })) };
    return resource === 'suppliers' ? { ...payload, registrationReference: value.registrationReference.trim() || null } : payload;
  }

  private replaceRecord(record: MasterDataRecord): void {
    const current = this.records();
    const index = current.findIndex((item) => item.id === record.id);
    if (index < 0) this.records.set([record, ...current]);
    else this.records.set(current.map((item, itemIndex) => itemIndex === index ? record : item));
  }

  private localizedName(english: string | null, arabic: string | null): string {
    return this.language.language() === 'ar' ? (arabic || english || this.language.text('emptyValue')) : (english || arabic || this.language.text('emptyValue'));
  }

  localizedPartyName(english: string | null, arabic: string | null): string { return this.localizedName(english, arabic); }
  private isCurrency(record: MasterDataRecord): record is CurrencyRecord { return 'revision' in record; }
  private isExchangeRate(record: MasterDataRecord): record is ExchangeRateRecord { return 'sourceCurrencyId' in record && 'targetCurrencyId' in record && 'versions' in record && !('code' in record); }
  private isCategory(record: MasterDataRecord): record is CategoryRecord { return 'parentCategoryId' in record; }
  private isUnit(record: MasterDataRecord): record is UnitOfMeasureRecord { return !('parentCategoryId' in record) && !('sku' in record) && !('contacts' in record) && !('revision' in record) && !('versions' in record); }

  private resetExchangeReference(): void {
    this.exchangeReference.set(null);
    this.exchangeReferenceDate.set(new Date().toISOString().slice(0, 10));
    this.exchangeReferenceLoading.set(false);
    this.exchangeReferenceError.set(null);
  }
}
