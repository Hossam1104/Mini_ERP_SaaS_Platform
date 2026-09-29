import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LanguageService } from '../../core/i18n/language.service';
import { InventoryService } from './inventory.service';
import { InventoryWarehouseOption } from './inventory.model';
import {
  InventoryFinanceValuationHandoff,
  InventoryValuationEvent,
  InventoryValuationPolicy,
  InventoryValuationReconciliation,
  InventoryValuationSummary,
} from './inventory-valuation.model';
import { InventoryValuationFilters, InventoryValuationService } from './inventory-valuation.service';

type ValuationTab = 'summary' | 'history' | 'pending' | 'reconciliation' | 'handoff';
type Bilingual = { en: string; ar: string };

const copy = {
  kicker: { en: 'Inventory control / valuation', ar: '\u0631\u0642\u0627\u0628\u0629 \u0627\u0644\u0645\u062e\u0632\u0648\u0646 / \u0627\u0644\u062a\u0642\u064a\u064a\u0645' },
  title: { en: 'Moving Weighted Average', ar: '\u0627\u0644\u0645\u062a\u0648\u0633\u0637 \u0627\u0644\u0645\u062a\u062d\u0631\u0643 \u0627\u0644\u0645\u0631\u062c\u062d' },
  lead: { en: 'An explainable value trail for every physical movement. Sequence is durable; source evidence remains immutable.', ar: '\u0645\u0633\u0627\u0631 \u0642\u064a\u0645\u0629 \u0648\u0627\u0636\u062d \u0644\u0643\u0644 \u062d\u0631\u0643\u0629 \u0641\u0639\u0644\u064a\u0629. \u0627\u0644\u062a\u0631\u062a\u064a\u0628 \u062f\u0627\u0626\u0645 \u0648\u0623\u062f\u0644\u0629 \u0627\u0644\u0645\u0635\u062f\u0631 \u063a\u064a\u0631 \u0642\u0627\u0628\u0644\u0629 \u0644\u0644\u062a\u063a\u064a\u064a\u0631.' },
  stockLedger: { en: 'Stock ledger', ar: '\u0633\u062c\u0644 \u0627\u0644\u0645\u062e\u0632\u0648\u0646' },
  process: { en: 'Process valuation', ar: '\u0645\u0639\u0627\u0644\u062c\u0629 \u0627\u0644\u062a\u0642\u064a\u064a\u0645' },
  authorizedScope: { en: 'Server-authorized scope', ar: '\u0627\u0644\u0646\u0637\u0627\u0642 \u0627\u0644\u0645\u0635\u0631\u062d \u0628\u0647 \u0645\u0646 \u0627\u0644\u062e\u0627\u062f\u0645' },
  scopeLead: { en: 'Tenant · Company · Warehouse', ar: '\u0627\u0644\u0645\u0624\u0633\u0633\u0629 · \u0627\u0644\u0634\u0631\u0643\u0629 · \u0627\u0644\u0645\u0633\u062a\u0648\u062f\u0639' },
  chooseWarehouse: { en: 'Choose a warehouse', ar: '\u0627\u062e\u062a\u0631 \u0645\u0633\u062a\u0648\u062f\u0639\u0627\u064b' },
  noScope: { en: 'No server-authorized warehouse context is available.', ar: '\u0644\u0627 \u064a\u0648\u062c\u062f \u0646\u0637\u0627\u0642 \u0645\u0633\u062a\u0648\u062f\u0639 \u0645\u0635\u0631\u062d \u0628\u0647 \u0645\u0646 \u0627\u0644\u062e\u0627\u062f\u0645.' },
  noScopeLead: { en: 'Select an approved Company or Branch context in the header, then return to this surface.', ar: '\u0627\u062e\u062a\u0631 \u0633\u064a\u0627\u0642 \u0634\u0631\u0643\u0629 \u0623\u0648 \u0641\u0631\u0639 \u0645\u0639\u062a\u0645\u062f\u0627\u064b \u0645\u0646 \u0627\u0644\u0631\u0623\u0633 \u062b\u0645 \u0627\u0639\u062f \u0625\u0644\u0649 \u0647\u0630\u0647 \u0627\u0644\u0648\u0627\u062c\u0647\u0629.' },
  loading: { en: 'Reading valuation evidence…', ar: '\u062c\u0627\u0631\u064d \u0642\u0631\u0627\u0621\u0629 \u0623\u062f\u0644\u0629 \u0627\u0644\u062a\u0642\u064a\u064a\u0645…' },
  loadFailed: { en: 'Valuation evidence is not available right now.', ar: '\u062a\u0639\u0630\u0631 \u062a\u0648\u0641\u064a\u0631 \u0623\u062f\u0644\u0629 \u0627\u0644\u062a\u0642\u064a\u064a\u0645 \u062d\u0627\u0644\u064a\u0627\u064b.' },
  retry: { en: 'Retry', ar: '\u0625\u0639\u0627\u062f\u0629 \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629' },
  onHand: { en: 'Physical on hand', ar: '\u0627\u0644\u0631\u0635\u064a\u062f \u0627\u0644\u0641\u0639\u0644\u064a' },
  valuedQuantity: { en: 'Valued quantity', ar: '\u0627\u0644\u0643\u0645\u064a\u0629 \u0627\u0644\u0645\u0642\u064a\u0645\u0629' },
  valuedAmount: { en: 'Valued amount', ar: '\u0642\u064a\u0645\u0629 \u0627\u0644\u0645\u062e\u0632\u0648\u0646' },
  averageCost: { en: 'Average unit cost', ar: '\u0645\u062a\u0648\u0633\u0637 \u062a\u0643\u0644\u0641\u0629 \u0627\u0644\u0648\u062d\u062f\u0629' },
  complete: { en: 'Complete', ar: '\u0645\u0643\u062a\u0645\u0644' },
  partial: { en: 'Partial / pending evidence', ar: '\u062c\u0632\u0626\u064a / \u0623\u062f\u0644\u0629 \u0645\u0639\u0644\u0642\u0629' },
  direction: { en: 'Direction', ar: '\u0627\u0644\u0627\u062a\u062c\u0627\u0647' },
  signedAmount: { en: 'Signed base amount', ar: '\u0627\u0644\u0642\u064a\u0645\u0629 \u0627\u0644\u0623\u0633\u0627\u0633\u064a\u0629 \u0627\u0644\u0645\u0648\u0642\u0639\u0629' },
  pending: { en: 'Pending', ar: '\u0645\u0639\u0644\u0642' },
  blocked: { en: 'Blocked', ar: '\u0645\u062d\u062c\u0648\u0628' },
  inTransit: { en: 'In transit', ar: '\u0642\u064a\u062f \u0627\u0644\u0646\u0642\u0644' },
  reconciled: { en: 'Reconciled', ar: '\u0645\u062a\u0637\u0627\u0628\u0642' },
  summary: { en: 'Summary', ar: '\u0627\u0644\u0645\u0644\u062e\u0635' },
  history: { en: 'MWA history', ar: '\u0633\u062c\u0644 \u0627\u0644\u0645\u062a\u0648\u0633\u0637 \u0627\u0644\u0645\u062a\u062d\u0631\u0643' },
  pendingTab: { en: 'Pending / blocked', ar: '\u0645\u0639\u0644\u0642 / \u0645\u062d\u062c\u0648\u0628' },
  reconciliation: { en: 'Reconciliation', ar: '\u0627\u0644\u062a\u0633\u0648\u064a\u0629' },
  handoff: { en: 'Finance handoff', ar: '\u062a\u0633\u0644\u064a\u0645 \u0644\u0644\u0645\u0627\u0644\u064a\u0629' },
  sequence: { en: 'Ledger sequence', ar: '\u062a\u0631\u062a\u064a\u0628 \u0627\u0644\u0633\u062c\u0644' },
  status: { en: 'Status', ar: '\u0627\u0644\u062d\u0627\u0644\u0629' },
  source: { en: 'Source / lineage', ar: '\u0627\u0644\u0645\u0635\u062f\u0631 / \u0627\u0644\u062a\u062a\u0628\u0639' },
  quantity: { en: 'Quantity', ar: '\u0627\u0644\u0643\u0645\u064a\u0629' },
  unitCost: { en: 'Base unit cost', ar: '\u062a\u0643\u0644\u0641\u0629 \u0627\u0644\u0648\u062d\u062f\u0629 \u0627\u0644\u0623\u0633\u0627\u0633\u064a\u0629' },
  movementValue: { en: 'Movement value', ar: '\u0642\u064a\u0645\u0629 \u0627\u0644\u062d\u0631\u0643\u0629' },
  resultingValue: { en: 'Resulting value', ar: '\u0627\u0644\u0642\u064a\u0645\u0629 \u0627\u0644\u0646\u0627\u062a\u062c\u0629' },
  effectiveDate: { en: 'Effective date', ar: '\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0633\u0631\u064a\u0627\u0646' },
  reason: { en: 'Reason', ar: '\u0627\u0644\u0633\u0628\u0628' },
  noEvents: { en: 'No valuation events in this scope.', ar: '\u0644\u0627 \u062a\u0648\u062c\u062f \u0623\u062d\u062f\u0627\u062b \u062a\u0642\u064a\u064a\u0645 \u0641\u064a \u0647\u0630\u0627 \u0627\u0644\u0646\u0637\u0627\u0642.' },
  noPending: { en: 'Nothing is waiting for a source, predecessor, or exchange-rate decision.', ar: '\u0644\u0627 \u0634\u064a\u0621 \u0645\u0646\u062a\u0638\u0631 \u0644\u0645\u0635\u062f\u0631 \u0623\u0648 \u0633\u0627\u0628\u0642 \u0623\u0648 \u0633\u0639\u0631 \u0635\u0631\u0641.' },
  noHandoff: { en: 'Applied valuation evidence will appear here as Finance-ready facts. No journal is posted by Inventory.', ar: '\u0633\u062a\u0638\u0647\u0631 \u0623\u062f\u0644\u0629 \u0627\u0644\u062a\u0642\u064a\u064a\u0645 \u0627\u0644\u0645\u0637\u0628\u0642\u0629 \u0647\u0646\u0627 \u0643\u062d\u0642\u0627\u0626\u0642 \u062c\u0627\u0647\u0632\u0629 \u0644\u0644\u0645\u0627\u0644\u064a\u0629. \u0644\u0627 \u064a\u0646\u0634\u0626 \u0627\u0644\u0645\u062e\u0632\u0648\u0646 \u0642\u064a\u0648\u062f\u0627\u064b.' },
  financeReady: { en: 'Ready for Finance', ar: '\u062c\u0627\u0647\u0632 \u0644\u0644\u0645\u0627\u0644\u064a\u0629' },
  policy: { en: 'Active policy', ar: '\u0627\u0644\u0633\u064a\u0627\u0633\u0629 \u0627\u0644\u0646\u0634\u0637\u0629' },
  policyBasis: { en: 'Source-cost basis', ar: '\u0623\u0633\u0627\u0633 \u062a\u0643\u0644\u0641\u0629 \u0627\u0644\u0645\u0635\u062f\u0631' },
  noPolicy: { en: 'No valuation policy is configured for this Company.', ar: '\u0644\u0627 \u062a\u0648\u062c\u062f \u0633\u064a\u0627\u0633\u0629 \u062a\u0642\u064a\u064a\u0645 \u0645\u0639\u062f\u0629 \u0644\u0647\u0630\u0647 \u0627\u0644\u0634\u0631\u0643\u0629.' },
  lastApplied: { en: 'Last applied sequence', ar: '\u0622\u062e\u0631 \u062a\u0631\u062a\u064a\u0628 \u0645\u0637\u0628\u0642' },
  immutable: { en: 'Immutable evidence', ar: '\u062f\u0644\u064a\u0644 \u063a\u064a\u0631 \u0642\u0627\u0628\u0644 \u0644\u0644\u062a\u063a\u064a\u064a\u0631' },
  noJournal: { en: 'Finance handoff facts only · no GL/AP posting', ar: '\u062d\u0642\u0627\u0626\u0642 \u062a\u0633\u0644\u064a\u0645 \u0641\u0642\u0637 · \u0628\u062f\u0648\u0646 \u0642\u064a\u0648\u062f \u0645\u0627\u0644\u064a\u0629' },
  export: { en: 'Export evidence', ar: '\u062a\u0635\u062f\u064a\u0631 \u0627\u0644\u0623\u062f\u0644\u0629' },
  exportFailed: { en: 'The valuation export could not be generated.', ar: '\u062a\u0639\u0630\u0631 \u0625\u0646\u0634\u0627\u0621 \u062a\u0635\u062f\u064a\u0631 \u0627\u0644\u062a\u0642\u064a\u064a\u0645.' },
  asOf: { en: 'As of', ar: '\u0643\u0645\u0627 \u0641\u064a' },
  freshness: { en: 'Fresh at', ar: '\u062d\u062f\u062b \u062a\u062d\u062f\u064a\u062b\u0647 \u0641\u064a' },
} as const;

