import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { CurrencyRecord, CustomerRecord, MasterDataAuditEntry, MasterDataRecord, ProductRecord, UnitOfMeasureRecord } from './master-data.models';
import { MasterDataService } from './master-data.service';
import {
  OrganizationScopeKind,
  PriceListPriceVersionRecord,
  PriceListPriceWriteRequest,
  PriceListProvenance,
  PriceListReferenceQuery,
  PriceListReferenceRecord,
  PriceListRecord,
  PriceListWriteRequest,
} from './price-list.model';
import { PriceListService } from './price-list.service';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { TabsComponent } from '../../shared/ui/tabs.component';
import type { AppTab } from '../../shared/ui/tabs.component';

type DetailMode = 'view' | 'edit' | 'create';
type DetailTab = 'overview' | 'prices' | 'history' | 'audit' | 'resolve';
type LifecycleAction = 'deactivate' | 'reactivate';

interface PriceListDraft {
  code: string;
  englishName: string;
  arabicName: string;
  currencyId: string;
  customerId: string;
  organizationScopeKind: '' | OrganizationScopeKind;
  organizationScopeId: string;
  priority: number;
}

interface PriceVersionDraft {
  productId: string;
  unitOfMeasureId: string;
  effectiveFrom: string;
  effectiveTo: string;
  openEnded: boolean;
  price: number;
  priceScale: number;
  provenance: PriceListProvenance;
  sourceReference: string;
}

interface ResolveDraft {
  productId: string;
  unitOfMeasureId: string;
  currencyId: string;
  customerId: string;
  organizationScopeKind: '' | OrganizationScopeKind;
  organizationScopeId: string;
  effectiveOn: string;
  limitToThisList: boolean;
}

