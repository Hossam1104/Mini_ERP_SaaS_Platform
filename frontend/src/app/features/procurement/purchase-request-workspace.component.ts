import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { SafeUiError, toSafeUiError } from '../../core/api/safe-error';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { MasterDataRecord, ProductRecord, UnitOfMeasureRecord } from '../master-data/master-data.models';
import { MasterDataService } from '../master-data/master-data.service';
import {
  PurchaseRequestAuditResponse,
  PurchaseRequestHistoryResponse,
  PurchaseRequestListItemResponse,
  PurchaseRequestOrganizationScopeResponse,
  PurchaseRequestResponse,
  PurchaseRequestStatus,
  PurchaseRequestWriteRequest,
} from './purchase-request.model';
import { PurchaseRequestService } from './purchase-request.service';

type Mode = 'list' | 'create' | 'edit' | 'view';
type DetailTab = 'summary' | 'lines' | 'history' | 'audit';
type LifecycleActionKind = 'submit' | 'approve' | 'reject' | 'return' | 'cancel';

interface LineDraft {
  productId: string;
  unitOfMeasureId: string;
  quantity: number;
  needByDate: string;
  purpose: string;
}

interface RequestDraft {
  companyId: string;
  branchId: string | null;
  purpose: string;
  lines: LineDraft[];
}