@Component({
  selector: 'app-inventory-valuation-workspace',
  standalone: true,
  imports: [PageHeaderComponent, DataGridComponent, CommonModule, FormsModule, RouterLink],
  template: `
    <section class="ui-page valuation-page" data-testid="inventory-valuation-workspace">
      <app-page-header class="page-header">
        <div page-header-copy>
          <p class="eyebrow">{{ text('kicker') }}</p>
          <h1>{{ text('title') }}</h1>
          <p class="lede">{{ text('lead') }}</p>
        </div>
          <div page-header-actions>
        <div class="valuation-header__actions">
          <a class="button button--secondary" routerLink="/app/inventory">{{ text('stockLedger') }}</a>
          <button class="button button--secondary" type="button" data-testid="valuation-export" (click)="exportValuation()" [disabled]="loading() || !selectedWarehouse()">{{ text('export') }}</button>
          <button class="button button--primary" type="button" data-testid="valuation-process" (click)="processValuation()" [disabled]="loading() || !selectedWarehouse()">{{ text('process') }}</button>
        </div>
      
          </div>
        </app-page-header>ext('loading') }}</h2></section>
      } @else if (error(); as currentError) {
        <section class="ui-surface state-card state-card--error" role="alert"><strong>{{ text('loadFailed') }}</strong><p>{{ currentError }}</p><button class="button button--secondary" type="button" (click)="load()">{{ text('retry') }}</button></section>
      } @else if (!selectedWarehouse()) {
        <section class="ui-surface state-card" data-testid="valuation-no-scope"><h2>{{ text('noScope') }}</h2><p>{{ text('noScopeLead') }}</p></section>
      } @else {
        <section class="ui-surface valuation-controlbar">
          <label class="scope-select"><span>{{ text('authorizedScope') }}</span><select [ngModel]="selectedWarehouseId()" (ngModelChange)="selectWarehouse($event)" data-testid="valuation-warehouse"><option value="">{{ text('chooseWarehouse') }}</option>@for (warehouse of warehouses(); track warehouse.warehouseId) {<option [value]="warehouse.warehouseId">{{ warehouse.displayName }}</option>}</select></label>
          <div class="policy-readout"><span>{{ text('policy') }}</span>@if (policy(); as activePolicy) {<strong>{{ activePolicy.functionalCurrencyCode }} · {{ activePolicy.scopeMode }}</strong><small>{{ text('policyBasis') }}: {{ activePolicy.goodsReceiptCostBasis }}</small>} @else {<strong>{{ text('noPolicy') }}</strong>}</div>
          <div class="controlbar-meta"><span>{{ text('lastApplied') }}</span><strong>{{ currentSummary()?.latestValuedLedgerSequence ?? 0 }}</strong></div>
        </section>

        <section class="valuation-metrics" data-testid="valuation-summary-metrics">
          <article class="metric-card metric-card--accent"><span>{{ text('onHand') }}</span><strong>{{ formatQuantity(currentSummary()?.physicalOnHandQuantity ?? 0) }}</strong><small>{{ currentSummary()?.functionalCurrencyCode ?? '—' }}</small></article>
          <article class="metric-card"><span>{{ text('valuedQuantity') }}</span><strong>{{ formatQuantity(currentSummary()?.valuedQuantity ?? 0) }}</strong><small>{{ currentSummary()?.isComplete ? text('complete') : text('partial') }}</small></article>
          <article class="metric-card"><span>{{ text('valuedAmount') }}</span><strong>{{ formatAmount(currentSummary()?.valuedAmount ?? 0) }}</strong><small>{{ currentSummary()?.functionalCurrencyCode ?? '—' }}</small></article>
          <article class="metric-card metric-card--warn"><span>{{ text('pending') }}</span><strong>{{ currentSummary()?.pendingMovementCount ?? 0 }}</strong><small>{{ text('pendingTab') }}</small></article>
          <article class="metric-card metric-card--warn"><span>{{ text('blocked') }}</span><strong>{{ currentSummary()?.blockedMovementCount ?? 0 }}</strong><small>{{ text('pendingTab') }}</small></article>
          <article class="metric-card metric-card--ink"><span>{{ text('inTransit') }}</span><strong>{{ formatQuantity(currentSummary()?.inTransitQuantity ?? 0) }}</strong><small>{{ formatAmount(currentSummary()?.inTransitValue ?? 0) }}</small></article>
          <article class="metric-card metric-card--ink"><span>{{ text('asOf') }}</span><strong>{{ formatDate(currentSummary()?.asOf) }}</strong><small>{{ text('freshness') }} {{ formatDate(currentSummary()?.freshAsOf) }}</small></article>
        </section>

        <nav class="valuation-tabs" aria-label="Valuation views" role="tablist">
          <button type="button" role="tab" [attr.aria-selected]="activeTab() === 'summary'" [class.is-active]="activeTab() === 'summary'" (click)="setTab('summary')">{{ text('summary') }}</button>
          <button type="button" role="tab" [attr.aria-selected]="activeTab() === 'history'" [class.is-active]="activeTab() === 'history'" (click)="setTab('history')">{{ text('history') }}</button>
          <button type="button" role="tab" [attr.aria-selected]="activeTab() === 'pending'" [class.is-active]="activeTab() === 'pending'" (click)="setTab('pending')">{{ text('pendingTab') }} <em>{{ pendingEvents().length }}</em></button>
          <button type="button" role="tab" [attr.aria-selected]="activeTab() === 'reconciliation'" [class.is-active]="activeTab() === 'reconciliation'" (click)="setTab('reconciliation')">{{ text('reconciliation') }}</button>
          <button type="button" role="tab" [attr.aria-selected]="activeTab() === 'handoff'" [class.is-active]="activeTab() === 'handoff'" (click)="setTab('handoff')">{{ text('handoff') }}</button>
        </nav>

        @if (activeTab() === 'summary') {
          <section class="valuation-grid">
            <article class="ui-surface valuation-panel valuation-panel--wide">
              <div class="panel-heading"><div><p class="eyebrow">{{ text('summary') }}</p><h2>{{ text('reconciliation') }}</h2></div><span class="status-badge" [class.status-badge--active]="currentSummary()?.isComplete">{{ currentSummary()?.reconciliationStatus ?? text('partial') }}</span></div>
              <div class="recon-line"><span></span><div><strong>{{ formatQuantity(currentSummary()?.valuedQuantity ?? 0) }}</strong><small>{{ text('valuedQuantity') }}</small></div><div><strong>{{ formatAmount(currentSummary()?.valuedAmount ?? 0) }}</strong><small>{{ text('valuedAmount') }}</small></div><div><strong>{{ currentSummary()?.isComplete ? text('complete') : text('partial') }}</strong><small>{{ text('reconciliation') }}</small></div></div>
              <p class="panel-note">{{ currentSummary()?.isComplete ? text('immutable') : text('partial') }}</p>
            </article>
            <article class="ui-surface valuation-panel">
              <div class="panel-heading"><div><p class="eyebrow">{{ text('policy') }}</p><h2>{{ policy()?.functionalCurrencyCode ?? '—' }}</h2></div><span class="status-badge status-badge--active">{{ policy()?.roundingMode ?? '—' }}</span></div>
              @if (policy(); as activePolicy) {<dl class="fact-list"><div><dt>{{ text('policyBasis') }}</dt><dd>{{ activePolicy.goodsReceiptCostBasis }}</dd></div><div><dt>{{ text('scopeLead') }}</dt><dd>{{ activePolicy.scopeMode }}</dd></div><div><dt>{{ text('effectiveDate') }}</dt><dd>{{ activePolicy.effectiveFrom }}</dd></div></dl>} @else {<p class="empty-copy">{{ text('noPolicy') }}</p>}
            </article>
          </section>
        }

        @if (activeTab() === 'history') {
          <section class="ui-surface valuation-panel" data-testid="valuation-history">
            <div class="panel-heading"><div><p class="eyebrow">{{ text('history') }}</p><h2>{{ text('immutable') }}</h2></div><span class="status-badge">{{ historyEvents().length }}</span></div>
            @if (historyEvents().length === 0) {<p class="empty-copy">{{ text('noEvents') }}</p>} @else {<app-data-grid [rows]="historyEvents()" [columns]="historyColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('history')" [countLabel]="gridLabel('count')" [emptyLabel]="text('noEvents')" [filterInputLabel]="gridLabel('filter')" [allValuesLabel]="gridLabel('all')" [clearFiltersLabel]="gridLabel('clear')" [pageSize]="historyEvents().length || 1" [showPager]="false" />}
          </section>
        }

        @if (activeTab() === 'pending') {
          <section class="ui-surface valuation-panel" data-testid="valuation-pending">
            <div class="panel-heading"><div><p class="eyebrow">{{ text('pendingTab') }}</p><h2>{{ text('reason') }}</h2></div><span class="status-badge status-badge--warning">{{ pendingEvents().length }}</span></div>
            @if (pendingEvents().length === 0) {<p class="empty-copy">{{ text('noPending') }}</p>} @else {<app-data-grid [rows]="pendingEvents()" [columns]="pendingColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('pendingTab')" [countLabel]="gridLabel('count')" [emptyLabel]="text('noPending')" [filterInputLabel]="gridLabel('filter')" [allValuesLabel]="gridLabel('all')" [clearFiltersLabel]="gridLabel('clear')" [pageSize]="pendingEvents().length || 1" [showPager]="false" />}
          </section>
        }

        @if (activeTab() === 'reconciliation') {
          <section class="ui-surface valuation-panel" data-testid="valuation-reconciliation">
            <div class="panel-heading"><div><p class="eyebrow">{{ text('reconciliation') }}</p><h2>{{ text('onHand') }} / {{ text('valuedQuantity') }}</h2></div><span class="status-badge" [class.status-badge--active]="currentSummary()?.isComplete">{{ currentSummary()?.reconciliationStatus ?? '—' }}</span></div>
            <app-data-grid [rows]="reconciliation()" [columns]="reconciliationColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('reconciliation')" [countLabel]="gridLabel('count')" [emptyLabel]="text('noEvents')" [filterInputLabel]="gridLabel('filter')" [allValuesLabel]="gridLabel('all')" [clearFiltersLabel]="gridLabel('clear')" [pageSize]="reconciliation().length || 1" [showPager]="false" [rowIdFor]="reconciliationRowId" />
          </section>
        }

        @if (activeTab() === 'handoff') {
          <section class="ui-surface valuation-panel" data-testid="valuation-finance-handoff">
            <div class="panel-heading"><div><p class="eyebrow">{{ text('handoff') }}</p><h2>{{ text('financeReady') }}</h2></div><span class="boundary-chip">{{ text('noJournal') }}</span></div>
            @if (handoffs().length === 0) {<p class="empty-copy">{{ text('noHandoff') }}</p>} @else {<app-data-grid [rows]="handoffs()" [columns]="handoffColumns" [rowActions]="[]" [language]="language.language()" [caption]="text('handoff')" [countLabel]="gridLabel('count')" [emptyLabel]="text('noHandoff')" [filterInputLabel]="gridLabel('filter')" [allValuesLabel]="gridLabel('all')" [clearFiltersLabel]="gridLabel('clear')" [pageSize]="handoffs().length || 1" [showPager]="false" />}
          </section>
        }
      }
    </section>
  `,
  styles: [`.valuation-page{--valuation-ink:#172f32;--valuation-teal:#2f7b72;--valuation-copper:#c17a4a}.valuation-context{display:grid;grid-template-columns:minmax(0,1fr) minmax(14rem,.65fr);gap:1rem;align-items:center;margin:0;padding:1rem 1.2rem;border:1px solid color-mix(in srgb,var(--accent) 18%,var(--line));border-radius:var(--radius-card);color:var(--ink);background:var(--surface-glass);box-shadow:var(--shadow-card);backdrop-filter:blur(12px) saturate(140%)}.valuation-context__copy{display:grid;gap:.3rem}.valuation-context__copy .eyebrow{margin:0;color:var(--accent);font-size:.68rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}.valuation-context__copy strong{font:700 1rem/1.35 var(--font-sans)}.valuation-context__copy span,.valuation-context__scope small{color:var(--ink-muted);font-size:.74rem;line-height:1.45}.valuation-context__scope{display:grid;gap:.32rem;padding-inline-start:1rem;border-inline-start:1px solid var(--line)}.valuation-context__scope>span{color:var(--ink-muted);font-size:.66rem;font-weight:800;letter-spacing:.08em;text-transform:uppercase}.valuation-context__scope strong{color:var(--ink-strong);font-size:.9rem}.valuation-header__actions{display:flex;gap:.65rem;flex-wrap:wrap}.valuation-controlbar{display:grid;grid-template-columns:minmax(12rem,1.2fr) minmax(13rem,1fr) auto;gap:1rem;align-items:end;padding:1rem 1.2rem;border-inline-start:.25rem solid var(--valuation-copper)}.scope-select,.policy-readout,.controlbar-meta{display:grid;gap:.28rem}.scope-select span,.policy-readout>span,.controlbar-meta>span{color:var(--ink-muted);font-size:.66rem;font-weight:800;letter-spacing:.08em;text-transform:uppercase}.scope-select select{width:100%;border:1px solid var(--line-strong);border-radius:.55rem;padding:.62rem;background:var(--surface-raised);color:var(--ink);font:700 .8rem/1.2 var(--font-sans)}.policy-readout strong,.controlbar-meta strong{color:var(--valuation-ink);font-size:.88rem}.policy-readout small{color:var(--ink-muted);font-size:.72rem}.controlbar-meta{text-align:end}.valuation-metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:.65rem;margin:1rem 0}.metric-card{display:grid;min-height:7.2rem;align-content:space-between;padding:1rem;border:1px solid var(--line);border-radius:.85rem;background:var(--surface-raised)}.metric-card span,.metric-card small{color:var(--ink-muted);font-size:.68rem;font-weight:750}.metric-card strong{color:var(--valuation-ink);font:750 1.45rem/1.1 var(--font-display)}.metric-card--accent{border-color:#abd3c9;background:#e9f5f0}.metric-card--warn{border-color:#e8c4a7;background:#fff5ec}.metric-card--ink{color:#eff8f4;background:var(--valuation-ink)}.metric-card--ink strong,.metric-card--ink span,.metric-card--ink small{color:#eff8f4}.valuation-tabs{display:flex;gap:.25rem;overflow:auto;margin:1.1rem 0 .85rem;border-bottom:1px solid var(--line)}.valuation-tabs button{border:0;border-bottom:.18rem solid transparent;padding:.7rem .85rem;color:var(--ink-muted);background:transparent;font:750 .75rem/1 var(--font-sans);white-space:nowrap;cursor:pointer}.valuation-tabs button:hover,.valuation-tabs button.is-active{border-bottom-color:var(--valuation-copper);color:var(--valuation-ink)}.valuation-tabs em{display:inline-grid;place-items:center;min-width:1.2rem;height:1.2rem;margin-inline-start:.2rem;border-radius:99px;color:#fff;background:var(--valuation-teal);font-size:.62rem;font-style:normal}.valuation-grid{display:grid;grid-template-columns:minmax(0,1.4fr) minmax(16rem,.6fr);gap:1rem}.valuation-panel{padding:1.25rem}.valuation-panel--wide{min-height:14rem}.panel-heading{display:flex;justify-content:space-between;gap:1rem;align-items:start;margin-bottom:1rem}.panel-heading h2{margin:.2rem 0;color:var(--valuation-ink);font-size:1.2rem}.recon-line{display:grid;grid-template-columns:.22fr repeat(3,1fr);gap:.75rem;align-items:center}.recon-line>span{width:3.6rem;height:3.6rem;border:.65rem solid #e6f0ed;border-inline-end-color:var(--valuation-copper);border-radius:50%}.recon-line div{display:grid;gap:.25rem;padding-inline-start:.75rem;border-inline-start:1px solid var(--line)}.recon-line strong{color:var(--valuation-ink);font:750 1rem/1.15 var(--font-display)}.recon-line small,.panel-note{color:var(--ink-muted);font-size:.72rem}.panel-note{margin:1.3rem 0 0;padding-top:1rem;border-top:1px solid var(--line);line-height:1.5}.fact-list{display:grid;gap:.75rem;margin:0}.fact-list div{display:flex;justify-content:space-between;gap:1rem;padding-bottom:.7rem;border-bottom:1px solid var(--line)}.fact-list dt{color:var(--ink-muted);font-size:.72rem}.fact-list dd{margin:0;color:var(--valuation-ink);font-size:.75rem;font-weight:750;text-align:end}.ui-grid-shell{overflow:auto}.ui-grid{min-width:48rem}.ui-grid th,.ui-grid td{vertical-align:top}.ui-grid td small{display:block;margin-top:.18rem;color:var(--ink-muted);font-size:.67rem}.sequence-cell{color:var(--valuation-teal);font:800 .78rem/1 var(--font-display)}.status-badge--warning{color:#8a4c20;background:#fff0df}.boundary-chip{display:inline-flex;max-width:15rem;border:1px solid #d8e6e2;border-radius:99px;padding:.35rem .6rem;color:var(--valuation-teal);font-size:.66rem;font-weight:800}.empty-copy{color:var(--ink-muted);font-size:.82rem;line-height:1.55}@media(max-width:1100px){.valuation-metrics{grid-template-columns:repeat(3,1fr)}}@media(max-width:760px){.valuation-context,.valuation-controlbar,.valuation-grid{grid-template-columns:1fr}.valuation-context__scope{padding-inline-start:0;border-inline-start:0;border-block-start:1px solid var(--line);padding-block-start:.75rem}.valuation-controlbar{align-items:start}.controlbar-meta{text-align:start}.valuation-metrics{grid-template-columns:repeat(2,1fr)}.recon-line{grid-template-columns:1fr 1fr}.recon-line>span{display:none}.recon-line div{border-inline-start:0;padding:0}.valuation-tabs button{padding-inline:.55rem}}@media(max-width:480px){.valuation-metrics{grid-template-columns:1fr}.valuation-header__actions{width:100%}.valuation-header__actions .button{flex:1;text-align:center}}`],
})
export class InventoryValuationWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  private readonly inventory = inject(InventoryService);
  private readonly valuation = inject(InventoryValuationService);

  readonly warehouses = signal<InventoryWarehouseOption[]>([]);
  readonly selectedWarehouseId = signal('');
  readonly policies = signal<InventoryValuationPolicy[]>([]);
  readonly summary = signal<InventoryValuationSummary | null>(null);
  readonly reconciliation = signal<InventoryValuationReconciliation[]>([]);
  readonly historyEvents = signal<InventoryValuationEvent[]>([]);
  readonly pendingEvents = signal<InventoryValuationEvent[]>([]);
  readonly handoffs = signal<InventoryFinanceValuationHandoff[]>([]);
  readonly historyColumns: DataGridColumn<InventoryValuationEvent>[] = [
    { key: 'sequence', label: this.text('sequence'), value: row => row.ledgerSequence, display: row => `#${row.ledgerSequence}`, align: 'end', filter: 'number-range' },
    { key: 'status', label: this.text('status'), value: row => row.status, secondaryText: row => row.statusCode, badge: true, filter: 'text' },
    { key: 'source', label: this.text('source'), value: row => row.sourceType, secondaryText: row => row.sourceReference ?? row.sourceDocumentId.substring(0, 8), filter: 'text' },
    { key: 'quantity', label: this.text('quantity'), value: row => row.quantity, display: row => this.formatQuantity(row.quantity), secondaryText: row => row.direction, align: 'end', filter: 'number-range' },
    { key: 'unitCost', label: this.text('unitCost'), value: row => row.baseUnitCost, display: row => row.baseUnitCost === null ? '\u2014' : this.formatAmount(row.baseUnitCost), align: 'end', filter: 'number-range' },
    { key: 'movement', label: this.text('movementValue'), value: row => row.movementValue, display: row => row.movementValue === null ? '\u2014' : this.formatAmount(row.movementValue), align: 'end', filter: 'number-range' },
    { key: 'result', label: this.text('resultingValue'), value: row => row.newValue, display: row => this.formatAmount(row.newValue), align: 'end', filter: 'number-range' },
    { key: 'date', label: this.text('effectiveDate'), value: row => row.effectiveOn, filter: 'date-range' },
  ];
  readonly pendingColumns: DataGridColumn<InventoryValuationEvent>[] = [
    { key: 'sequence', label: this.text('sequence'), value: row => row.ledgerSequence, display: row => `#${row.ledgerSequence}`, align: 'end', filter: 'number-range' },
    { key: 'status', label: this.text('status'), value: row => row.status, badge: true, filter: 'text' },
    { key: 'source', label: this.text('source'), value: row => row.sourceType, secondaryText: row => row.sourceReference ?? row.sourceDocumentId.substring(0, 8), filter: 'text' },
    { key: 'reason', label: this.text('reason'), value: row => row.pendingReason ?? row.statusCode, filter: 'text' },
    { key: 'date', label: this.text('effectiveDate'), value: row => row.effectiveOn, filter: 'date-range' },
  ];
  readonly reconciliationColumns: DataGridColumn<InventoryValuationReconciliation>[] = [
    { key: 'status', label: this.text('status'), value: row => row.status, badge: true, filter: 'text' },
    { key: 'onHand', label: this.text('onHand'), value: row => row.physicalOnHandQuantity, display: row => this.formatQuantity(row.physicalOnHandQuantity), align: 'end', filter: 'number-range' },
    { key: 'quantity', label: this.text('valuedQuantity'), value: row => row.valuedQuantity, display: row => this.formatQuantity(row.valuedQuantity), align: 'end', filter: 'number-range' },
    { key: 'amount', label: this.text('valuedAmount'), value: row => row.valuedAmount, display: row => `${this.formatAmount(row.valuedAmount)} ${row.functionalCurrencyCode}`, align: 'end', filter: 'number-range' },
    { key: 'transit', label: this.text('inTransit'), value: row => row.inTransitQuantity, display: row => this.formatQuantity(row.inTransitQuantity), secondaryText: row => this.formatAmount(row.inTransitValue), align: 'end', filter: 'number-range' },
    { key: 'handoff', label: this.text('handoff'), value: row => row.financeHandoffStatus, filter: 'text' },
    { key: 'lastApplied', label: this.text('lastApplied'), value: row => row.lastAppliedLedgerSequence, display: row => `#${row.lastAppliedLedgerSequence}`, align: 'end', filter: 'number-range' },
  ];
  readonly handoffColumns: DataGridColumn<InventoryFinanceValuationHandoff>[] = [
    { key: 'sequence', label: this.text('sequence'), value: row => row.ledgerSequence, display: row => `#${row.ledgerSequence}`, align: 'end', filter: 'number-range' },
    { key: 'source', label: this.text('source'), value: row => row.sourceType, secondaryText: row => row.sourceDocumentId.substring(0, 8), filter: 'text' },
    { key: 'direction', label: this.text('direction'), value: row => row.direction, filter: 'text' },
    { key: 'quantity', label: this.text('quantity'), value: row => row.quantity, display: row => this.formatQuantity(row.quantity), align: 'end', filter: 'number-range' },
    { key: 'unitCost', label: this.text('unitCost'), value: row => row.baseUnitCost, display: row => this.formatAmount(row.baseUnitCost), align: 'end', filter: 'number-range' },
    { key: 'amount', label: this.text('signedAmount'), value: row => row.signedBaseAmount, display: row => `${this.formatAmount(row.signedBaseAmount)} ${row.functionalCurrencyCode}`, align: 'end', filter: 'number-range' },
    { key: 'status', label: this.text('status'), value: row => row.status, badge: true, filter: 'text' },
    { key: 'contract', label: 'Contract', value: row => row.contractVersion, filter: 'text' },
  ];
  readonly reconciliationRowId = (row: InventoryValuationReconciliation) => `${row.warehouseId}:${row.productId}:${row.unitOfMeasureId}`;
  readonly loading = signal(false);
  readonly error = signal('');
  readonly activeTab = signal<ValuationTab>('summary');
  readonly selectedWarehouse = computed(() => this.warehouses().find(item => item.warehouseId === this.selectedWarehouseId()) ?? null);
  readonly currentSummary = computed(() => this.summary());
  readonly policy = computed(() => this.valuation.selectCurrentPolicy(this.policies()));

  ngOnInit(): void { void this.load(); }

  text(key: keyof typeof copy): string { return copy[key][this.language.language()]; }
  gridLabel(key: 'count' | 'filter' | 'all' | 'clear'): string {
    const labels = this.language.language() === 'ar'
      ? { count: '\u0633\u062c\u0644\u0627\u062a', filter: '\u062a\u0635\u0641\u064a\u0629', all: '\u0627\u0644\u0643\u0644', clear: '\u0645\u0633\u062d \u0639\u0648\u0627\u0645\u0644 \u0627\u0644\u062a\u0635\u0641\u064a\u0629' }
      : { count: 'records', filter: 'Filter', all: 'All', clear: 'Clear filters' };
    return labels[key];
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const warehouses = await firstValueFrom(this.inventory.warehouses());
      this.warehouses.set(warehouses);
      const selected = this.selectedWarehouseId() && warehouses.some(item => item.warehouseId === this.selectedWarehouseId())
        ? this.selectedWarehouseId()
        : warehouses[0]?.warehouseId ?? '';
      this.selectedWarehouseId.set(selected);
      if (selected) await this.loadReports();
    } catch {
      this.error.set(this.text('loadFailed'));
    } finally {
      this.loading.set(false);
    }
  }

  async selectWarehouse(warehouseId: string): Promise<void> {
    this.selectedWarehouseId.set(warehouseId);
    if (warehouseId) await this.loadReports();
  }

  setTab(tab: ValuationTab): void { this.activeTab.set(tab); }

  async processValuation(): Promise<void> {
    const warehouse = this.selectedWarehouse();
    if (!warehouse) return;
    this.loading.set(true);
    this.error.set('');
    try {
      await this.valuation.process({ companyId: warehouse.companyId, branchId: warehouse.branchId, warehouseId: warehouse.warehouseId });
      await this.loadReports();
    } catch {
      this.error.set(this.text('loadFailed'));
    } finally {
      this.loading.set(false);
    }
  }

  formatQuantity(value: number): string { return new Intl.NumberFormat(this.language.language(), { maximumFractionDigits: 4 }).format(value); }
  formatAmount(value: number): string { return new Intl.NumberFormat(this.language.language(), { minimumFractionDigits: 2, maximumFractionDigits: 8 }).format(value); }
  formatDate(value: string | null | undefined): string { return value ? new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—'; }
  statusClass(status: string): string { return status === 'Applied' || status === 'Reconciled' ? 'status-badge--active' : status === 'Pending' ? 'status-badge--warning' : 'status-badge--danger'; }

  async exportValuation(): Promise<void> {
    const warehouse = this.selectedWarehouse();
    if (!warehouse) return;
    this.error.set('');
    try {
      const filters: InventoryValuationFilters = { companyId: warehouse.companyId, branchId: warehouse.branchId, warehouseId: warehouse.warehouseId };
      const blob = await firstValueFrom(this.valuation.export(filters));
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `inventory-valuation-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set(this.text('exportFailed'));
    }
  }

  private async loadReports(): Promise<void> {
    const warehouse = this.selectedWarehouse();
    if (!warehouse) return;
    const filters: InventoryValuationFilters = { companyId: warehouse.companyId, branchId: warehouse.branchId, warehouseId: warehouse.warehouseId };
    const [policies, summary, reconciliation, history, handoffs] = await Promise.all([
      firstValueFrom(this.valuation.policies(warehouse.companyId)),
      firstValueFrom(this.valuation.summary(filters)),
      firstValueFrom(this.valuation.reconciliation(filters)),
      firstValueFrom(this.valuation.history(filters)),
      firstValueFrom(this.valuation.financeHandoffs(filters)),
    ]);
    this.policies.set(policies);
    this.summary.set(summary);
    this.reconciliation.set(reconciliation);
    this.historyEvents.set(history);
    this.pendingEvents.set(history.filter(event => event.status === 'Pending' || event.status === 'Blocked'));
    this.handoffs.set(handoffs);
  }
}