@Component({
  selector: 'app-price-list-workspace',
  standalone: true,
  imports: [FormsModule, NgTemplateOutlet, DataGridComponent, PageHeaderComponent, TabsComponent],
  template: `
    <section class="price-list-workspace" aria-labelledby="price-list-title">
      <app-page-header>
        <div page-header-copy>
          <p class="eyebrow">{{ language.text('masterData') }} / {{ language.text('tenantCatalog') }}</p>
          <h1 id="price-list-title">{{ language.text('priceLists') }}</h1>
          <p>{{ language.text('priceListsLead') }} {{ language.text('serverAuthority') }}: {{ language.text('priceListBoundary') }}. {{ language.text('priority') }}: {{ language.text('priorityHint') }}</p>
        </div>
        <div page-header-actions>
          @if (detailMode()) {
            <button class="button button--quiet" type="button" (click)="backToList()">← {{ language.text('priceLists') }}</button>
          } @else {
            <button class="button button--quiet" type="button" (click)="loadList()" [disabled]="loading()" [attr.aria-label]="language.text('refresh')">↻ {{ language.text('refresh') }}</button>
            <button class="button button--primary" type="button" (click)="startCreate()" [disabled]="!canMutate()" [title]="canMutate() ? '' : language.text('accessUnavailable')">＋ {{ language.text('newRecord') }}</button>
          }
        </div>
      </app-page-header>

      <div class="workspace-panel">
        @if (detailMode()) { <ng-container *ngTemplateOutlet="detailView" /> } @else { <ng-container *ngTemplateOutlet="listView" /> }
      </div>
    </section>

    <ng-template #listView>
      <section class="list-view" [attr.aria-label]="language.text('priceLists')">

        <form class="toolbar" role="search" (ngSubmit)="onSearchSubmit()">
          <label class="search-field">
            <span class="sr-only">{{ language.text('searchRecords') }}</span>
            <span class="search-field__icon" aria-hidden="true">⌕</span>
            <input type="search" [value]="filterQuery()" (input)="onSearchInput($event)" [placeholder]="language.text('searchPlaceholder')" />
          </label>
          <button class="button button--quiet" type="submit" [disabled]="loading()">{{ language.text('searchRecords') }}</button>
          @if (filterQuery()) { <button class="button button--quiet" type="button" (click)="clearSearch()">{{ language.text('clearSearch') }}</button> }
          <span class="toolbar__count">{{ records().length }} {{ language.text('recordCount') }}</span>
        </form>

        @if (loading()) {
          <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><div><b>{{ language.text('loadingRecords') }}</b><p>{{ language.text('serverAuthority') }}</p></div></div>
        } @else if (listError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(listError()) }}</b><p>{{ language.text('listLoadFailed') }}</p><button class="button button--quiet button--small" type="button" (click)="loadList()">{{ language.text('retry') }} ↗</button></div></div>
        } @else if (records().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ language.text('noRecords') }}</b><p>{{ language.text('noRecordsLead') }}</p></div></div>
        } @else {
          <app-data-grid [rows]="pagedRecords()" [columns]="recordColumns" [rowActionsTemplate]="recordActions" [language]="language.language()" [clientPaging]="false" [showPager]="false" [scopeLabel]="gridScopeLabel()" [caption]="language.text('priceLists')" [countLabel]="language.text('recordCount')" [filterInputLabel]="language.text('searchRecords')">
            <ng-template #recordActions let-record>
              <button class="icon-button" type="button" (click)="openRecord(record.id)" [attr.aria-label]="language.text('viewRecord')">↗</button>
            </ng-template>
          </app-data-grid>
          <div class="pagination" aria-label="Pagination">
            <span>{{ pageLabel() }}</span>
            <div><button class="pager-button" type="button" (click)="previousPage()" [disabled]="page() === 1">← {{ language.text('previous') }}</button><button class="pager-button" type="button" (click)="nextPage()" [disabled]="page() === totalPages()">{{ language.text('next') }} →</button></div>
          </div>
        }
      </section>
    </ng-template>

    <ng-template #detailView>
      <section class="detail-view" aria-labelledby="detail-title">
        <div class="detail-topline"><button class="button button--quiet" type="button" (click)="backToList()">← {{ language.text('priceLists') }}</button></div>
        @if (detailLoading()) {
          <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loadingRecord') }}</b></div>
        } @else if (detailError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(detailError()) }}</b><p>{{ language.text('detailLoadFailed') }}</p><button class="button button--quiet button--small" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') }} ↗</button></div></div>
        } @else {
          <div class="detail-heading">
            <div><p class="eyebrow eyebrow--soft">{{ detailMode() === 'create' ? language.text('newRecord') : language.text('priceListDetail') }}</p><h2 id="detail-title">{{ detailMode() === 'create' ? language.text('createRecord') : (selectedRecord()!.code + ' · ' + selectedRecord()!.englishName) }}</h2><p>{{ language.text('priceListsLead') }}</p></div>
            <div class="detail-heading__actions">
              @if (selectedRecord() && detailMode() === 'view') {
                <span class="status-pill" [class.status-pill--inactive]="!isActive(selectedRecord()!)"><i aria-hidden="true"></i>{{ statusLabel(selectedRecord()!.lifecycleState) }}</span>
                <button class="button button--quiet" type="button" (click)="startEdit()" [disabled]="!canMutate()">{{ language.text('editRecord') }}</button>
                @if (isActive(selectedRecord()!)) { <button class="button button--danger" type="button" (click)="openLifecycle('deactivate')" [disabled]="!canMutate()">{{ language.text('deactivate') }}</button> } @else { <button class="button button--primary" type="button" (click)="openLifecycle('reactivate')" [disabled]="!canMutate()">{{ language.text('reactivate') }}</button> }
              }
            </div>
          </div>

          @if (mutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b><span>{{ mutationError()?.code === 'concurrency_conflict' ? language.text('conflictLead') : '' }}</span>@if (mutationError()?.code === 'concurrency_conflict') { <button class="button button--quiet button--small" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') }}</button> }</div> }
          @if (formNotice()) { <div class="inline-alert inline-alert--success" role="status">{{ formNotice() }}</div> }

          @if (detailMode() === 'view' && selectedRecord()) {
            <app-tabs #priceListTabsRef [tabs]="priceListTabs()" [selected]="detailTab()" [ariaLabel]="language.text('priceListDetail')" (selectedChange)="selectPriceListTab($event)" />

            @switch (detailTab()) {
              @case ('overview') { <div role="tabpanel" [id]="priceListTabsRef.panelId('overview')" [attr.aria-labelledby]="priceListTabsRef.tabId('overview')" tabindex="0"><ng-container *ngTemplateOutlet="overviewTab" /></div> }
              @case ('prices') { <div role="tabpanel" [id]="priceListTabsRef.panelId('prices')" [attr.aria-labelledby]="priceListTabsRef.tabId('prices')" tabindex="0"><ng-container *ngTemplateOutlet="pricesTab" /></div> }
              @case ('history') { <div role="tabpanel" [id]="priceListTabsRef.panelId('history')" [attr.aria-labelledby]="priceListTabsRef.tabId('history')" tabindex="0"><ng-container *ngTemplateOutlet="historyTab" /></div> }
              @case ('audit') { <div role="tabpanel" [id]="priceListTabsRef.panelId('audit')" [attr.aria-labelledby]="priceListTabsRef.tabId('audit')" tabindex="0"><ng-container *ngTemplateOutlet="auditTab" /></div> }
              @case ('resolve') { <div role="tabpanel" [id]="priceListTabsRef.panelId('resolve')" [attr.aria-labelledby]="priceListTabsRef.tabId('resolve')" tabindex="0"><ng-container *ngTemplateOutlet="resolveTab" /></div> }
            }
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

    <ng-template #overviewTab>
      @if (selectedRecord(); as record) {
        <div class="field-read-grid">
          <div><span>{{ language.text('code') }}</span><b>{{ record.code }}</b></div>
          <div><span>{{ language.text('englishName') }}</span><b>{{ record.englishName }}</b></div>
          <div><span>{{ language.text('arabicName') }}</span><b dir="rtl">{{ valueOrEmpty(record.arabicName) }}</b></div>
          <div><span>{{ language.text('currency') }}</span><b>{{ record.currencyCode }}</b></div>
          <div><span>{{ language.text('customer') }}</span><b>{{ customerDisplay(record.customerId) }}</b></div>
          <div><span>{{ language.text('organizationScope') }}</span><b>{{ organizationScopeDisplay(record) }}</b></div>
          <div><span>{{ language.text('priority') }}</span><b>{{ record.priority }}</b></div>
          <div><span>{{ language.text('currentVersion') }}</span><b>v{{ record.currentVersionNumber }}</b></div>
          <div class="field-read-grid__wide"><span>{{ language.text('pricesSection') }}</span><b>{{ record.prices.length }} {{ language.text('recordCount') }}</b></div>
        </div>
        <p class="boundary-note">{{ language.text('priceListBoundary') }}</p>
      }
    </ng-template>

    <ng-template #priceRows let-entries="entries" let-empty="empty" let-emptyLead="emptyLead">
      @if (entries.length === 0) {
        <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ empty }}</b><p>{{ emptyLead }}</p></div></div>
      } @else {
        <app-data-grid [rows]="entries" [columns]="priceColumns" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('pricesSection')" [countLabel]="language.text('recordCount')" [emptyLabel]="empty" />
      }
    </ng-template>

    <ng-template #pricesTab>
      @if (selectedRecord(); as record) {
        <div class="section-heading">
          <div><p class="eyebrow eyebrow--soft">{{ language.text('pricesSection') }}</p><h3>{{ language.text('pricesSection') }}</h3></div>
          <div class="section-heading__actions"><button class="button button--primary" type="button" (click)="openAddPrice()" [disabled]="!canMutate()">＋ {{ language.text('addPriceVersion') }}</button></div>
        </div>
        <p class="term-hint">{{ language.text('addPriceVersionLead') }}</p>

        @if (priceFormOpen()) {
          <form class="panel-block" (ngSubmit)="submitPrice()" novalidate>
            @if (priceFormError()) { <div class="form-summary" role="alert">{{ language.text('validationSummary') }}</div> }
            @if (priceMutationError()) { <div class="inline-alert" role="alert"><b>{{ errorMessage(priceMutationError()) }}</b>@if (priceMutationError()?.code === 'concurrency_conflict') { <button class="button button--quiet button--small" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') }}</button> }</div> }
            <div class="form-grid">
              <label class="form-field" [class.has-error]="priceInvalid('productId')"><span>{{ language.text('product') }} <em>*</em></span><select [ngModel]="priceDraft.productId" (ngModelChange)="setPriceField('productId', $event)" name="priceProductId"><option value="">{{ language.text('selectProduct') }}</option>@for (p of productChoices(); track p.id) { <option [value]="p.id">{{ productOptionLabel(p) }}</option> }</select><small>{{ priceInvalid('productId') ? language.text('required') : '' }}</small></label>
              <label class="form-field" [class.has-error]="priceInvalid('unitOfMeasureId')"><span>{{ language.text('unitOfMeasure') }} <em>*</em></span><select [ngModel]="priceDraft.unitOfMeasureId" (ngModelChange)="setPriceField('unitOfMeasureId', $event)" name="priceUnitId"><option value="">{{ language.text('selectUnit') }}</option>@for (u of unitChoices(); track u.id) { <option [value]="u.id">{{ unitOptionLabel(u) }}</option> }</select><small>{{ priceInvalid('unitOfMeasureId') ? language.text('required') : '' }}</small></label>
              <label class="form-field" [class.has-error]="priceInvalid('effectiveFrom')"><span>{{ language.text('effectiveFrom') }} <em>*</em></span><input type="date" [ngModel]="priceDraft.effectiveFrom" (ngModelChange)="setPriceField('effectiveFrom', $event)" name="priceEffectiveFrom" /><small>{{ priceInvalid('effectiveFrom') ? language.text('required') : '' }}</small></label>
              <label class="check-field"><input type="checkbox" [ngModel]="priceDraft.openEnded" (ngModelChange)="setPriceField('openEnded', $event)" name="priceOpenEnded" /><span>{{ language.text('openEnded') }}</span></label>
              @if (!priceDraft.openEnded) { <label class="form-field" [class.has-error]="priceInvalid('effectiveTo')"><span>{{ language.text('effectiveTo') }}</span><input type="date" [ngModel]="priceDraft.effectiveTo" (ngModelChange)="setPriceField('effectiveTo', $event)" name="priceEffectiveTo" /></label> }
              <label class="form-field" [class.has-error]="priceInvalid('price')"><span>{{ language.text('priceValue') }} <em>*</em></span><input type="number" min="0" step="0.000001" [ngModel]="priceDraft.price" (ngModelChange)="setPriceField('price', $event)" name="priceValue" /><small>{{ priceInvalid('price') ? language.text('validationSummary') : '' }}</small></label>
              <label class="form-field" [class.has-error]="priceInvalid('priceScale')"><span>{{ language.text('priceScale') }} <em>*</em></span><input type="number" min="0" max="12" step="1" [ngModel]="priceDraft.priceScale" (ngModelChange)="setPriceField('priceScale', $event)" name="priceScaleField" /><small>{{ priceInvalid('priceScale') ? language.text('validationSummary') : '' }}</small></label>
              <label class="form-field"><span>{{ language.text('provenance') }}</span><select [ngModel]="priceDraft.provenance" (ngModelChange)="setPriceField('provenance', $event)" name="priceProvenance"><option value="Manual">{{ language.text('manualProvenance') }}</option><option value="Configured">{{ language.text('configuredProvenance') }}</option></select></label>
              <label class="form-field form-field--full"><span>{{ language.text('sourceReference') }}</span><input [ngModel]="priceDraft.sourceReference" (ngModelChange)="setPriceField('sourceReference', $event)" name="priceSourceReference" /></label>
            </div>
            <div class="form-actions"><button class="button button--quiet" type="button" (click)="closeAddPrice()">{{ language.text('cancel') }}</button><button class="button button--primary" type="submit" [disabled]="priceSaving()">{{ priceSaving() ? language.text('actionInProgress') : language.text('addPriceVersion') }}</button></div>
          </form>
        }

        <ng-container *ngTemplateOutlet="priceRows; context: { entries: sortedPrices(), empty: language.text('noPriceVersions'), emptyLead: language.text('noPriceVersionsLead') }" />
      }
    </ng-template>

    <ng-template #historyTab>
      <p class="term-hint">{{ language.text('priceHistoryLead') }}</p>
      @if (historyLoading()) {
        <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
      } @else if (historyError()) {
        <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(historyError()) }}</b><button class="button button--quiet button--small" type="button" (click)="setTab('history', true)">{{ language.text('retry') }} ↗</button></div></div>
      } @else {
        <ng-container *ngTemplateOutlet="priceRows; context: { entries: sortedHistory(), empty: language.text('noPriceVersions'), emptyLead: language.text('noPriceVersionsLead') }" />
      }
    </ng-template>

    <ng-template #auditTab>
      @if (auditLoading()) {
        <p class="muted-line">{{ language.text('loading') }}…</p>
      } @else if (auditError()) {
        <p class="muted-line">{{ language.text('auditUnavailable') }}</p>
      } @else if (auditEntries().length === 0) {
        <p class="muted-line">{{ language.text('auditEmpty') }}</p>
      } @else {
        <app-data-grid [rows]="auditEntries()" [columns]="auditColumns" [rowIdFor]="auditRowId" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('audit')" [countLabel]="language.text('recordCount')" [emptyLabel]="language.text('auditEmpty')" />
      }
    </ng-template>

    <ng-template #resolveTab>
      <p class="term-hint">{{ language.text('checkApplicablePriceLead') }}</p>
      <form class="panel-block" (ngSubmit)="resolvePrice()" novalidate>
        <div class="form-grid">
          <label class="form-field" [class.has-error]="resolveFieldErrors().has('productId')"><span>{{ language.text('product') }} <em>*</em></span><select [ngModel]="resolveDraft.productId" (ngModelChange)="setResolveField('productId', $event)" name="resolveProductId"><option value="">{{ language.text('selectProduct') }}</option>@for (p of productChoices(); track p.id) { <option [value]="p.id">{{ productOptionLabel(p) }}</option> }</select></label>
          <label class="form-field" [class.has-error]="resolveFieldErrors().has('unitOfMeasureId')"><span>{{ language.text('unitOfMeasure') }} <em>*</em></span><select [ngModel]="resolveDraft.unitOfMeasureId" (ngModelChange)="setResolveField('unitOfMeasureId', $event)" name="resolveUnitId"><option value="">{{ language.text('selectUnit') }}</option>@for (u of unitChoices(); track u.id) { <option [value]="u.id">{{ unitOptionLabel(u) }}</option> }</select></label>
          <label class="form-field" [class.has-error]="resolveFieldErrors().has('currencyId')"><span>{{ language.text('currency') }} <em>*</em></span><select [ngModel]="resolveDraft.currencyId" (ngModelChange)="setResolveField('currencyId', $event)" name="resolveCurrencyId"><option value="">{{ language.text('selectCurrency') }}</option>@for (c of currencyChoices(); track c.id) { <option [value]="c.id">{{ currencyOptionLabel(c) }}</option> }</select></label>
          <label class="form-field"><span>{{ language.text('customer') }}</span><select [ngModel]="resolveDraft.customerId" (ngModelChange)="setResolveField('customerId', $event)" name="resolveCustomerId"><option value="">{{ language.text('generalCustomer') }}</option>@for (c of customerChoices(); track c.id) { <option [value]="c.id">{{ customerOptionLabel(c) }}</option> }</select></label>
          <label class="form-field"><span>{{ language.text('organizationScope') }}</span><select [ngModel]="resolveDraft.organizationScopeKind" (ngModelChange)="setResolveField('organizationScopeKind', $event)" name="resolveOrgKind"><option value="">{{ language.text('organizationScopeEntire') }}</option><option value="Company">{{ language.text('organizationScopeCompany') }}</option><option value="Branch">{{ language.text('organizationScopeBranch') }}</option></select></label>
          @if (resolveDraft.organizationScopeKind) { <label class="form-field"><span>{{ language.text('organizationScopeId') }}</span><input [ngModel]="resolveDraft.organizationScopeId" (ngModelChange)="setResolveField('organizationScopeId', $event)" name="resolveOrgId" /><small>{{ language.text('organizationScopeIdHint') }}</small></label> }
          <label class="form-field" [class.has-error]="resolveFieldErrors().has('effectiveOn')"><span>{{ language.text('effectiveOn') }} <em>*</em></span><input type="date" [ngModel]="resolveDraft.effectiveOn" (ngModelChange)="setResolveField('effectiveOn', $event)" name="resolveEffectiveOn" /></label>
          <label class="check-field"><input type="checkbox" [ngModel]="resolveDraft.limitToThisList" (ngModelChange)="setResolveField('limitToThisList', $event)" name="resolveLimit" /><span>{{ language.text('limitToThisPriceList') }}</span></label>
          <div class="form-field form-field--action"><span>&nbsp;</span><button class="button button--primary" type="submit" [disabled]="resolveLoading()">{{ resolveLoading() ? language.text('resolvingPrice') : language.text('resolvePrice') }}</button></div>
        </div>
      </form>
      <p class="boundary-note">{{ language.text('precedenceExplanation') }}</p>

      @if (resolveLoading()) {
        <p class="muted-line" role="status">{{ language.text('resolvingPrice') }}</p>
      } @else if (resolveError(); as err) {
        @if (err.code === 'price_list_precedence_conflict') {
          <div class="inline-alert" role="alert"><b>{{ language.text('priceListPrecedenceConflictError') }}</b><span>{{ language.text('precedenceExplanation') }}</span></div>
        } @else if (err.code === 'price_list_not_found') {
          <p class="muted-line">{{ language.text('priceListResolutionNotFound') }}</p>
        } @else {
          <div class="inline-alert" role="alert"><b>{{ errorMessage(err) }}</b></div>
        }
      } @else if (resolveResult(); as r) {
        <div class="tax-result" role="status">
          <div><span>{{ language.text('resolvedPriceList') }}</span><b>{{ r.priceListCode }}</b></div>
          <div><span>{{ language.text('resolvedVersion') }}</span><b>v{{ r.priceVersionNumber }}</b></div>
          <div><span>{{ language.text('priceValue') }}</span><b>{{ r.referenceValue }} {{ r.currencyCode }}</b></div>
          <div><span>{{ language.text('resolvedSource') }}</span><b>{{ r.provenance }}</b></div>
          <div><span>{{ language.text('effectiveFrom') }}</span><b>{{ r.effectiveFrom }}{{ r.effectiveTo ? ' - ' + r.effectiveTo : '' }}</b></div>
          <div><span>{{ language.text('priority') }}</span><b>{{ r.priority }}</b></div>
          <div class="field-read-grid__wide"><span>{{ language.text('sourceReference') }}</span><b>{{ valueOrEmpty(r.sourceReference) }}</b></div>
        </div>
      } @else {
        <p class="muted-line">{{ language.text('noResolutionYet') }}</p>
      }
    </ng-template>

    <ng-template #editableFields>
      <div class="form-section">
        <div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">01 / {{ language.text('priceLists') }}</p><h3>{{ language.text('priceLists') }}</h3></div><span>{{ language.text('serverAuthority') }}</span></div>
        <div class="form-grid">
          <label class="form-field" [class.has-error]="invalid('code')"><span>{{ language.text('code') }} <em>*</em></span><input [ngModel]="draft.code" (ngModelChange)="setDraftField('code', $event)" name="priceListCode" autocomplete="off" [attr.aria-invalid]="invalid('code')" /><small>{{ invalid('code') ? language.text('required') : '' }}</small></label>
          <label class="form-field" [class.has-error]="invalid('englishName')"><span>{{ language.text('englishName') }} <em>*</em></span><input [ngModel]="draft.englishName" (ngModelChange)="setDraftField('englishName', $event)" name="priceListEnglishName" [attr.aria-invalid]="invalid('englishName')" /><small>{{ invalid('englishName') ? language.text('required') : '' }}</small></label>
          <label class="form-field" dir="rtl"><span>{{ language.text('arabicName') }}</span><input [ngModel]="draft.arabicName" (ngModelChange)="setDraftField('arabicName', $event)" name="priceListArabicName" /></label>
          <label class="form-field" [class.has-error]="invalid('currencyId')"><span>{{ language.text('currency') }} <em>*</em></span><select [ngModel]="draft.currencyId" (ngModelChange)="setDraftField('currencyId', $event)" name="priceListCurrencyId" [attr.aria-invalid]="invalid('currencyId')"><option value="">{{ language.text('selectCurrency') }}</option>@for (c of currencyChoices(); track c.id) { <option [value]="c.id">{{ currencyOptionLabel(c) }}</option> }</select><small>{{ invalid('currencyId') ? language.text('required') : '' }}</small></label>
          <label class="form-field"><span>{{ language.text('customer') }}</span><select [ngModel]="draft.customerId" (ngModelChange)="setDraftField('customerId', $event)" name="priceListCustomerId"><option value="">{{ language.text('generalCustomer') }}</option>@for (c of customerChoices(); track c.id) { <option [value]="c.id">{{ customerOptionLabel(c) }}</option> }</select></label>
          <label class="form-field" [class.has-error]="invalid('priority')"><span>{{ language.text('priority') }} <em>*</em></span><input type="number" step="1" [ngModel]="draft.priority" (ngModelChange)="setDraftField('priority', $event)" name="priceListPriority" [attr.aria-invalid]="invalid('priority')" /><small>{{ invalid('priority') ? language.text('required') : language.text('priorityHint') }}</small></label>
          <label class="form-field"><span>{{ language.text('organizationScope') }}</span><select [ngModel]="draft.organizationScopeKind" (ngModelChange)="setDraftField('organizationScopeKind', $event)" name="priceListOrgKind"><option value="">{{ language.text('organizationScopeEntire') }}</option><option value="Company">{{ language.text('organizationScopeCompany') }}</option><option value="Branch">{{ language.text('organizationScopeBranch') }}</option></select></label>
          @if (draft.organizationScopeKind) { <label class="form-field" [class.has-error]="invalid('organizationScopeId')"><span>{{ language.text('organizationScopeId') }} <em>*</em></span><input [ngModel]="draft.organizationScopeId" (ngModelChange)="setDraftField('organizationScopeId', $event)" name="priceListOrgId" [attr.aria-invalid]="invalid('organizationScopeId')" /><small>{{ invalid('organizationScopeId') ? language.text('required') : language.text('organizationScopeIdHint') }}</small></label> }
        </div>
        @if (referenceLoadFailed()) { <p class="muted-line">{{ language.text('accessUnavailable') }}</p> }
        <p class="boundary-note">{{ language.text('priceListBoundary') }}</p>
      </div>
    </ng-template>

    @if (lifecycleAction()) {
      <div class="dialog-backdrop" role="presentation" (click)="closeLifecycle()">
        <section class="lifecycle-dialog" role="dialog" aria-modal="true" aria-labelledby="lifecycle-title" (click)="$event.stopPropagation()">
          <p class="eyebrow eyebrow--soft">{{ language.text('lifecycle') }}</p>
          <h2 id="lifecycle-title">{{ lifecycleAction() === 'deactivate' ? language.text('deactivateTitle') : language.text('reactivateTitle') }}</h2>
          @if (mutationError()) {
            <div class="inline-alert" role="alert"><b>{{ errorMessage(mutationError()) }}</b>@if (mutationError()?.code === 'concurrency_conflict') { <button class="button button--quiet button--small" type="button" (click)="reloadAndCloseLifecycle()">{{ language.text('retryLoad') }}</button> }</div>
          }
          <div class="form-actions">
            <button class="button button--quiet" type="button" (click)="closeLifecycle()">{{ language.text('cancel') }}</button>
            <button class="button button--primary" type="button" (click)="confirmLifecycle()" [disabled]="lifecycleSaving()">{{ lifecycleSaving() ? language.text('actionInProgress') : (lifecycleAction() === 'deactivate' ? language.text('confirmDeactivate') : language.text('confirmReactivate')) }}</button>
          </div>
        </section>
      </div>
    }
  `,
  styles: `:host { display: block; }
    .price-list-workspace { display: grid; gap: 1.35rem; }
    .eyebrow { margin: 0 0 .55rem; color: var(--primary); font-size: 12px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    .eyebrow--soft { color: var(--accent-strong); }
    h1, h2, h3, p { margin-block-start: 0; }
    .workspace-panel { min-width: 0; border: 1px solid var(--line); border-radius: 1.15rem; background: var(--surface-raised); box-shadow: var(--shadow-soft); }
    .list-view, .detail-view { padding: clamp(1rem, 2.5vw, 1.65rem); }
    .section-heading, .detail-heading, .detail-topline, .toolbar, .pagination, .form-section__heading, .form-actions { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .section-heading { align-items: flex-end; margin-block-end: 1.35rem; flex-wrap: wrap; }
    .section-heading h2, .detail-heading h2, .section-heading h3 { margin: 0; color: var(--ink); font: 800 clamp(1.3rem, 3vw, 1.9rem)/1 var(--font-display); letter-spacing: -.04em; }
    .section-heading p:not(.eyebrow), .detail-heading p:not(.eyebrow) { max-width: 37rem; margin: .5rem 0 0; color: var(--ink-muted); font-size: .82rem; line-height: 1.5; }
    .section-heading__actions, .detail-heading__actions { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: .5rem; }
    .toolbar { align-items: stretch; flex-wrap: wrap; margin-block-end: 1rem; border-block: 1px solid var(--line); padding-block: .75rem; }
    .search-field__icon { color: var(--accent-strong); font-size: 1.3rem; }
    .toolbar__count { align-self: center; margin-inline-start: auto; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; white-space: nowrap; }
    .record-name { display: block; color: var(--ink); font-weight: 700; }
    .status-pill { display: inline-flex; align-items: center; gap: .35rem; border-radius: 99px; padding: .3rem .5rem; color: var(--success); background: var(--accent-soft); font-size: .66rem; font-weight: 800; white-space: nowrap; }
    .status-pill i { width: .38rem; height: .38rem; border-radius: 50%; background: currentColor; }
    .status-pill--inactive { color: var(--support); background: var(--support-soft); }
    .table-action { text-align: end !important; }
    .pagination { margin-block-start: .8rem; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; }
    .pagination > div { display: flex; gap: .4rem; }
    .state-card { display: flex; align-items: flex-start; gap: .8rem; border: 1px dashed var(--line-strong); border-radius: .8rem; padding: 1.35rem; background: var(--canvas); }
    .state-card b { color: var(--ink); font-size: .85rem; }
    .state-card p { margin: .3rem 0 0; color: var(--ink-muted); font-size: .75rem; line-height: 1.5; }
    .state-card--error { border-style: solid; border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); background: color-mix(in srgb, var(--danger) 5%, var(--surface-raised)); }
    .state-card--empty { min-height: 8rem; align-items: center; }
    .state-icon { display: grid; flex: 0 0 1.8rem; place-items: center; width: 1.8rem; height: 1.8rem; border-radius: .5rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); font-weight: 900; }
    .state-card--empty .state-icon { color: var(--accent-strong); background: var(--accent-soft); }
    .loader { width: 1.2rem; height: 1.2rem; border: 2px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: spin .8s linear infinite; }
    .text-button { margin-block-start: .7rem; }
    .detail-topline { margin-block-end: 1.25rem; justify-content: flex-start; }
    .back-link:hover { color: var(--accent-strong); }
    .detail-heading { align-items: flex-end; margin-block-end: 1.25rem; flex-wrap: wrap; }
    .field-read-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .9rem 1.2rem; }
    .field-read-grid > div { min-width: 0; border-block-start: 2px solid var(--line); padding-block-start: .5rem; }
    .field-read-grid span, .contacts-read > span { display: block; color: var(--ink-muted); font-size: .67rem; font-weight: 700; }
    .field-read-grid b { display: block; margin-block-start: .3rem; overflow-wrap: anywhere; color: var(--ink); font-size: .8rem; line-height: 1.4; }
    .field-read-grid__wide { grid-column: 1 / -1; }
    .boundary-note { margin: 1rem 0 0; border-inline-start: 3px solid var(--accent); padding-inline-start: .7rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.5; }
    .muted-line { margin: 0; color: var(--ink-muted); font-size: .75rem; }
    .term-hint { margin: -.35rem 0 .85rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.45; }
    .edit-card { padding: 0; overflow: hidden; }
    .form-section { padding: 1rem; }
    .form-section__heading { align-items: flex-start; margin-block-end: 1rem; }
    .form-section__heading h3 { margin: 0; font: 800 1.05rem/1 var(--font-display); }
    .form-section__heading > span { color: var(--ink-muted); font-size: .68rem; }
    .form-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .9rem; }
    .panel-block { margin-block-start: 1rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }
    .form-field { display: grid; gap: .35rem; min-width: 0; }
    .form-field > span, .check-field span { color: var(--ink-muted); font-size: .7rem; font-weight: 800; }
    .form-field em { color: var(--danger); font-style: normal; }
    .form-field input, .form-field select, .form-field textarea { width: 100%; }
    .form-field small { min-height: 1rem; color: var(--danger); font-size: .62rem; line-height: 1.35; }
    .form-field:not(.has-error) small { color: var(--ink-muted); }
    .form-field--full { grid-column: 1 / -1; }
    .check-field { display: flex; align-items: center; gap: .5rem; align-self: center; min-height: 2.4rem; border: 1px solid var(--line); border-radius: .45rem; padding: .55rem .65rem; background: var(--canvas); cursor: pointer; }
    .check-field input { accent-color: var(--accent-strong); }
    .form-summary, .inline-alert { margin: 1rem 1rem 0; border-radius: .55rem; padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .74rem; }
    .inline-alert { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; justify-content: space-between; margin: 0 0 1rem; }
    .inline-alert span { color: var(--ink-muted); }
    .inline-alert--success { color: var(--success); background: var(--accent-soft); }
    .form-actions { justify-content: flex-end; border-block-start: 1px solid var(--line); padding: .85rem 1rem; background: var(--canvas); }
    .panel-block .form-actions { margin-inline: -1rem; margin-block-end: -1rem; }
    .dialog-backdrop { display: grid; position: fixed; z-index: 5; inset: 0; place-items: center; padding: 1rem; background: rgb(16 39 37 / 48%); }
    .lifecycle-dialog { width: min(100%, 27rem); border: 1px solid var(--line); border-radius: 1rem; padding: 1.35rem; background: var(--surface-raised); box-shadow: var(--shadow-card); }
    .lifecycle-dialog h2 { margin: 0 0 1rem; font: 800 1.35rem/1.05 var(--font-display); }
    .lifecycle-dialog .form-actions { margin: 1.25rem -1.35rem -1.35rem; }
    .tax-result { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .6rem; margin-block-start: 1rem; border: 1px solid var(--line); border-radius: .6rem; padding: .75rem; background: var(--canvas); }
    .tax-result span, .tax-result b { display: block; }
    .tax-result span { color: var(--ink-muted); font-size: .64rem; font-weight: 700; }
    .tax-result b { margin-block-start: .25rem; color: var(--ink); font-size: .78rem; overflow-wrap: anywhere; }
    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; } }
    @media (max-width: 980px) { .tax-result { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 720px) {
      .section-heading, .detail-heading, .toolbar { align-items: stretch; flex-direction: column; }
      .section-heading__actions, .detail-heading__actions { justify-content: flex-start; }
      .toolbar__count { margin-inline-start: 0; }
      .form-grid { grid-template-columns: 1fr; }
      .field-read-grid { grid-template-columns: 1fr; }
      .field-read-grid__wide { grid-column: auto; }
      .tax-result { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 460px) { .list-view, .detail-view { padding: .8rem; } .button span { display: none; } .form-actions { flex-wrap: wrap; } .form-actions .button { flex: 1; } .tax-result { grid-template-columns: 1fr; } }
  `,
})
export class PriceListWorkspaceComponent {
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  private readonly priceLists = inject(PriceListService);
  private readonly data = inject(MasterDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly records = signal<PriceListRecord[]>([]);
  readonly loading = signal(false);
  readonly listError = signal<SafeUiError | null>(null);
  readonly filterQuery = signal('');
  readonly page = signal(1);
  readonly pageSize = 8;

  readonly detailMode = signal<DetailMode | null>(null);
  readonly detailTab = signal<DetailTab>('overview');
  priceListTabs(): AppTab[] { return [
    { id: 'overview', label: this.language.text('priceListOverview') },
    { id: 'prices', label: `${this.language.text('pricesSection')} (${this.selectedRecord()?.prices.length ?? 0})` },
    { id: 'history', label: this.language.text('historySection') },
    { id: 'audit', label: this.language.text('audit') },
    { id: 'resolve', label: this.language.text('referenceResolutionSection') },
  ]; }
  selectPriceListTab(tab: string): void { this.setTab(tab as DetailTab); }
  readonly detailLoading = signal(false);
  readonly detailError = signal<SafeUiError | null>(null);
  readonly selectedRecord = signal<PriceListRecord | null>(null);
  readonly mutationError = signal<SafeUiError | null>(null);
  readonly formError = signal(false);
  readonly fieldErrors = signal<ReadonlySet<string>>(new Set());
  readonly formNotice = signal<string | null>(null);
  readonly saving = signal(false);

  readonly lifecycleAction = signal<LifecycleAction | null>(null);
  readonly lifecycleSaving = signal(false);

  readonly productChoices = signal<ProductRecord[]>([]);
  readonly unitChoices = signal<UnitOfMeasureRecord[]>([]);
  readonly currencyChoices = signal<CurrencyRecord[]>([]);
  readonly customerChoices = signal<CustomerRecord[]>([]);
  readonly referenceLoadFailed = signal(false);

  readonly historyEntries = signal<PriceListPriceVersionRecord[]>([]);
  readonly historyLoading = signal(false);
  readonly historyError = signal<SafeUiError | null>(null);
  readonly historyLoaded = signal(false);

  readonly auditEntries = signal<MasterDataAuditEntry[]>([]);
  readonly auditLoading = signal(false);
  readonly auditError = signal<SafeUiError | null>(null);
  readonly auditLoaded = signal(false);

  readonly priceFormOpen = signal(false);
  readonly priceSaving = signal(false);
  readonly priceFormError = signal(false);
  readonly priceFieldErrors = signal<ReadonlySet<string>>(new Set());
  readonly priceMutationError = signal<SafeUiError | null>(null);

  readonly resolveResult = signal<PriceListReferenceRecord | null>(null);
  readonly resolveLoading = signal(false);
  readonly resolveError = signal<SafeUiError | null>(null);
  readonly resolveFieldErrors = signal<ReadonlySet<string>>(new Set());

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.records().length / this.pageSize)));
  readonly pagedRecords = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.records().slice(start, start + this.pageSize);
  });
  readonly sortedPrices = computed(() => {
    const record = this.selectedRecord();
    return record ? [...record.prices].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom) || b.versionNumber - a.versionNumber) : [];
  });
  readonly sortedHistory = computed(() =>
    [...this.historyEntries()].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom) || b.versionNumber - a.versionNumber),
  );
  readonly recordColumns: DataGridColumn<PriceListRecord>[] = [
    { key: 'code', label: this.language.text('code'), value: row => row.code, secondaryText: row => row.englishName, filter: 'text' },
    { key: 'currencyCode', label: this.language.text('currency'), value: row => row.currencyCode, filter: 'select' },
    { key: 'priority', label: this.language.text('priority'), value: row => row.priority, filter: 'number-range', align: 'end' },
    { key: 'customerId', label: this.language.text('scope'), value: row => this.customerDisplay(row.customerId), secondaryText: row => this.organizationScopeDisplay(row), filter: 'text' },
    { key: 'lifecycleState', label: this.language.text('lifecycle'), value: row => this.statusLabel(row.lifecycleState), badge: true, filter: 'select' },
  ];
  readonly priceColumns: DataGridColumn<PriceListPriceVersionRecord>[] = [
    { key: 'versionNumber', label: this.language.text('versionNumber'), value: row => row.versionNumber, display: row => `v${row.versionNumber}`, filter: 'number-range' },
    { key: 'productSku', label: this.language.text('product'), value: row => row.productSku, filter: 'text' },
    { key: 'unitOfMeasureCode', label: this.language.text('unitOfMeasure'), value: row => row.unitOfMeasureCode, filter: 'select' },
    { key: 'effectiveFrom', label: `${this.language.text('effectiveFrom')} / ${this.language.text('effectiveTo')}`, value: row => row.effectiveFrom, secondaryText: row => row.effectiveTo || this.language.text('openEnded'), filter: 'date-range' },
    { key: 'price', label: this.language.text('priceValue'), value: row => row.price, display: row => `${this.formatPrice(row.price, row.priceScale)} ${row.currencyCode}`, filter: 'number-range', align: 'end' },
    { key: 'provenance', label: this.language.text('provenance'), value: row => row.provenance, filter: 'select' },
    { key: 'sourceReference', label: this.language.text('sourceReference'), value: row => this.valueOrEmpty(row.sourceReference), filter: 'text' },
  ];
  readonly auditColumns: DataGridColumn<MasterDataAuditEntry>[] = [
    { key: 'occurredAt', label: this.language.text('auditWhen'), value: row => row.occurredAt, display: row => new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(row.occurredAt)), filter: 'date-range' },
    { key: 'operation', label: this.language.text('auditAction'), value: row => row.operation, filter: 'text' },
    { key: 'decision', label: this.language.text('auditDecision'), value: row => row.decision, badge: true, filter: 'select' },
    { key: 'reason', label: this.language.text('auditReason'), value: row => row.reason, secondaryText: row => row.afterSummary ?? '', filter: 'text' },
  ];
  readonly auditRowId = (row: MasterDataAuditEntry): string => row.evidenceId;

  gridScopeLabel(): string {
    return this.language.language() === 'ar' ? 'تطبق التصفية والترتيب على هذه الصفحة فقط' : 'Filters and sorting apply to this page only';
  }

  draft: PriceListDraft = this.emptyDraft();
  priceDraft: PriceVersionDraft = this.emptyPriceDraft();
  resolveDraft: ResolveDraft = this.emptyResolveDraft(null);
  private loadSequence = 0;

  constructor() {
    void this.loadReferenceData();
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      this.filterQuery.set('');
      this.page.set(1);
      if (!id) {
        this.detailMode.set(null);
        this.selectedRecord.set(null);
        this.detailError.set(null);
        this.loadList('');
        return;
      }
      if (id === 'new') {
        this.prepareCreate();
        return;
      }
      void this.loadDetailById(id);
    });
  }

  loadList(search: string = this.filterQuery()): void {
    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.listError.set(null);
    void firstValueFrom(this.priceLists.list(search))
      .then((records) => {
        if (sequence !== this.loadSequence) return;
        this.records.set(records ?? []);
        this.page.set(1);
      })
      .catch((error: unknown) => {
        if (sequence === this.loadSequence) this.listError.set(toSafeUiError(error));
      })
      .finally(() => {
        if (sequence === this.loadSequence) this.loading.set(false);
      });
  }

  private async loadReferenceData(): Promise<void> {
    this.referenceLoadFailed.set(false);
    try {
      const [products, units, currencies, customers] = await Promise.all([
        firstValueFrom(this.data.list('products')),
        firstValueFrom(this.data.list('units')),
        firstValueFrom(this.data.list('currencies')),
        firstValueFrom(this.data.list('customers')),
      ]);
      this.productChoices.set((products ?? []).filter((record): record is ProductRecord => this.isProduct(record)));
      this.unitChoices.set((units ?? []).filter((record): record is UnitOfMeasureRecord => this.isUnit(record)));
      this.currencyChoices.set((currencies ?? []).filter((record): record is CurrencyRecord => this.isCurrency(record)));
      this.customerChoices.set((customers ?? []).filter((record): record is CustomerRecord => this.isCustomer(record)));
    } catch {
      this.referenceLoadFailed.set(true);
    }
  }

  onSearchInput(event: Event): void {
    this.filterQuery.set((event.target as HTMLInputElement).value);
  }

  onSearchSubmit(): void {
    this.page.set(1);
    this.loadList(this.filterQuery());
  }

  clearSearch(): void {
    this.filterQuery.set('');
    this.page.set(1);
    this.loadList('');
  }

  previousPage(): void {
    if (this.page() > 1) this.page.set(this.page() - 1);
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) this.page.set(this.page() + 1);
  }

  pageLabel(): string {
    return this.language.text('pageOf').replace('{page}', String(this.page())).replace('{pages}', String(this.totalPages()));
  }

  openRecord(id: string): void {
    void this.router.navigate(['/app/price-lists', id]);
  }

  backToList(): void {
    void this.router.navigate(['/app/price-lists']);
  }

  startCreate(): void {
    if (!this.canMutate()) return;
    void this.router.navigate(['/app/price-lists', 'new']);
  }

  private prepareCreate(): void {
    this.detailMode.set('create');
    this.selectedRecord.set(null);
    this.draft = this.emptyDraft();
    this.detailLoading.set(false);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.formNotice.set(null);
  }

  private async loadDetailById(id: string): Promise<void> {
    this.detailMode.set('view');
    this.detailTab.set('overview');
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.formNotice.set(null);
    this.historyEntries.set([]);
    this.historyLoaded.set(false);
    this.auditEntries.set([]);
    this.auditLoaded.set(false);
    this.resolveResult.set(null);
    this.resolveError.set(null);
    this.priceFormOpen.set(false);
    try {
      const record = await firstValueFrom(this.priceLists.get(id));
      this.selectedRecord.set(record);
      this.draft = this.toDraft(record);
      this.resolveDraft = this.emptyResolveDraft(record);
    } catch (error: unknown) {
      this.detailError.set(toSafeUiError(error));
    } finally {
      this.detailLoading.set(false);
    }
  }

  async reloadDetail(): Promise<void> {
    const id = this.selectedRecord()?.id ?? this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') return;
    await this.loadDetailById(id);
  }

  reloadAndCloseLifecycle(): void {
    this.lifecycleAction.set(null);
    void this.reloadDetail();
  }

  startEdit(): void {
    const record = this.selectedRecord();
    if (!record || !this.canMutate()) return;
    this.draft = this.toDraft(record);
    this.detailMode.set('edit');
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.mutationError.set(null);
  }

  cancelEdit(): void {
    if (this.detailMode() === 'create') {
      this.backToList();
      return;
    }
    const record = this.selectedRecord();
    if (record) this.draft = this.toDraft(record);
    this.detailMode.set('view');
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.mutationError.set(null);
  }

  setDraftField<K extends keyof PriceListDraft>(field: K, value: PriceListDraft[K]): void {
    this.draft = { ...this.draft, [field]: value };
  }

  invalid(field: string): boolean {
    return this.fieldErrors().has(field);
  }

  async save(): Promise<void> {
    this.formNotice.set(null);
    this.mutationError.set(null);
    if (!this.validateDraft()) {
      this.formError.set(true);
      return;
    }
    const mode = this.detailMode();
    const existing = mode === 'edit' ? this.selectedRecord() : null;
    this.saving.set(true);
    try {
      const payload = this.toPayload();
      const saved = mode === 'create'
        ? await this.priceLists.create(payload)
        : existing
          ? await this.priceLists.edit(existing.id, payload, existing.version)
          : null;
      if (!saved) return;
      this.selectedRecord.set(saved);
      this.replaceRecord(saved);
      this.draft = this.toDraft(saved);
      this.detailMode.set('view');
      this.formError.set(false);
      this.fieldErrors.set(new Set());
      this.formNotice.set(mode === 'create' ? this.language.text('recordCreated') : this.language.text('recordSaved'));
      if (mode === 'create') await this.router.navigate(['/app/price-lists', saved.id]);
      await this.loadAudit(saved.id);
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  openLifecycle(action: LifecycleAction): void {
    if (!this.selectedRecord() || !this.canMutate()) return;
    this.mutationError.set(null);
    this.lifecycleAction.set(action);
  }

  closeLifecycle(): void {
    if (this.lifecycleSaving()) return;
    this.lifecycleAction.set(null);
  }

  async confirmLifecycle(): Promise<void> {
    const record = this.selectedRecord();
    const action = this.lifecycleAction();
    if (!record || !action) return;
    this.lifecycleSaving.set(true);
    this.mutationError.set(null);
    try {
      const updated = action === 'deactivate'
        ? await this.priceLists.deactivate(record.id, record.version)
        : await this.priceLists.reactivate(record.id, record.version);
      this.selectedRecord.set(updated);
      this.replaceRecord(updated);
      this.formNotice.set(this.language.text('lifecycleSaved'));
      this.lifecycleAction.set(null);
      await this.loadAudit(updated.id);
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.lifecycleSaving.set(false);
    }
  }

  setTab(tab: DetailTab, force = false): void {
    this.detailTab.set(tab);
    if (tab === 'history' && (force || !this.historyLoaded())) void this.loadHistory();
    if (tab === 'audit' && (force || !this.auditLoaded())) void this.loadAudit();
  }

  private async loadHistory(): Promise<void> {
    const record = this.selectedRecord();
    if (!record) return;
    this.historyLoading.set(true);
    this.historyError.set(null);
    try {
      this.historyEntries.set(await firstValueFrom(this.priceLists.history(record.id)));
      this.historyLoaded.set(true);
    } catch (error: unknown) {
      this.historyError.set(toSafeUiError(error));
    } finally {
      this.historyLoading.set(false);
    }
  }

  private async loadAudit(id?: string): Promise<void> {
    const targetId = id ?? this.selectedRecord()?.id;
    if (!targetId) return;
    this.auditLoading.set(true);
    this.auditError.set(null);
    try {
      this.auditEntries.set(await firstValueFrom(this.priceLists.audit(targetId)));
      this.auditLoaded.set(true);
    } catch (error: unknown) {
      this.auditError.set(toSafeUiError(error));
    } finally {
      this.auditLoading.set(false);
    }
  }

  openAddPrice(): void {
    if (!this.canMutate()) return;
    this.priceDraft = this.emptyPriceDraft();
    this.priceFormError.set(false);
    this.priceFieldErrors.set(new Set());
    this.priceMutationError.set(null);
    this.priceFormOpen.set(true);
  }

  closeAddPrice(): void {
    if (this.priceSaving()) return;
    this.priceFormOpen.set(false);
  }

  setPriceField<K extends keyof PriceVersionDraft>(field: K, value: PriceVersionDraft[K]): void {
    this.priceDraft = { ...this.priceDraft, [field]: value };
  }

  priceInvalid(field: string): boolean {
    return this.priceFieldErrors().has(field);
  }

  async submitPrice(): Promise<void> {
    const record = this.selectedRecord();
    if (!record) return;
    this.priceMutationError.set(null);
    if (!this.validatePriceDraft()) {
      this.priceFormError.set(true);
      return;
    }
    this.priceSaving.set(true);
    try {
      const payload: PriceListPriceWriteRequest = {
        productId: this.priceDraft.productId,
        unitOfMeasureId: this.priceDraft.unitOfMeasureId,
        effectiveFrom: this.priceDraft.effectiveFrom,
        effectiveTo: this.priceDraft.openEnded ? null : this.priceDraft.effectiveTo || null,
        price: Number(this.priceDraft.price),
        priceScale: Number(this.priceDraft.priceScale),
        provenance: this.priceDraft.provenance,
        sourceReference: this.priceDraft.sourceReference.trim() || null,
      };
      const updated = await this.priceLists.appendPrice(record.id, payload, record.version);
      this.selectedRecord.set(updated);
      this.replaceRecord(updated);
      this.historyLoaded.set(false);
      this.priceFormOpen.set(false);
      this.priceFormError.set(false);
      this.formNotice.set(this.language.text('recordSaved'));
    } catch (error: unknown) {
      this.priceMutationError.set(toSafeUiError(error));
    } finally {
      this.priceSaving.set(false);
    }
  }

  setResolveField<K extends keyof ResolveDraft>(field: K, value: ResolveDraft[K]): void {
    this.resolveDraft = { ...this.resolveDraft, [field]: value };
  }

  async resolvePrice(): Promise<void> {
    const errors = new Set<string>();
    if (!this.resolveDraft.productId) errors.add('productId');
    if (!this.resolveDraft.unitOfMeasureId) errors.add('unitOfMeasureId');
    if (!this.resolveDraft.currencyId) errors.add('currencyId');
    if (!this.resolveDraft.effectiveOn) errors.add('effectiveOn');
    this.resolveFieldErrors.set(errors);
    if (errors.size > 0) return;

    this.resolveLoading.set(true);
    this.resolveError.set(null);
    try {
      const query: PriceListReferenceQuery = {
        priceListId: this.resolveDraft.limitToThisList ? this.selectedRecord()?.id : undefined,
        productId: this.resolveDraft.productId,
        unitOfMeasureId: this.resolveDraft.unitOfMeasureId,
        currencyId: this.resolveDraft.currencyId,
        customerId: this.resolveDraft.customerId || undefined,
        organizationScopeKind: this.resolveDraft.organizationScopeKind || undefined,
        organizationScopeId: this.resolveDraft.organizationScopeId || undefined,
        effectiveOn: this.resolveDraft.effectiveOn,
      };
      this.resolveResult.set(await firstValueFrom(this.priceLists.resolveReference(query)));
    } catch (error: unknown) {
      this.resolveResult.set(null);
      this.resolveError.set(toSafeUiError(error));
    } finally {
      this.resolveLoading.set(false);
    }
  }

  canMutate(): boolean {
    return this.auth.status() === 'authenticated' && this.auth.session()?.selectedContextId !== null && this.auth.session()?.selectedContextId !== undefined;
  }

  isActive(record: PriceListRecord): boolean {
    return record.lifecycleState === 'Active';
  }

  statusLabel(state: string): string {
    if (state === 'Active') return this.language.text('activeStatus');
    if (state === 'Inactive') return this.language.text('inactiveStatus');
    return this.language.text('unknownState');
  }

  valueOrEmpty(value: string | null | undefined): string {
    return value && value.trim().length > 0 ? value : this.language.text('emptyValue');
  }

  formatPrice(price: number, scale: number): string {
    const bounded = Math.min(Math.max(Math.trunc(scale) || 0, 0), 12);
    return price.toFixed(bounded);
  }

  customerDisplay(customerId: string | null): string {
    if (!customerId) return this.language.text('generalCustomer');
    const found = this.customerChoices().find((c) => c.id === customerId);
    return found ? this.customerOptionLabel(found) : customerId;
  }

  organizationScopeDisplay(record: PriceListRecord): string {
    if (!record.organizationScopeKind) return this.language.text('organizationScopeEntire');
    const kind = record.organizationScopeKind === 'Company' ? this.language.text('organizationScopeCompany') : this.language.text('organizationScopeBranch');
    return record.organizationScopeId ? `${kind} · ${record.organizationScopeId}` : kind;
  }

  productOptionLabel(record: ProductRecord): string {
    return this.nameLabel(record.sku, record.englishName, record.arabicName);
  }

  unitOptionLabel(record: UnitOfMeasureRecord): string {
    return this.nameLabel(record.code, record.englishName, record.arabicName);
  }

  currencyOptionLabel(record: CurrencyRecord): string {
    return this.nameLabel(record.code, record.englishName, record.arabicName);
  }

  customerOptionLabel(record: CustomerRecord): string {
    return this.nameLabel(record.code, record.englishLegalName, record.arabicLegalName);
  }

  errorMessage(error: SafeUiError | null): string {
    if (!error) return this.language.text('requestError');
    switch (error.code) {
      case 'authentication_failed':
      case 'access_denied':
      case 'permission_denied':
      case 'antiforgery_failed':
        return this.language.text('accessUnavailable');
      case 'concurrency_conflict':
      case 'context_version_conflict':
        return this.language.text('conflictTitle');
      case 'validation_failed':
        return this.language.text('validationSummary');
      case 'network_error':
        return this.language.text('networkError');
      case 'audit_unavailable':
        return this.language.text('auditUnavailable');
      case 'price_list_not_found':
        return this.language.text('priceListNotFoundError');
      case 'price_list_inactive':
        return this.language.text('priceListInactiveError');
      case 'price_list_effective_overlap':
        return this.language.text('priceListEffectiveOverlapError');
      case 'price_list_precedence_conflict':
        return this.language.text('priceListPrecedenceConflictError');
      case 'price_list_duplicate':
        return this.language.text('priceListDuplicateError');
      case 'idempotency_conflict':
        return this.language.text('idempotencyConflictError');
      case 'customer_reference_unavailable':
        return this.language.text('customerReferenceUnavailableError');
      case 'persistence_unavailable':
        return this.language.text('persistenceUnavailableError');
      default:
        return this.language.text('requestError');
    }
  }

  private nameLabel(code: string, englishName: string | null, arabicName: string | null): string {
    return `${code} · ${englishName ?? arabicName ?? code}`;
  }

  private replaceRecord(record: PriceListRecord): void {
    const list = this.records();
    const index = list.findIndex((item) => item.id === record.id);
    if (index === -1) {
      this.records.set([record, ...list]);
      return;
    }
    const next = [...list];
    next[index] = record;
    this.records.set(next);
  }

  private validateDraft(): boolean {
    const errors = new Set<string>();
    if (!this.draft.code.trim()) errors.add('code');
    if (!this.draft.englishName.trim()) errors.add('englishName');
    if (!this.draft.currencyId) errors.add('currencyId');
    if (!Number.isFinite(Number(this.draft.priority))) errors.add('priority');
    if (this.draft.organizationScopeKind && !this.draft.organizationScopeId.trim()) errors.add('organizationScopeId');
    this.fieldErrors.set(errors);
    return errors.size === 0;
  }

  private validatePriceDraft(): boolean {
    const errors = new Set<string>();
    if (!this.priceDraft.productId) errors.add('productId');
    if (!this.priceDraft.unitOfMeasureId) errors.add('unitOfMeasureId');
    if (!this.priceDraft.effectiveFrom) errors.add('effectiveFrom');
    if (!this.priceDraft.openEnded && this.priceDraft.effectiveTo && this.priceDraft.effectiveTo < this.priceDraft.effectiveFrom) errors.add('effectiveTo');
    if (!Number.isFinite(Number(this.priceDraft.price)) || Number(this.priceDraft.price) < 0) errors.add('price');
    if (!Number.isInteger(Number(this.priceDraft.priceScale)) || Number(this.priceDraft.priceScale) < 0 || Number(this.priceDraft.priceScale) > 12) errors.add('priceScale');
    this.priceFieldErrors.set(errors);
    return errors.size === 0;
  }

  private toPayload(): PriceListWriteRequest {
    return {
      code: this.draft.code.trim(),
      englishName: this.draft.englishName.trim(),
      arabicName: this.draft.arabicName.trim() || null,
      currencyId: this.draft.currencyId,
      customerId: this.draft.customerId || null,
      organizationScopeKind: (this.draft.organizationScopeKind || null) as OrganizationScopeKind | null,
      organizationScopeId: this.draft.organizationScopeId.trim() || null,
      priority: Number(this.draft.priority),
    };
  }

  private toDraft(record: PriceListRecord): PriceListDraft {
    return {
      code: record.code,
      englishName: record.englishName,
      arabicName: record.arabicName ?? '',
      currencyId: record.currencyId,
      customerId: record.customerId ?? '',
      organizationScopeKind: record.organizationScopeKind ?? '',
      organizationScopeId: record.organizationScopeId ?? '',
      priority: record.priority,
    };
  }

  private emptyDraft(): PriceListDraft {
    return { code: '', englishName: '', arabicName: '', currencyId: '', customerId: '', organizationScopeKind: '', organizationScopeId: '', priority: 100 };
  }

  private emptyPriceDraft(): PriceVersionDraft {
    return {
      productId: '',
      unitOfMeasureId: '',
      effectiveFrom: new Date().toISOString().slice(0, 10),
      effectiveTo: '',
      openEnded: true,
      price: 0,
      priceScale: 2,
      provenance: 'Manual',
      sourceReference: '',
    };
  }

  private emptyResolveDraft(record: PriceListRecord | null): ResolveDraft {
    return {
      productId: '',
      unitOfMeasureId: '',
      currencyId: record?.currencyId ?? '',
      customerId: record?.customerId ?? '',
      organizationScopeKind: record?.organizationScopeKind ?? '',
      organizationScopeId: record?.organizationScopeId ?? '',
      effectiveOn: new Date().toISOString().slice(0, 10),
      limitToThisList: true,
    };
  }

  private isProduct(record: MasterDataRecord): record is ProductRecord {
    return 'sku' in record;
  }

  private isUnit(record: MasterDataRecord): record is UnitOfMeasureRecord {
    return !('parentCategoryId' in record) && !('sku' in record) && !('contacts' in record) && !('revision' in record) && !('versions' in record);
  }

  private isCurrency(record: MasterDataRecord): record is CurrencyRecord {
    return 'revision' in record;
  }

  private isCustomer(record: MasterDataRecord): record is CustomerRecord {
    return 'contacts' in record && !('registrationReference' in record);
  }
}