@Component({
  selector: 'app-purchase-request-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, DatePipe, FormsModule, NgTemplateOutlet],
  template: `
    <section class="pr-workspace" aria-labelledby="pr-title">
      <app-page-header class="page-header">
        <div class="page-header__copy" page-header-copy>
          <p class="eyebrow">{{ language.text('procurementNavLabel') }} / {{ language.text('purchaseRequestsNavLabel') }}</p>
          <h1 id="pr-title">{{ language.text('purchaseRequests') }}</h1>
          <p class="hero-lede">{{ language.text('purchaseRequestsLead') }}</p>
        </div>
        
      </app-page-header> *ngTemplateOutlet="listView" /> }
          @default { <ng-container *ngTemplateOutlet="detailView" /> }
        }
      </div>
    </section>

    <ng-template #listView>
      <section class="list-view ui-page" aria-labelledby="pr-title-list">
        <div class="section-heading">
          <div>
            <p class="eyebrow eyebrow--soft">{{ language.text('purchaseRequests') }}</p>
            <h2 id="pr-title-list">{{ language.text('purchaseRequests') }}</h2>
            <p>{{ language.text('purchaseRequestsLead') }}</p>
          </div>
          <div class="section-heading__actions">
            <button class="button button--quiet" type="button" (click)="loadList()" [disabled]="loading()" [attr.aria-label]="language.text('refresh')">↻ <span>{{ language.text('refresh') }}</span></button>
            <button class="button button--primary" type="button" (click)="startCreate()" [disabled]="!canMutate()" [title]="canMutate() ? '' : language.text('accessUnavailable')">＋ {{ language.text('newPurchaseRequest') }}</button>
          </div>
        </div>

        <form class="toolbar ui-toolbar" role="search" (ngSubmit)="onSearchSubmit()">
          <label class="form-field toolbar__status">
            <span>{{ language.text('scope') }}</span>
            <select [ngModel]="statusFilter()" (ngModelChange)="onStatusFilterChange($event)" name="prStatusFilter">
              <option value="">{{ language.text('purchaseRequests') }}</option>
              <option value="Draft">{{ language.text('prStatusDraft') }}</option>
              <option value="PendingApproval">{{ language.text('prStatusPendingApproval') }}</option>
              <option value="Approved">{{ language.text('prStatusApproved') }}</option>
              <option value="Rejected">{{ language.text('prStatusRejected') }}</option>
              <option value="ReturnedForChange">{{ language.text('prStatusReturnedForChange') }}</option>
              <option value="Cancelled">{{ language.text('prStatusCancelled') }}</option>
            </select>
          </label>
          <label class="search-field">
            <span class="sr-only">{{ language.text('clientSideSearch') }}</span>
            <span class="search-field__icon" aria-hidden="true">⌕</span>
            <input type="search" [value]="searchQuery()" (input)="onSearchInput($event)" [placeholder]="language.text('clientSideSearch')" />
          </label>
          @if (searchQuery()) { <button class="button button--quiet" type="button" (click)="clearSearch()">{{ language.text('clearSearch') }}</button> }
          <span class="toolbar__count">{{ filteredRecords().length }} {{ language.text('recordCount') }}</span>
        </form>
        <p class="term-hint">{{ language.text('clientSideSearchHint') }}</p>

        @if (loading()) {
          <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><div><b>{{ language.text('loadingPurchaseRequests') }}</b><p>{{ language.text('serverAuthority') }}</p></div></div>
        } @else if (listError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(listError()) }}</b><p>{{ language.text('purchaseRequestListLoadFailed') }}</p><button class="text-button" type="button" (click)="loadList()">{{ language.text('retry') }} ↗</button></div></div>
        } @else if (filteredRecords().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ language.text('noPurchaseRequests') }}</b><p>{{ language.text('noPurchaseRequestsLead') }}</p></div></div>
        } @else {
          <app-data-grid [caption]="language.text('purchaseRequests')" [rows]="filteredRecords()" [columns]="gridColumns()" [language]="language.language()" [pageSize]="filteredRecords().length || 1" [showPager]="false" [rowActions]="gridActions()" [countLabel]="language.text('recordCount')" [rowActionsLabel]="language.text('viewRecord')" (rowAction)="onGridAction($event)" />
        }
      </section>
    </ng-template>

    <ng-template #detailView>
      <section class="detail-view ui-page" aria-labelledby="detail-title">
        <div class="detail-topline"><button class="back-link" type="button" (click)="backToList()">← {{ language.text('purchaseRequests') }}</button></div>
        @if (detailLoading()) {
          <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loadingRecord') }}</b></div>
        } @else if (detailError()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(detailError()) }}</b><p>{{ language.text('detailLoadFailed') }}</p><button class="text-button" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') }} ↗</button></div></div>
        } @else {
          <div class="detail-heading">
            <div><p class="eyebrow eyebrow--soft">{{ mode() === 'create' ? language.text('newPurchaseRequest') : language.text('purchaseRequestDetail') }}</p><h2 id="detail-title">{{ mode() === 'create' ? language.text('newPurchaseRequest') : (language.text('purchaseRequestSingular') + ' · ' + shortReference(selectedRecord()!.id)) }}</h2><p>{{ language.text('purchaseRequestsLead') }}</p></div>
            @if (mode() === 'view' && selectedRecord(); as record) {
              <div class="detail-heading__actions">
                <span class="status-pill ui-status-chip" [class]="'status-pill ui-status-chip status-pill--' + statusTone(record.status)"><i aria-hidden="true">{{ statusIcon(record.status) }}</i>{{ statusLabel(record.status) }}</span>
                @if (record.canEdit) { <button class="button button--quiet" type="button" (click)="startEdit()">{{ language.text('editRecord') }}</button> }
                @if (record.canSubmit) { <button class="button button--primary" type="button" (click)="openLifecycle('submit')">{{ language.text('submitForApproval') }}</button> }
                @if (record.canApprove) { <button class="button button--primary" type="button" (click)="openLifecycle('approve')">{{ language.text('approveRequest') }}</button> }
                @if (record.canReturnForChange) { <button class="button button--quiet" type="button" (click)="openLifecycle('return')">{{ language.text('returnForChange') }}</button> }
                @if (record.canReject) { <button class="button button--danger" type="button" (click)="openLifecycle('reject')">{{ language.text('rejectRequest') }}</button> }
                @if (record.canCancel) { <button class="button button--danger" type="button" (click)="openLifecycle('cancel')">{{ language.text('cancelRequest') }}</button> }
              </div>
            }
          </div>

          @if (mutationError()) {
            <div class="inline-alert" role="alert">
              <b>{{ errorMessage(mutationError()) }}</b>
              @if (mutationError()?.code === 'concurrency_conflict') {
                <span>{{ language.text('prConcurrencyConflictError') }}</span>
                <button class="text-button" type="button" (click)="reloadDetail()">{{ language.text('reloadLatestVersion') }}</button>
              }
            </div>
          }
          @if (formNotice()) { <div class="inline-alert inline-alert--success" role="status">{{ formNotice() }}</div> }

          @if (mode() === 'view' && selectedRecord()) {
            <nav class="tabs" role="tablist" [attr.aria-label]="language.text('purchaseRequestDetail')" (keydown)="onTabsKeydown($event)">
              <button id="pr-tab-summary" role="tab" type="button" [attr.aria-selected]="detailTab() === 'summary'" [attr.aria-controls]="'pr-tabpanel-summary'" [attr.tabindex]="detailTab() === 'summary' ? 0 : -1" [class.is-active]="detailTab() === 'summary'" (click)="setTab('summary')">{{ language.text('prTabSummary') }}</button>
              <button id="pr-tab-lines" role="tab" type="button" [attr.aria-selected]="detailTab() === 'lines'" [attr.aria-controls]="'pr-tabpanel-lines'" [attr.tabindex]="detailTab() === 'lines' ? 0 : -1" [class.is-active]="detailTab() === 'lines'" (click)="setTab('lines')">{{ language.text('prTabLines') }} ({{ selectedRecord()!.lines.length }})</button>
              <button id="pr-tab-history" role="tab" type="button" [attr.aria-selected]="detailTab() === 'history'" [attr.aria-controls]="'pr-tabpanel-history'" [attr.tabindex]="detailTab() === 'history' ? 0 : -1" [class.is-active]="detailTab() === 'history'" (click)="setTab('history')">{{ language.text('prTabHistory') }}</button>
              <button id="pr-tab-audit" role="tab" type="button" [attr.aria-selected]="detailTab() === 'audit'" [attr.aria-controls]="'pr-tabpanel-audit'" [attr.tabindex]="detailTab() === 'audit' ? 0 : -1" [class.is-active]="detailTab() === 'audit'" (click)="setTab('audit')">{{ language.text('prTabAudit') }}</button>
            </nav>

            @switch (detailTab()) {
              @case ('summary') { <div id="pr-tabpanel-summary" role="tabpanel" aria-labelledby="pr-tab-summary"><ng-container *ngTemplateOutlet="summaryTab" /></div> }
              @case ('lines') { <div id="pr-tabpanel-lines" role="tabpanel" aria-labelledby="pr-tab-lines"><ng-container *ngTemplateOutlet="linesTab" /></div> }
              @case ('history') { <div id="pr-tabpanel-history" role="tabpanel" aria-labelledby="pr-tab-history"><ng-container *ngTemplateOutlet="historyTab" /></div> }
              @case ('audit') { <div id="pr-tabpanel-audit" role="tabpanel" aria-labelledby="pr-tab-audit"><ng-container *ngTemplateOutlet="auditTab" /></div> }
            }
          } @else {
            <form class="edit-card" (ngSubmit)="save()" novalidate>
              @if (formError()) { <div class="form-summary" role="alert">{{ language.text('validationSummary') }}</div> }
              <ng-container *ngTemplateOutlet="editableFields" />
              <div class="form-actions"><button class="button button--quiet" type="button" (click)="cancelEdit()">{{ language.text('cancel') }}</button><button class="button button--primary" type="submit" [disabled]="saving() || isOrganizationScopeUnavailable()">{{ saving() ? language.text('actionInProgress') : (mode() === 'create' ? language.text('saveDraft') : language.text('saveRecord')) }}</button></div>
            </form>
          }
        }
      </section>
    </ng-template>

    <ng-template #summaryTab>
      @if (selectedRecord(); as record) {
        <div class="field-read-grid">
          <div><span>{{ language.text('organizationScopeCompany') }}</span><b>{{ companyLabel(record.companyId) }}</b></div>
          <div><span>{{ language.text('organizationScopeBranch') }}</span><b>{{ branchLabel(record.branchId) }}</b></div>
          <div><span>{{ language.text('prRequester') }}</span><b>{{ record.requesterId }}</b></div>
          <div class="field-read-grid__wide"><span>{{ language.text('purpose') }}</span><b>{{ valueOrEmpty(record.purpose) }}</b></div>
          <div><span>{{ language.text('prRequestedOn') }}</span><b>{{ record.createdAt | date:'medium' }}</b></div>
          <div><span>{{ language.text('prUpdatedOn') }}</span><b>{{ record.updatedAt | date:'medium' }}</b></div>
          @if (record.submittedAt) { <div><span>{{ language.text('prSubmittedOn') }}</span><b>{{ record.submittedAt | date:'medium' }}</b></div> }
          @if (record.approvedAt) { <div><span>{{ language.text('prApprovedOn') }}</span><b>{{ record.approvedAt | date:'medium' }}</b></div> }
          @if (record.cancelledAt) { <div><span>{{ language.text('prCancelledOn') }}</span><b>{{ record.cancelledAt | date:'medium' }}</b></div> }
        </div>
        <div class="approval-panel">
          <p class="eyebrow eyebrow--soft">{{ language.text('prApprovalStage') }}</p>
          @if (record.approval; as approval) {
            <div class="field-read-grid">
              <div><span>{{ language.text('prApprovalPolicy') }}</span><b>{{ approval.policyId }} · v{{ approval.policyVersion }}</b></div>
              <div><span>{{ language.text('prApprovalStage') }}</span><b>{{ approval.stageKey }} ({{ approval.stageIndex + 1 }})</b></div>
              <div><span>{{ language.text('prApprovalsRecorded') }}</span><b>{{ approval.recordedApprovals }} / {{ approval.requiredApprovals }}</b></div>
            </div>
          } @else {
            <p class="muted-line">{{ language.text('prNoApproval') }}</p>
          }
        </div>
        <div class="approval-panel">
          <p class="eyebrow eyebrow--soft">{{ language.text('technicalDetailsSection') }}</p>
          <div class="field-read-grid">
            <div><span>{{ language.text('fullRequestId') }}</span><b>{{ record.id }}</b></div>
            <div><span>{{ language.text('companyId') }}</span><b>{{ record.companyId }}</b></div>
            @if (record.branchId) { <div><span>{{ language.text('branchId') }}</span><b>{{ record.branchId }}</b></div> }
          </div>
        </div>
        <p class="boundary-note">{{ language.text('purchaseRequestBoundary') }}</p>
      }
    </ng-template>

    <ng-template #linesTab>
      @if (selectedRecord(); as record) {
        @if (record.lines.length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ language.text('noLines') }}</b></div></div>
        } @else {
          <div class="record-table-wrap ui-grid-shell">
            <table class="record-table ui-grid">
              <thead><tr><th scope="col">{{ language.text('product') }}</th><th scope="col">{{ language.text('unitOfMeasure') }}</th><th scope="col">{{ language.text('quantity') }}</th><th scope="col">{{ language.text('needByDate') }}</th><th scope="col">{{ language.text('purpose') }}</th></tr></thead>
              <tbody>
                @for (line of record.lines; track line.id) {
                  <tr>
                    <td><span class="record-name">{{ line.productSku }}</span><small>{{ line.productName }}</small></td>
                    <td><span class="record-name">{{ line.unitOfMeasureCode }}</span></td>
                    <td><span class="record-name">{{ line.quantity }}</span></td>
                    <td><span class="record-name">{{ line.needByDate | date:'mediumDate' }}</span></td>
                    <td><span class="record-name">{{ valueOrEmpty(line.purpose) }}</span></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      }
    </ng-template>

    <ng-template #historyTab>
      @if (historyLoading()) {
        <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
      } @else if (historyError()) {
        <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(historyError()) }}</b><button class="text-button" type="button" (click)="setTab('history', true)">{{ language.text('retry') }} ↗</button></div></div>
      } @else if (historyEntries().length === 0) {
        <p class="muted-line">{{ language.text('noRecords') }}</p>
      } @else {
        <div class="audit-table-wrap">
          <table class="audit-table">
            <thead><tr><th>{{ language.text('auditWhen') }}</th><th>{{ language.text('auditAction') }}</th><th>{{ language.text('scope') }}</th><th>{{ language.text('auditReason') }}</th></tr></thead>
            <tbody>
              @for (entry of historyEntries(); track entry.evidenceId) {
                <tr><td>{{ entry.occurredAt | date:'medium' }}</td><td>{{ entry.action }}</td><td>{{ statusLabel(entry.fromStatus) }} → {{ statusLabel(entry.toStatus) }}</td><td>{{ valueOrEmpty(entry.reason) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
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
        <div class="audit-table-wrap">
          <table class="audit-table">
            <thead><tr><th>{{ language.text('auditWhen') }}</th><th>{{ language.text('auditAction') }}</th><th>{{ language.text('auditDecision') }}</th><th>{{ language.text('auditReason') }}</th></tr></thead>
            <tbody>
              @for (entry of auditEntries(); track entry.evidenceId) {
                <tr><td>{{ entry.occurredAt | date:'medium' }}</td><td>{{ entry.operationId }}</td><td>{{ entry.decision }}</td><td><span>{{ valueOrEmpty(entry.reason) }}</span>@if (entry.afterSummary) { <small>{{ entry.afterSummary }}</small> }</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </ng-template>

    <ng-template #editableFields>
      <div class="form-section">
        <div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">01 / {{ language.text('requestContextSection') }}</p><h3>{{ language.text('requestContextSection') }}</h3></div><span>{{ language.text('serverAuthority') }}</span></div>

        @if (mode() === 'edit') {
          <div class="form-grid">
            <div class="form-field"><span>{{ language.text('organizationScopeCompany') }}</span><b>{{ companyLabel(draft.companyId) }}</b></div>
            <div class="form-field"><span>{{ language.text('organizationScopeBranch') }}</span><b>{{ branchLabel(draft.branchId) }}</b></div>
          </div>
          <p class="term-hint">{{ language.text('organizationScopeLockedHint') }}</p>
        } @else if (organizationScopesLoading()) {
          <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
        } @else if (organizationScopes().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ language.text('noOrganizationScopeAvailable') }}</b><p>{{ language.text('noOrganizationScopeAvailableLead') }}</p></div></div>
        } @else if (organizationScopes().length === 1) {
          <div class="form-grid">
            <div class="form-field"><span>{{ language.text('organizationScopeCompany') }}</span><b>{{ companyLabel(draft.companyId) }}</b></div>
            <div class="form-field"><span>{{ language.text('organizationScopeBranch') }}</span><b>{{ branchLabel(draft.branchId) }}</b></div>
          </div>
        } @else {
          <div class="form-grid">
            <label class="form-field" [class.has-error]="invalid('organizationScope')">
              <span>{{ language.text('organizationScope') }} <em>*</em></span>
              <select [ngModel]="selectedOrganizationScopeKey()" (ngModelChange)="onOrganizationScopeChange($event)" name="prOrganizationScope">
                <option value="">{{ language.text('selectOrganizationScope') }}</option>
                @for (option of organizationScopes(); track organizationScopeKey(option)) {
                  <option [value]="organizationScopeKey(option)">{{ option.displayName }}</option>
                }
              </select>
              <small>{{ invalid('organizationScope') ? language.text('required') : language.text('organizationScopeSelectHint') }}</small>
            </label>
          </div>
        }

        <div class="form-grid">
          <label class="form-field form-field--full"><span>{{ language.text('purpose') }}</span><textarea rows="3" [ngModel]="draft.purpose" (ngModelChange)="setDraftField('purpose', $event)" name="prPurpose"></textarea><small>{{ language.text('purposeHint') }}</small></label>
        </div>
        @if (referenceLoadFailed()) { <p class="muted-line">{{ language.text('accessUnavailable') }}</p> }
      </div>

      @if (!isOrganizationScopeUnavailable()) {
        <div class="form-section">
          <div class="form-section__heading"><div><p class="eyebrow eyebrow--soft">02 / {{ language.text('requestLines') }}</p><h3>{{ language.text('requestLines') }}</h3></div><button class="button button--quiet" type="button" (click)="addLine()">＋ {{ language.text('addLine') }}</button></div>
          <p class="term-hint">{{ language.text('requestLinesLead') }}</p>
          @if (invalid('lines')) { <p class="muted-line" role="alert">{{ language.text('noLines') }}</p> }
          <div class="line-editor">
            @for (line of draft.lines; track $index; let i = $index) {
              <div class="line-row">
                <div class="line-row__grid">
                  <label class="form-field" [class.has-error]="lineInvalid(i, 'productId')"><span>{{ language.text('product') }} <em>*</em></span><select [ngModel]="line.productId" (ngModelChange)="setLineField(i, 'productId', $event)" [name]="'lineProduct' + i"><option value="">{{ language.text('selectProduct') }}</option>@for (p of productChoices(); track p.id) { <option [value]="p.id">{{ productOptionLabel(p) }}</option> }</select><small>{{ lineInvalid(i, 'productId') ? language.text('lineProductRequired') : '' }}</small></label>
                  <label class="form-field" [class.has-error]="lineInvalid(i, 'unitOfMeasureId')"><span>{{ language.text('unitOfMeasure') }} <em>*</em></span><select [ngModel]="line.unitOfMeasureId" (ngModelChange)="setLineField(i, 'unitOfMeasureId', $event)" [name]="'lineUnit' + i"><option value="">{{ language.text('selectUnit') }}</option>@for (u of unitChoices(); track u.id) { <option [value]="u.id">{{ unitOptionLabel(u) }}</option> }</select><small>{{ lineInvalid(i, 'unitOfMeasureId') ? language.text('lineUnitRequired') : '' }}</small></label>
                  <label class="form-field" [class.has-error]="lineInvalid(i, 'quantity')"><span>{{ language.text('quantity') }} <em>*</em></span><input type="number" min="0.000001" step="any" [ngModel]="line.quantity" (ngModelChange)="setLineField(i, 'quantity', $event)" [name]="'lineQuantity' + i" /><small>{{ lineInvalid(i, 'quantity') ? language.text('lineQuantityRequired') : '' }}</small></label>
                  <label class="form-field" [class.has-error]="lineInvalid(i, 'needByDate')"><span>{{ language.text('needByDate') }} <em>*</em></span><input type="date" [ngModel]="line.needByDate" (ngModelChange)="setLineField(i, 'needByDate', $event)" [name]="'lineNeedByDate' + i" /><small>{{ lineInvalid(i, 'needByDate') ? language.text('lineNeedByDateRequired') : '' }}</small></label>
                  <label class="form-field" [class.has-error]="lineInvalid(i, 'purpose')"><span>{{ language.text('purpose') }} <em>*</em></span><input [ngModel]="line.purpose" (ngModelChange)="setLineField(i, 'purpose', $event)" [name]="'linePurpose' + i" /><small>{{ lineInvalid(i, 'purpose') ? language.text('linePurposeRequired') : '' }}</small></label>
                </div>
                <div class="line-row__remove"><button class="text-button" type="button" (click)="removeLine(i)" [disabled]="draft.lines.length === 1">✕ {{ language.text('removeLine') }}</button></div>
              </div>
            }
          </div>
        </div>
      }
    </ng-template>

    @if (lifecycleAction(); as action) {
      <div class="dialog-backdrop" role="presentation" (click)="closeLifecycle()">
        <section class="lifecycle-dialog" role="dialog" aria-modal="true" aria-labelledby="lifecycle-title" (click)="$event.stopPropagation()" (keydown)="onLifecycleDialogKeydown($event)">
          <p class="eyebrow eyebrow--soft">{{ language.text('purchaseRequestDetail') }}</p>
          <h2 id="lifecycle-title">{{ lifecycleTitle(action) }}</h2>
          <p class="term-hint">{{ lifecycleLead(action) }}</p>

          @if (action === 'reject' || action === 'return') {
            <label class="form-field" [class.has-error]="lifecycleReasonError()"><span>{{ language.text('reasonRequired') }} <em>*</em></span><textarea rows="3" [ngModel]="lifecycleReason()" (ngModelChange)="lifecycleReason.set($event)" name="lifecycleReasonField"></textarea><small>{{ lifecycleReasonError() ? language.text('required') : language.text('reasonRequiredHint') }}</small></label>
          } @else if (action === 'cancel') {
            <label class="form-field"><span>{{ language.text('lifecycleReason') }}</span><textarea rows="3" [ngModel]="lifecycleReason()" (ngModelChange)="lifecycleReason.set($event)" name="lifecycleReasonField"></textarea><small>{{ language.text('lifecycleReasonHint') }}</small></label>
          }

          @if (mutationError()) {
            <div class="inline-alert" role="alert">
              <b>{{ errorMessage(mutationError()) }}</b>
              @if (mutationError()?.code === 'concurrency_conflict') { <button class="text-button" type="button" (click)="reloadAndCloseLifecycle()">{{ language.text('reloadLatestVersion') }}</button> }
            </div>
          }
          <div class="form-actions">
            <button id="lifecycle-dialog-cancel" class="button button--quiet" type="button" (click)="closeLifecycle()">{{ language.text('cancel') }}</button>
            <button class="button button--primary" type="button" (click)="confirmLifecycle()" [disabled]="lifecycleSaving()">{{ lifecycleSaving() ? language.text('actionInProgress') : lifecycleConfirmLabel(action) }}</button>
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .pr-workspace { display: grid; gap: 1.35rem; }
    
    
    .hero-copy, .hero-facts { position: relative; z-index: 1; }
    .hero-copy { max-width: 42rem; }
    .eyebrow { margin: 0 0 .55rem; color: #bee5d0; font-size: .68rem; font-weight: 800; letter-spacing: .14em; text-transform: uppercase; }
    .eyebrow--soft { color: var(--accent-strong); }
    h1, h2, h3, p { margin-block-start: 0; }
    h1 { margin-block-end: .85rem; font: 800 clamp(2rem, 5vw, 3.4rem)/.98 var(--font-display); letter-spacing: -.05em; }
    .hero-lede { max-width: 38rem; margin: 0; color: #d7e7e1; font-size: .95rem; line-height: 1.6; }
    .hero-facts { display: grid; align-content: end; gap: .7rem; min-width: 15rem; }
    .hero-fact { display: flex; align-items: center; gap: .7rem; border-block-start: 1px solid rgb(255 255 255 / 25%); padding-block-start: .65rem; }
    .hero-fact--quiet { opacity: .72; }
    .hero-fact__mark { color: #e9b965; font: 700 .72rem/1 ui-monospace, monospace; }
    .hero-fact b, .hero-fact small { display: block; }
    .hero-fact b { font-size: .75rem; }
    .hero-fact small { margin-block-start: .2rem; color: #b9d0c8; font-size: .68rem; }
    .workspace-panel { min-width: 0; border: 1px solid var(--line); border-radius: 1.15rem; background: var(--surface-raised); box-shadow: var(--shadow-soft); }
    .list-view, .detail-view { padding: clamp(1rem, 2.5vw, 1.65rem); }
    .section-heading, .detail-heading, .detail-topline, .toolbar, .form-section__heading, .form-actions { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .section-heading { align-items: flex-end; margin-block-end: 1.35rem; flex-wrap: wrap; }
    .section-heading h2, .detail-heading h2, .section-heading h3 { margin: 0; color: var(--ink); font: 800 clamp(1.3rem, 3vw, 1.9rem)/1 var(--font-display); letter-spacing: -.04em; }
    .section-heading p:not(.eyebrow), .detail-heading p:not(.eyebrow) { max-width: 37rem; margin: .5rem 0 0; color: var(--ink-muted); font-size: .82rem; line-height: 1.5; }
    .section-heading__actions, .detail-heading__actions { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: .5rem; }
    .button { min-height: 2.35rem; border: 1px solid transparent; border-radius: .55rem; padding: .58rem .82rem; font-size: .76rem; font-weight: 800; cursor: pointer; }
    .button:disabled { cursor: not-allowed; opacity: .45; }
    .button--primary { color: #173b35; background: var(--accent); }
    .button--primary:hover:not(:disabled) { background: #c4ead1; }
    .button--quiet { border-color: var(--line); color: var(--ink-muted); background: transparent; }
    .button--quiet:hover:not(:disabled) { border-color: var(--line-strong); color: var(--ink); background: var(--canvas); }
    .button--danger { color: #fff; background: var(--danger); }
    .toolbar { align-items: stretch; flex-wrap: wrap; margin-block-end: .5rem; border-block: 1px solid var(--line); padding-block: .75rem; }
    .toolbar__status { flex: 0 0 auto; min-width: 12rem; }
    .toolbar__status select { min-height: 2.35rem; border: 1px solid var(--line); border-radius: .5rem; padding-inline: .6rem; background: var(--canvas); font-size: .78rem; }
    .search-field { display: flex; align-items: center; flex: 1 1 16rem; gap: .5rem; border: 1px solid var(--line); border-radius: .55rem; padding-inline: .7rem; background: var(--canvas); }
    .search-field:focus-within { border-color: var(--focus); box-shadow: 0 0 0 3px rgb(13 138 131 / 12%); }
    .search-field__icon { color: var(--accent-strong); font-size: 1.3rem; }
    .search-field input { min-width: 0; width: 100%; border: 0; outline: 0; color: var(--ink); background: transparent; font-size: .8rem; }
    .toolbar__count { align-self: center; margin-inline-start: auto; color: var(--ink-muted); font: 700 .68rem/1 ui-monospace, monospace; white-space: nowrap; }
    .term-hint { margin: 0 0 1rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.45; }
    .record-table-wrap, .audit-table-wrap { overflow-x: auto; }
    .record-table, .audit-table { width: 100%; border-collapse: collapse; font-size: .78rem; }
    .record-table th, .record-table td, .audit-table th, .audit-table td { border-block-end: 1px solid var(--line); padding: .85rem .7rem; text-align: start; vertical-align: middle; }
    .record-table th, .audit-table th { color: var(--ink-muted); font-size: .64rem; letter-spacing: .08em; text-transform: uppercase; }
    .record-table tbody tr:hover { background: #f7faf7; }
    .record-code { display: block; border: 0; padding: 0; color: var(--accent-strong); background: none; font: 800 .82rem/1.2 ui-monospace, monospace; cursor: pointer; }
    .record-code:hover { text-decoration: underline; }
    .record-table small, .audit-table small { display: block; max-width: 22rem; margin-block-start: .25rem; overflow: hidden; color: var(--ink-muted); font-size: .66rem; text-overflow: ellipsis; white-space: nowrap; }
    .record-name { display: block; color: var(--ink); font-weight: 700; }
    .status-pill { display: inline-flex; align-items: center; gap: .35rem; border-radius: 99px; padding: .3rem .55rem; font-size: .66rem; font-weight: 800; white-space: nowrap; }
    .status-pill i { font-style: normal; font-size: .8rem; line-height: 1; }
    .status-pill--neutral { color: var(--ink-muted); background: var(--support-soft); }
    .status-pill--progress { color: var(--accent-strong); background: var(--accent-soft); }
    .status-pill--success { color: var(--success); background: var(--accent-soft); }
    .status-pill--danger { color: #fff; background: var(--danger); }
    .status-pill--warning { color: color-mix(in srgb, var(--danger) 55%, var(--ink)); background: color-mix(in srgb, var(--danger) 14%, var(--surface-raised)); }
    .status-pill--muted { color: var(--ink-muted); background: var(--canvas); }
    .table-action { text-align: end !important; }
    .icon-button { display: inline-grid; place-items: center; width: 2rem; height: 2rem; border: 1px solid var(--line); border-radius: .5rem; color: var(--accent-strong); background: transparent; cursor: pointer; }
    .icon-button:hover { border-color: var(--accent-strong); background: var(--accent-soft); }
    .record-cards { display: none; }
    .state-card { display: flex; align-items: flex-start; gap: .8rem; border: 1px dashed var(--line-strong); border-radius: .8rem; padding: 1.35rem; background: var(--canvas); }
    .state-card b { color: var(--ink); font-size: .85rem; }
    .state-card p { margin: .3rem 0 0; color: var(--ink-muted); font-size: .75rem; line-height: 1.5; }
    .state-card--error { border-style: solid; border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); background: color-mix(in srgb, var(--danger) 5%, var(--surface-raised)); }
    .state-card--empty { min-height: 8rem; align-items: center; }
    .state-icon { display: grid; flex: 0 0 1.8rem; place-items: center; width: 1.8rem; height: 1.8rem; border-radius: .5rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); font-weight: 900; }
    .state-card--empty .state-icon { color: var(--accent-strong); background: var(--accent-soft); }
    .loader { width: 1.2rem; height: 1.2rem; border: 2px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: spin .8s linear infinite; }
    .text-button, .back-link { border: 0; padding: 0; color: var(--accent-strong); background: transparent; font-size: .74rem; font-weight: 800; cursor: pointer; }
    .text-button:disabled { color: var(--ink-muted); cursor: not-allowed; opacity: .5; }
    .text-button { display: block; margin-block-start: .7rem; }
    .detail-topline { margin-block-end: 1.25rem; justify-content: flex-start; }
    .back-link { color: var(--ink-muted); }
    .back-link:hover { color: var(--accent-strong); }
    .detail-heading { align-items: flex-end; margin-block-end: 1.25rem; flex-wrap: wrap; }
    .tabs { display: flex; gap: .2rem; margin-block-end: 1.1rem; border-block-end: 1px solid var(--line); overflow-x: auto; }
    .tabs button { border: 0; border-block-end: 2px solid transparent; padding: .65rem .2rem; margin-inline-end: 1.2rem; color: var(--ink-muted); background: transparent; font: 800 .78rem/1 var(--font-sans); cursor: pointer; white-space: nowrap; }
    .tabs button:hover { color: var(--ink); }
    .tabs button:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }
    .tabs button.is-active { color: var(--accent-strong); border-block-end-color: var(--accent-strong); }
    .field-read-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .9rem 1.2rem; }
    .field-read-grid > div { min-width: 0; border-block-start: 2px solid var(--line); padding-block-start: .5rem; }
    .field-read-grid span { display: block; color: var(--ink-muted); font-size: .67rem; font-weight: 700; }
    .field-read-grid b { display: block; margin-block-start: .3rem; overflow-wrap: anywhere; color: var(--ink); font-size: .8rem; line-height: 1.4; }
    .field-read-grid__wide { grid-column: 1 / -1; }
    .approval-panel { margin-block-start: 1.25rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }
    .boundary-note { margin: 1rem 0 0; border-inline-start: 3px solid var(--accent); padding-inline-start: .7rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.5; }
    .muted-line { margin: 0; color: var(--ink-muted); font-size: .75rem; }
    .edit-card { padding: 0; overflow: hidden; }
    .form-section { padding: 1rem; border-block-end: 1px solid var(--line); }
    .form-section:last-of-type { border-block-end: 0; }
    .form-section__heading { align-items: flex-start; margin-block-end: 1rem; }
    .form-section__heading h3 { margin: 0; font: 800 1.05rem/1 var(--font-display); }
    .form-section__heading > span { color: var(--ink-muted); font-size: .68rem; }
    .form-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: .9rem; }
    .form-field { display: grid; gap: .35rem; min-width: 0; }
    .form-field > span { color: var(--ink-muted); font-size: .7rem; font-weight: 800; }
    .form-field em { color: var(--danger); font-style: normal; }
    .form-field input, .form-field select, .form-field textarea { width: 100%; border: 1px solid var(--line); border-radius: .45rem; padding: .6rem .65rem; color: var(--ink); background: var(--surface-raised); font-size: .78rem; font-family: inherit; }
    .form-field textarea { resize: vertical; }
    .form-field input:focus, .form-field select:focus, .form-field textarea:focus { border-color: var(--focus); outline: 0; box-shadow: 0 0 0 3px rgb(13 138 131 / 10%); }
    .form-field.has-error input, .form-field.has-error select, .form-field.has-error textarea { border-color: var(--danger); }
    .form-field small { min-height: 1rem; color: var(--danger); font-size: .62rem; line-height: 1.35; }
    .form-field:not(.has-error) small { color: var(--ink-muted); }
    .form-field--full { grid-column: 1 / -1; }
    .form-summary, .inline-alert { margin: 1rem 1rem 0; border-radius: .55rem; padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .74rem; }
    .inline-alert { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; justify-content: space-between; margin: 0 0 1rem; }
    .inline-alert span { color: var(--ink-muted); }
    .inline-alert--success { color: var(--success); background: var(--accent-soft); }
    .form-actions { justify-content: flex-end; border-block-start: 1px solid var(--line); padding: .85rem 1rem; background: var(--canvas); }
    .line-editor { display: grid; gap: .75rem; }
    .line-row { display: grid; gap: .6rem; border: 1px solid var(--line); border-radius: .8rem; padding: .85rem; background: var(--canvas); }
    .line-row__grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: .6rem; }
    .line-row__remove { display: flex; justify-content: flex-end; }
    .dialog-backdrop { display: grid; position: fixed; z-index: 5; inset: 0; place-items: center; padding: 1rem; background: rgb(16 39 37 / 48%); }
    .lifecycle-dialog { width: min(100%, 30rem); border: 1px solid var(--line); border-radius: 1rem; padding: 1.35rem; background: var(--surface-raised); box-shadow: var(--shadow-card); }
    .lifecycle-dialog h2 { margin: 0 0 .5rem; font: 800 1.35rem/1.05 var(--font-display); }
    .lifecycle-dialog .form-field { margin-block-start: .85rem; }
    .lifecycle-dialog .form-actions { margin: 1.25rem -1.35rem -1.35rem; }
    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; } }
    @media (max-width: 980px) {  .hero-facts { grid-template-columns: repeat(2, minmax(0, 1fr)); min-width: 0; } .line-row__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 720px) {
      .record-table-wrap { display: none; }
      .record-cards { display: grid; gap: .65rem; }
      .record-card { display: grid; gap: .5rem; border: 1px solid var(--line); border-radius: .8rem; padding: .85rem; text-align: start; background: var(--canvas); cursor: pointer; }
      .record-card__top { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
      .record-card__facts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem .8rem; margin-block-start: .3rem; }
      .record-card__facts span { display: block; color: var(--ink-muted); font-size: .64rem; font-weight: 700; }
      .record-card__facts b { display: block; margin-block-start: .15rem; color: var(--ink); font-size: .76rem; }
      .section-heading, .detail-heading, .toolbar { align-items: stretch; flex-direction: column; }
      .section-heading__actions, .detail-heading__actions { justify-content: flex-start; }
      .toolbar__count { margin-inline-start: 0; }
      .form-grid { grid-template-columns: 1fr; }
      .field-read-grid { grid-template-columns: 1fr; }
      .field-read-grid__wide { grid-column: auto; }
      .hero-facts { grid-template-columns: 1fr; }
      .line-row__grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 460px) {   .list-view, .detail-view { padding: .8rem; } .button span { display: none; } .form-actions { flex-wrap: wrap; } .form-actions .button { flex: 1; } }
  `,
})
export class PurchaseRequestWorkspaceComponent {
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  private readonly purchaseRequests = inject(PurchaseRequestService);
  private readonly data = inject(MasterDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly mode = signal<Mode>('list');

  readonly records = signal<PurchaseRequestListItemResponse[]>([]);
  readonly loading = signal(false);
  readonly listError = signal<SafeUiError | null>(null);
  readonly statusFilter = signal<PurchaseRequestStatus | ''>('');
  readonly searchQuery = signal('');

  readonly detailTab = signal<DetailTab>('summary');
  readonly detailLoading = signal(false);
  readonly detailError = signal<SafeUiError | null>(null);
  readonly selectedRecord = signal<PurchaseRequestResponse | null>(null);
  readonly mutationError = signal<SafeUiError | null>(null);
  readonly formError = signal(false);
  readonly fieldErrors = signal<ReadonlySet<string>>(new Set());
  readonly formNotice = signal<string | null>(null);
  readonly saving = signal(false);

  readonly lifecycleAction = signal<LifecycleActionKind | null>(null);
  readonly lifecycleSaving = signal(false);
  readonly lifecycleReason = signal('');
  readonly lifecycleReasonError = signal(false);

  readonly productChoices = signal<ProductRecord[]>([]);
  readonly unitChoices = signal<UnitOfMeasureRecord[]>([]);
  readonly referenceLoadFailed = signal(false);

  readonly organizationScopes = signal<PurchaseRequestOrganizationScopeResponse[]>([]);
  readonly organizationScopesLoading = signal(false);
  readonly organizationScopesLoaded = signal(false);

  readonly historyEntries = signal<PurchaseRequestHistoryResponse[]>([]);
  readonly historyLoading = signal(false);
  readonly historyError = signal<SafeUiError | null>(null);
  readonly historyLoaded = signal(false);

  readonly auditEntries = signal<PurchaseRequestAuditResponse[]>([]);
  readonly auditLoading = signal(false);
  readonly auditError = signal<SafeUiError | null>(null);
  readonly auditLoaded = signal(false);

  readonly filteredRecords = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    if (!query) return this.records();
    return this.records().filter((record) =>
      record.id.toLowerCase().includes(query)
      || this.companyLabel(record.companyId).toLowerCase().includes(query)
      || this.branchLabel(record.branchId).toLowerCase().includes(query)
      || (record.purpose ?? '').toLowerCase().includes(query),
    );
  });

  gridColumns(): DataGridColumn<PurchaseRequestListItemResponse>[] { return [
    { key: 'status', label: this.language.text('prStatusColumn'), value: (record) => record.status, display: (record) => this.statusLabel(record.status), badge: true, filter: 'select', filterOptions: ['Draft', 'PendingApproval', 'Approved', 'Rejected', 'ReturnedForChange', 'Cancelled'].map((status) => ({ value: status, label: this.statusLabel(status as PurchaseRequestStatus) })) },
    { key: 'purpose', label: this.language.text('purpose'), value: (record) => this.valueOrEmpty(record.purpose), secondaryText: (record) => this.shortReference(record.id), link: (record) => `/app/procurement/purchase-requests/${record.id}`, filter: 'text' },
    { key: 'company', label: this.language.text('prOrganizationColumn'), value: (record) => this.companyLabel(record.companyId), filter: 'text' },
    { key: 'branch', label: this.language.text('organizationScopeBranch'), value: (record) => this.branchLabel(record.branchId), filter: 'text' },
    { key: 'lines', label: this.language.text('requestLines'), value: (record) => record.lineCount, filter: 'number-range', align: 'end' },
    { key: 'updated', label: this.language.text('prUpdatedColumn'), value: (record) => record.updatedAt, display: (record) => new Date(record.updatedAt).toLocaleDateString(this.language.language()), filter: 'date-range' },
  ]; }

  gridActions(): readonly { key: string; label: string }[] { return [{ key: 'view', label: this.language.text('viewRecord') }]; }
  onGridAction(event: { row: PurchaseRequestListItemResponse }): void { this.openRecord(event.row.id); }

  private readonly detailTabOrder: DetailTab[] = ['summary', 'lines', 'history', 'audit'];
  private lastFocusedElement: HTMLElement | null = null;
  private loadSequence = 0;

  draft: RequestDraft = this.emptyDraft();

  constructor() {
    void this.loadReferenceData();
    void this.loadOrganizationScopes();
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const path = this.route.snapshot.routeConfig?.path ?? '';
      const id = params.get('id');
      if (path === 'procurement/purchase-requests/new') {
        this.prepareCreate();
        return;
      }
      if (path === 'procurement/purchase-requests/:id/edit' && id) {
        void this.loadDetailById(id, 'edit');
        return;
      }
      if (path === 'procurement/purchase-requests/:id' && id) {
        void this.loadDetailById(id, 'view');
        return;
      }
      this.mode.set('list');
      this.selectedRecord.set(null);
      this.detailError.set(null);
      this.loadList();
    });
  }

  loadList(): void {
    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.listError.set(null);
    void firstValueFrom(this.purchaseRequests.list(this.statusFilter() || undefined))
      .then((records) => {
        if (sequence !== this.loadSequence) return;
        this.records.set(records ?? []);
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
      const [products, units] = await Promise.all([
        firstValueFrom(this.data.list('products')),
        firstValueFrom(this.data.list('units')),
      ]);
      this.productChoices.set((products ?? []).filter((record): record is ProductRecord => this.isProduct(record)));
      this.unitChoices.set((units ?? []).filter((record): record is UnitOfMeasureRecord => this.isUnit(record)));
    } catch {
      this.referenceLoadFailed.set(true);
    }
  }

  private async loadOrganizationScopes(): Promise<void> {
    this.organizationScopesLoading.set(true);
    try {
      const options = await firstValueFrom(this.purchaseRequests.organizationScopes());
      this.organizationScopes.set(options ?? []);
    } catch {
      this.organizationScopes.set([]);
    } finally {
      this.organizationScopesLoading.set(false);
      this.organizationScopesLoaded.set(true);
      this.applyDefaultOrganizationScope();
    }
  }

  private applyDefaultOrganizationScope(): void {
    if (this.mode() !== 'create' || this.draft.companyId) return;
    const options = this.organizationScopes();
    if (options.length === 1) {
      this.draft = { ...this.draft, companyId: options[0].companyId, branchId: options[0].branchId };
    }
  }

  onStatusFilterChange(value: PurchaseRequestStatus | ''): void {
    this.statusFilter.set(value);
    this.loadList();
  }

  onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }

  onSearchSubmit(): void {
    // Search is applied client-side reactively via filteredRecords(); submit is a no-op safeguard for Enter key.
  }

  clearSearch(): void {
    this.searchQuery.set('');
  }

  openRecord(id: string): void {
    this.formNotice.set(null);
    void this.router.navigate(['/app/procurement/purchase-requests', id]);
  }

  backToList(): void {
    this.formNotice.set(null);
    void this.router.navigate(['/app/procurement/purchase-requests']);
  }

  startCreate(): void {
    if (!this.canMutate()) return;
    this.formNotice.set(null);
    void this.router.navigate(['/app/procurement/purchase-requests', 'new']);
  }

  private prepareCreate(): void {
    this.mode.set('create');
    this.selectedRecord.set(null);
    this.draft = this.emptyDraft();
    this.detailLoading.set(false);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.formError.set(false);
    this.fieldErrors.set(new Set());
    this.applyDefaultOrganizationScope();
  }

  private async loadDetailById(id: string, targetMode: 'view' | 'edit'): Promise<void> {
    this.mode.set(targetMode);
    this.detailTab.set('summary');
    this.detailLoading.set(true);
    this.detailError.set(null);
    this.mutationError.set(null);
    this.historyEntries.set([]);
    this.historyLoaded.set(false);
    this.auditEntries.set([]);
    this.auditLoaded.set(false);
    try {
      const record = await firstValueFrom(this.purchaseRequests.get(id));
      this.selectedRecord.set(record);
      this.draft = this.toDraft(record);
      if (targetMode === 'edit' && !record.canEdit) {
        await this.router.navigate(['/app/procurement/purchase-requests', id]);
      }
    } catch (error: unknown) {
      this.detailError.set(toSafeUiError(error));
    } finally {
      this.detailLoading.set(false);
    }
  }

  async reloadDetail(): Promise<void> {
    const id = this.selectedRecord()?.id ?? this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.mutationError.set(null);
    await this.loadDetailById(id, this.mode() === 'edit' ? 'edit' : 'view');
  }

  reloadAndCloseLifecycle(): void {
    this.lifecycleAction.set(null);
    void this.reloadDetail();
  }

  startEdit(): void {
    const record = this.selectedRecord();
    if (!record || !record.canEdit) return;
    this.formNotice.set(null);
    void this.router.navigate(['/app/procurement/purchase-requests', record.id, 'edit']);
  }

  cancelEdit(): void {
    if (this.mode() === 'create') {
      this.backToList();
      return;
    }
    const record = this.selectedRecord();
    if (record) {
      void this.router.navigate(['/app/procurement/purchase-requests', record.id]);
    } else {
      this.backToList();
    }
  }

  setDraftField<K extends keyof RequestDraft>(field: K, value: RequestDraft[K]): void {
    this.draft = { ...this.draft, [field]: value };
  }

  addLine(): void {
    this.draft = { ...this.draft, lines: [...this.draft.lines, this.emptyLine()] };
  }

  removeLine(index: number): void {
    if (this.draft.lines.length <= 1) return;
    this.draft = { ...this.draft, lines: this.draft.lines.filter((_, i) => i !== index) };
  }

  setLineField<K extends keyof LineDraft>(index: number, field: K, value: LineDraft[K]): void {
    const lines = this.draft.lines.map((line, i) => (i === index ? { ...line, [field]: value } : line));
    this.draft = { ...this.draft, lines };
  }

  invalid(field: string): boolean {
    return this.fieldErrors().has(field) || this.fieldErrors().has(`${field}-format`);
  }

  lineInvalid(index: number, field: string): boolean {
    return this.fieldErrors().has(`line-${index}-${field}`);
  }

  async save(): Promise<void> {
    this.formNotice.set(null);
    this.mutationError.set(null);
    if (!this.validateDraft()) {
      this.formError.set(true);
      return;
    }
    const mode = this.mode();
    const existing = mode === 'edit' ? this.selectedRecord() : null;
    this.saving.set(true);
    try {
      const payload = this.toPayload();
      const saved = mode === 'create'
        ? await this.purchaseRequests.create(payload)
        : existing
          ? await this.purchaseRequests.edit(existing.id, payload, existing.version)
          : null;
      if (!saved) return;
      this.formError.set(false);
      this.fieldErrors.set(new Set());
      this.formNotice.set(mode === 'create' ? this.language.text('recordCreated') : this.language.text('recordSaved'));
      await this.router.navigate(['/app/procurement/purchase-requests', saved.id]);
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  openLifecycle(action: LifecycleActionKind): void {
    const record = this.selectedRecord();
    if (!record) return;
    const allowed = action === 'submit' ? record.canSubmit
      : action === 'approve' ? record.canApprove
        : action === 'reject' ? record.canReject
          : action === 'return' ? record.canReturnForChange
            : record.canCancel;
    if (!allowed) return;
    this.lastFocusedElement = document.activeElement as HTMLElement | null;
    this.lifecycleReason.set('');
    this.lifecycleReasonError.set(false);
    this.mutationError.set(null);
    this.lifecycleAction.set(action);
    setTimeout(() => document.getElementById('lifecycle-dialog-cancel')?.focus(), 0);
  }

  closeLifecycle(): void {
    if (this.lifecycleSaving()) return;
    this.lifecycleAction.set(null);
    this.restoreFocusToOpener();
  }

  onLifecycleDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.trapTabKey(event, event.currentTarget as HTMLElement);
      return;
    }
    if (event.key === 'Escape') {
      if (this.lifecycleSaving()) return;
      event.preventDefault();
      this.closeLifecycle();
    }
  }

  async confirmLifecycle(): Promise<void> {
    const record = this.selectedRecord();
    const action = this.lifecycleAction();
    if (!record || !action) return;
    const reasonRequired = action === 'reject' || action === 'return';
    const reason = this.lifecycleReason().trim();
    if (reasonRequired && !reason) {
      this.lifecycleReasonError.set(true);
      return;
    }
    this.lifecycleReasonError.set(false);
    this.lifecycleSaving.set(true);
    this.mutationError.set(null);
    try {
      let updated: PurchaseRequestResponse;
      switch (action) {
        case 'submit':
          updated = await this.purchaseRequests.submit(record.id, record.version);
          break;
        case 'approve':
          updated = await this.purchaseRequests.approve(record.id, record.version);
          break;
        case 'reject':
          updated = await this.purchaseRequests.reject(record.id, record.version, reason);
          break;
        case 'return':
          updated = await this.purchaseRequests.returnForChange(record.id, record.version, reason);
          break;
        case 'cancel':
          updated = await this.purchaseRequests.cancel(record.id, record.version, reason || undefined);
          break;
      }
      this.selectedRecord.set(updated);
      this.draft = this.toDraft(updated);
      this.formNotice.set(this.language.text('recordSaved'));
      this.lifecycleAction.set(null);
      this.restoreFocusToOpener();
      this.historyLoaded.set(false);
      this.auditLoaded.set(false);
      if (this.detailTab() === 'history') void this.loadHistory();
      if (this.detailTab() === 'audit') void this.loadAudit();
    } catch (error: unknown) {
      this.mutationError.set(toSafeUiError(error));
    } finally {
      this.lifecycleSaving.set(false);
    }
  }

  lifecycleTitle(action: LifecycleActionKind): string {
    switch (action) {
      case 'submit': return this.language.text('prSubmitConfirmTitle');
      case 'approve': return this.language.text('prApproveConfirmTitle');
      case 'reject': return this.language.text('prRejectConfirmTitle');
      case 'return': return this.language.text('prReturnConfirmTitle');
      case 'cancel': return this.language.text('prCancelConfirmTitle');
    }
  }

  lifecycleLead(action: LifecycleActionKind): string {
    switch (action) {
      case 'submit': return this.language.text('prSubmitConfirmLead');
      case 'approve': return this.language.text('prApproveConfirmLead');
      case 'reject': return this.language.text('prRejectConfirmLead');
      case 'return': return this.language.text('prReturnConfirmLead');
      case 'cancel': return this.language.text('prCancelConfirmLead');
    }
  }

  lifecycleConfirmLabel(action: LifecycleActionKind): string {
    switch (action) {
      case 'submit': return this.language.text('submitForApproval');
      case 'approve': return this.language.text('approveRequest');
      case 'reject': return this.language.text('rejectRequest');
      case 'return': return this.language.text('returnForChange');
      case 'cancel': return this.language.text('cancelRequest');
    }
  }

  setTab(tab: DetailTab, force = false): void {
    this.detailTab.set(tab);
    if (tab === 'history' && (force || !this.historyLoaded())) void this.loadHistory();
    if (tab === 'audit' && (force || !this.auditLoaded())) void this.loadAudit();
  }

  onTabsKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key !== 'ArrowLeft' && key !== 'ArrowRight' && key !== 'Home' && key !== 'End') return;
    event.preventDefault();
    const tabs = this.detailTabOrder;
    const currentIdx = tabs.indexOf(this.detailTab());
    const isRtl = this.language.language() === 'ar';
    let nextIdx: number;
    if (key === 'Home') nextIdx = 0;
    else if (key === 'End') nextIdx = tabs.length - 1;
    else {
      const forward = key === 'ArrowRight';
      const movesNext = isRtl ? !forward : forward;
      nextIdx = movesNext ? (currentIdx + 1) % tabs.length : (currentIdx - 1 + tabs.length) % tabs.length;
    }
    const nextTab = tabs[nextIdx];
    this.setTab(nextTab);
    setTimeout(() => document.getElementById('pr-tab-' + nextTab)?.focus(), 0);
  }

  private async loadHistory(): Promise<void> {
    const record = this.selectedRecord();
    if (!record) return;
    this.historyLoading.set(true);
    this.historyError.set(null);
    try {
      this.historyEntries.set(await firstValueFrom(this.purchaseRequests.history(record.id)));
      this.historyLoaded.set(true);
    } catch (error: unknown) {
      this.historyError.set(toSafeUiError(error));
    } finally {
      this.historyLoading.set(false);
    }
  }

  private async loadAudit(): Promise<void> {
    const record = this.selectedRecord();
    if (!record) return;
    this.auditLoading.set(true);
    this.auditError.set(null);
    try {
      this.auditEntries.set(await firstValueFrom(this.purchaseRequests.audit(record.id)));
      this.auditLoaded.set(true);
    } catch (error: unknown) {
      this.auditError.set(toSafeUiError(error));
    } finally {
      this.auditLoading.set(false);
    }
  }

  canMutate(): boolean {
    return this.auth.status() === 'authenticated' && this.auth.session()?.selectedContextId !== null && this.auth.session()?.selectedContextId !== undefined;
  }

  statusIcon(status: PurchaseRequestStatus): string {
    switch (status) {
      case 'Draft': return '✎';
      case 'PendingApproval': return '⏳';
      case 'Approved': return '✓';
      case 'Rejected': return '✕';
      case 'ReturnedForChange': return '↺';
      case 'Cancelled': return '⊘';
      default: return '•';
    }
  }

  statusLabel(status: PurchaseRequestStatus): string {
    switch (status) {
      case 'Draft': return this.language.text('prStatusDraft');
      case 'PendingApproval': return this.language.text('prStatusPendingApproval');
      case 'Approved': return this.language.text('prStatusApproved');
      case 'Rejected': return this.language.text('prStatusRejected');
      case 'ReturnedForChange': return this.language.text('prStatusReturnedForChange');
      case 'Cancelled': return this.language.text('prStatusCancelled');
      default: return status;
    }
  }

  statusTone(status: PurchaseRequestStatus): string {
    switch (status) {
      case 'Draft': return 'neutral';
      case 'PendingApproval': return 'progress';
      case 'Approved': return 'success';
      case 'Rejected': return 'danger';
      case 'ReturnedForChange': return 'warning';
      case 'Cancelled': return 'muted';
      default: return 'neutral';
    }
  }

  valueOrEmpty(value: string | null | undefined): string {
    return value && value.trim().length > 0 ? value : this.language.text('emptyValue');
  }

  shortReference(id: string): string {
    const compact = id.replace(/-/g, '').slice(-8).toUpperCase();
    return `#${compact}`;
  }

  companyLabel(companyId: string): string {
    const option = this.organizationScopes().find((candidate) => candidate.companyId === companyId);
    return option?.companyDisplayName ?? this.language.text('organizationScopeUnresolved');
  }

  branchLabel(branchId: string | null): string {
    if (!branchId) return this.language.text('emptyValue');
    const option = this.organizationScopes().find((candidate) => candidate.branchId === branchId);
    return option?.branchDisplayName ?? this.language.text('organizationScopeUnresolved');
  }

  organizationScopeKey(option: PurchaseRequestOrganizationScopeResponse): string {
    return `${option.companyId}|${option.branchId ?? ''}`;
  }

  selectedOrganizationScopeKey(): string {
    return this.draft.companyId ? `${this.draft.companyId}|${this.draft.branchId ?? ''}` : '';
  }

  onOrganizationScopeChange(key: string): void {
    if (!key) {
      this.draft = { ...this.draft, companyId: '', branchId: null };
      return;
    }
    const [companyId, branchId] = key.split('|');
    this.draft = { ...this.draft, companyId, branchId: branchId || null };
  }

  isOrganizationScopeUnavailable(): boolean {
    return this.mode() === 'create' && this.organizationScopesLoaded() && !this.organizationScopesLoading() && this.organizationScopes().length === 0;
  }

  productOptionLabel(record: ProductRecord): string {
    return `${record.sku} · ${record.englishName ?? record.arabicName ?? record.sku}`;
  }

  unitOptionLabel(record: UnitOfMeasureRecord): string {
    return `${record.code} · ${record.englishName ?? record.arabicName ?? record.code}`;
  }

  errorMessage(error: SafeUiError | null): string {
    if (!error) return this.language.text('requestError');
    switch (error.code) {
      case 'authentication_failed':
      case 'access_denied':
      case 'permission_denied':
      case 'antiforgery_failed':
      case 'authorization_profile_denied':
      case 'tenant_context_failed':
        return this.language.text('accessUnavailable');
      case 'resource_scope_denied':
      case 'cross_tenant_target_denied':
      case 'organization_scope_denied':
        return this.language.text('scopeDeniedError');
      case 'requester_only':
        return this.language.text('requesterOnlyError');
      case 'self_approval_denied':
        return this.language.text('selfApprovalDeniedError');
      case 'concurrency_conflict':
      case 'context_version_conflict':
        return this.language.text('conflictTitle');
      case 'validation_failed':
      case 'idempotency_key_invalid':
        return this.language.text('validationSummary');
      case 'network_error':
        return this.language.text('networkError');
      case 'persistence_unavailable':
      case 'reference_persistence_unavailable':
        return this.language.text('persistenceUnavailableError');
      case 'authorization_operation_unmapped':
        return this.language.text('requestError');
      case 'approval_policy_not_configured':
        return this.language.text('approvalPolicyNotConfiguredError');
      case 'purchase_request_not_found':
        return this.language.text('purchaseRequestNotFoundError');
      case 'purchase_request_duplicate':
        return this.language.text('purchaseRequestDuplicateError');
      case 'idempotency_conflict':
        return this.language.text('idempotencyConflictError');
      case 'edit_not_allowed':
        return this.language.text('editNotAllowedError');
      case 'submit_not_allowed':
        return this.language.text('submitNotAllowedError');
      case 'decision_not_allowed':
        return this.language.text('decisionNotAllowedError');
      case 'approval_not_eligible':
        return this.language.text('approvalNotEligibleError');
      case 'approval_duplicate':
        return this.language.text('approvalDuplicateError');
      case 'cancel_not_allowed':
        return this.language.text('cancelNotAllowedError');
      default:
        return this.language.text('requestError');
    }
  }

  private validateDraft(): boolean {
    const errors = new Set<string>();
    if (!this.draft.companyId.trim()) {
      errors.add('organizationScope');
    }
    if (this.draft.lines.length === 0) errors.add('lines');
    this.draft.lines.forEach((line, index) => {
      if (!line.productId) errors.add(`line-${index}-productId`);
      if (!line.unitOfMeasureId) errors.add(`line-${index}-unitOfMeasureId`);
      if (!Number.isFinite(Number(line.quantity)) || Number(line.quantity) <= 0) errors.add(`line-${index}-quantity`);
      if (!line.needByDate) errors.add(`line-${index}-needByDate`);
      if (!line.purpose.trim()) errors.add(`line-${index}-purpose`);
    });
    this.fieldErrors.set(errors);
    return errors.size === 0;
  }

  private toPayload(): PurchaseRequestWriteRequest {
    return {
      companyId: this.draft.companyId,
      branchId: this.draft.branchId,
      purpose: this.draft.purpose.trim() || null,
      lines: this.draft.lines.map((line) => ({
        productId: line.productId,
        unitOfMeasureId: line.unitOfMeasureId,
        quantity: Number(line.quantity),
        needByDate: line.needByDate,
        purpose: line.purpose.trim(),
      })),
    };
  }

  private toDraft(record: PurchaseRequestResponse): RequestDraft {
    return {
      companyId: record.companyId,
      branchId: record.branchId ?? null,
      purpose: record.purpose ?? '',
      lines: record.lines.length > 0
        ? record.lines.map((line) => ({
            productId: line.productId,
            unitOfMeasureId: line.unitOfMeasureId,
            quantity: line.quantity,
            needByDate: line.needByDate,
            purpose: line.purpose ?? '',
          }))
        : [this.emptyLine()],
    };
  }

  private emptyLine(): LineDraft {
    return { productId: '', unitOfMeasureId: '', quantity: 1, needByDate: new Date().toISOString().slice(0, 10), purpose: '' };
  }

  private emptyDraft(): RequestDraft {
    return { companyId: '', branchId: null, purpose: '', lines: [this.emptyLine()] };
  }

  private isProduct(record: MasterDataRecord): record is ProductRecord {
    return 'sku' in record;
  }

  private isUnit(record: MasterDataRecord): record is UnitOfMeasureRecord {
    return !('parentCategoryId' in record) && !('sku' in record) && !('contacts' in record) && !('revision' in record) && !('versions' in record);
  }

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
    } else if (active === last || !active || !panel.contains(active)) {
      event.preventDefault();
      first.focus();
    }
  }

  private getFocusableElements(panel: HTMLElement): HTMLElement[] {
    const selector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(panel.querySelectorAll<HTMLElement>(selector));
  }

  private restoreFocusToOpener(): void {
    const el = this.lastFocusedElement;
    this.lastFocusedElement = null;
    if (el && document.contains(el)) {
      setTimeout(() => el.focus(), 0);
    }
  }
}
