import { Component, EventEmitter, HostListener, Input, Output, computed, effect, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

export type GridFilterType = 'text' | 'select' | 'number-range' | 'date-range';
export type GridFilterValue = string | { from: string; to: string };

export interface DataGridColumn<T extends object> {
  key: string;
  label: string;
  value: (row: T) => string | number | null;
  display?: (row: T) => string;
  link?: (row: T) => string;
  secondaryText?: (row: T) => string;
  filter?: GridFilterType;
  filterOptions?: readonly { value: string; label: string }[];
  badge?: boolean;
  currencySymbol?: (row: T) => { url: string | null; text: string };
  width?: number;
  align?: 'start' | 'end';
}

export interface DataGridAction<T extends object> { action: string; row: T; }

@Component({
  selector: 'app-data-grid',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="data-grid-card" [attr.aria-label]="caption">
      <div class="data-grid-toolbar">
        <span class="data-grid-total"><span class="data-grid-total__dot" aria-hidden="true"></span><strong>{{ filteredRows().length }}</strong> {{ countLabel }}</span>
        <span class="data-grid-summary" aria-live="polite">{{ summaryLabel() }}</span>
      </div>
      @if (loading) {
        <div class="data-grid-state data-grid-state--loading" role="status" aria-live="polite"><span class="grid-spinner" aria-hidden="true"></span>{{ loadingLabel }}</div>
      } @else if (rows().length === 0) {
        <div class="data-grid-state" role="status"><svg class="icon grid-empty-icon" aria-hidden="true"><use href="#icon-receipt" /></svg><strong>{{ emptyLabel }}</strong><span>{{ emptyHint }}</span></div>
      } @else if (filteredRows().length === 0) {
        <div class="data-grid-state" role="status"><svg class="icon grid-empty-icon" aria-hidden="true"><use href="#icon-search" /></svg><strong>{{ noMatchesLabel }}</strong><button class="grid-clear-all" type="button" (click)="clearAllFilters()">{{ clearFiltersLabel }}</button></div>
      } @else {
        <div class="data-grid-scroll" tabindex="0" [attr.aria-label]="caption + ' table'">
          <table class="data-grid-table">
            <caption class="sr-only">{{ caption }}</caption>
            <colgroup>
              <col class="selection-column" />
              @for (column of columns(); track column.key) { <col [style.width.px]="columnWidth(column)" /> }
              <col class="action-column" />
            </colgroup>
            <thead>
              <tr>
                <th class="selection-cell" scope="col"><input type="checkbox" [checked]="allVisibleSelected()" [indeterminate]="someVisibleSelected() && !allVisibleSelected()" [attr.aria-label]="selectAllLabel" (change)="toggleVisibleSelection($event)" /></th>
                @for (column of columns(); track column.key) {
                  <th scope="col" [attr.aria-sort]="ariaSort(column)" [class.numeric]="column.align === 'end'">
                    <div class="grid-heading">
                      <button class="grid-sort" type="button" (click)="sortBy(column)" [class.is-sorted]="sortKey() === column.key" [attr.aria-label]="sortLabel(column)">{{ column.label }}<svg class="icon sort-icon" [class.is-sorted]="sortKey() === column.key" aria-hidden="true"><use [attr.href]="'#icon-' + sortMark(column)" /></svg></button>
                      @if (column.filter) {
                        <button class="grid-filter-button" type="button" [id]="filterTriggerId(column)" [class.is-filtered]="filterIsActive(column.key)" [attr.aria-label]="filterLabel(column)" aria-haspopup="dialog" [attr.aria-expanded]="filterColumn() === column.key" [attr.aria-controls]="filterColumn() === column.key ? filterPopoverId(column) : null" (click)="toggleFilter(column.key, $event)"><svg class="icon" aria-hidden="true"><use href="#icon-filter" /></svg></button>
                      }
                      <span class="grid-resize-handle" role="separator" aria-orientation="vertical" tabindex="0" [attr.aria-label]="resizeLabel(column)" [attr.aria-valuemin]="112" [attr.aria-valuemax]="520" [attr.aria-valuenow]="columnWidth(column)" (mousedown)="startResize($event, column)" (keydown)="resizeByKeyboard($event, column)"></span>
                    </div>
                  </th>
                }
                <th class="action-cell" scope="col"><span class="sr-only">{{ rowActionsLabel }}</span></th>
              </tr>
            </thead>
            <tbody>
              @for (row of pageRows(); track rowId(row)) {
                <tr [attr.data-row-id]="rowId(row)" [class.is-selected]="isSelected(row)">
                  <td class="selection-cell"><input type="checkbox" [checked]="isSelected(row)" [attr.aria-label]="selectRowLabel(row)" (change)="toggleRowSelection(row, $event)" /></td>
                  @for (column of columns(); track column.key) {
                    <td [class.numeric]="column.align === 'end'">
                      @if (column.badge) {
                        <span [class]="'data-grid-badge ' + badgeClass(column.value(row))">{{ cellText(column, row) }}</span>
                      } @else if (column.currencySymbol) {
                        @let currencySymbol = column.currencySymbol(row);
                        <span class="data-grid-money" [attr.dir]="language === 'ar' ? 'rtl' : 'ltr'">
                          @if (currencySymbol.url) { <img [src]="currencySymbol.url" alt="" /> } @else { <span>{{ currencySymbol.text }}</span> }
                          <span>{{ cellText(column, row) }}</span>
                        </span>
                      } @else if (linkFor(column, row); as link) { <a class="grid-cell-link" [routerLink]="link">{{ cellText(column, row) }}</a> } @else { {{ cellText(column, row) }} }
                      @if (secondaryTextFor(column, row); as detail) { <small class="grid-cell-detail">{{ detail }}</small> }
                    </td>
                  }
                  <td class="action-cell">
                    <button class="grid-row-menu-trigger" type="button" [id]="rowMenuButtonId(row)" [attr.aria-label]="rowActionButtonLabel(row)" aria-haspopup="menu" [attr.aria-expanded]="actionRowId() === rowId(row)" (click)="toggleRowActions(row)"><svg class="icon" aria-hidden="true"><use href="#icon-ellipsis" /></svg></button>
                    @if (actionRowId() === rowId(row)) {
                      <div class="grid-row-menu" role="menu" [attr.aria-label]="rowActionsLabel" (keydown)="onRowMenuKeydown($event)">
                        @for (action of rowActions; track action.key) { <button type="button" role="menuitem" (click)="runRowAction(action.key, row)">{{ action.label }}</button> }
                      </div>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <footer class="data-grid-pager">
          <span class="pager-summary">{{ pagerSummaryLabel() }}</span>
          <div class="pager-controls">
            <button type="button" class="pager-button" (click)="changePage(-1)" [disabled]="currentPage() === 0" [attr.aria-label]="previousPageLabel"><svg class="icon icon--chevron-left" aria-hidden="true"><use href="#icon-chevron-left" /></svg></button>
            <span>{{ currentPage() + 1 }} / {{ pageCount() }}</span>
            <button type="button" class="pager-button" (click)="changePage(1)" [disabled]="currentPage() + 1 >= pageCount()" [attr.aria-label]="nextPageLabel"><svg class="icon icon--chevron-right" aria-hidden="true"><use href="#icon-chevron-right" /></svg></button>
          </div>
          <span class="pager-size">{{ pageSize }} {{ perPageLabel }}</span>
        </footer>
      }
      @if (activeFilterColumn(); as column) {
        <div class="grid-filter-popover" role="dialog" [attr.id]="filterPopoverId(column)" [attr.aria-label]="filterLabel(column)" [style.top.px]="filterPopoverPosition().top" [style.left.px]="filterPopoverPosition().left" (keydown.escape)="closeFilter(column.key, true)">
          @if (column.filter === 'text') {
            <label><span>{{ filterInputLabel }}</span><input type="search" [value]="textFilterValue(column.key)" [attr.aria-label]="filterInputLabel + ' ' + column.label" (input)="setTextFilter(column.key, $any($event.target).value)" /></label>
          } @else if (column.filter === 'select') {
            <label><span>{{ filterInputLabel }}</span><select [value]="textFilterValue(column.key)" [attr.aria-label]="filterInputLabel + ' ' + column.label" (change)="setTextFilter(column.key, $any($event.target).value)"><option value="">{{ allValuesLabel }}</option>@for (option of optionsFor(column); track option.value) { <option [value]="option.value">{{ option.label }}</option> }</select></label>
          } @else {
            <div class="grid-range-fields">
              <label><span>{{ fromLabel }}</span><input [type]="column.filter === 'date-range' ? 'date' : 'number'" [attr.aria-label]="fromLabel + ' ' + column.label" [value]="rangeFilterValue(column.key, 'from')" (input)="setRangeFilter(column.key, 'from', $any($event.target).value)" /></label>
              <label><span>{{ toLabel }}</span><input [type]="column.filter === 'date-range' ? 'date' : 'number'" [attr.aria-label]="toLabel + ' ' + column.label" [value]="rangeFilterValue(column.key, 'to')" (input)="setRangeFilter(column.key, 'to', $any($event.target).value)" /></label>
            </div>
          }
          <button class="grid-filter-clear" type="button" (click)="clearFilter(column.key)">{{ clearFiltersLabel }}</button>
        </div>
      }
    </section>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .data-grid-card { position: relative; min-width: 0; overflow: visible; border: 1px solid color-mix(in srgb, var(--accent) 12%, var(--line)); border-radius: var(--radius-card); background: linear-gradient(145deg, var(--surface-glass), var(--surface-raised)); box-shadow: var(--shadow-card); backdrop-filter: blur(12px) saturate(140%); }
    .data-grid-toolbar { display: flex; min-height: 56px; align-items: center; justify-content: space-between; gap: 1rem; border-block-end: 1px solid var(--line); padding: .65rem 1rem; }
    .data-grid-total { display: inline-flex; align-items: center; gap: .5rem; border: 1px solid color-mix(in srgb, var(--accent) 18%, var(--line)); border-radius: 999px; padding: .4rem .8rem; color: var(--accent-strong); background: var(--accent-soft); font-size: .9rem; white-space: nowrap; }
    .data-grid-total strong { color: var(--ink-strong); font-size: 1rem; }
    .data-grid-total__dot { width: 8px; height: 8px; border-radius: 50%; background: var(--accent); }
    .data-grid-summary, .pager-summary, .pager-size { color: var(--ink-muted); font-size: 14px; }
    .data-grid-scroll { max-width: 100%; max-height: min(66vh, 640px); overflow: auto; overscroll-behavior: contain; }
    .data-grid-table { width: 100%; min-width: max-content; table-layout: fixed; border-collapse: separate; border-spacing: 0; font-size: 14px; }
    .data-grid-table .selection-column { width: 44px; }
    .data-grid-table .action-column { width: 58px; }
    .data-grid-table th, .data-grid-table td { height: 52px; border-block-end: 1px solid var(--line); padding: .55rem .7rem; text-align: start; vertical-align: middle; }
    .data-grid-table th { position: sticky; inset-block-start: 0; z-index: 5; color: var(--ink-muted); background: var(--grid-header); font-size: 14px; font-weight: 800; }
    .data-grid-table td { color: var(--ink); white-space: nowrap; }
    .grid-cell-link { color: var(--accent); font-weight: 750; text-decoration: none; }
    .grid-cell-link:hover { text-decoration: underline; }
    .grid-cell-detail { display: block; color: var(--ink-muted); font-size: 14px; }
    .data-grid-badge { display: inline-flex; min-height: 28px; align-items: center; border: 1px solid var(--line); border-radius: 999px; padding: .2rem .65rem; color: var(--ink-muted); background: var(--surface-tint); font-size: 14px; font-weight: 700; }
    .data-grid-badge--draft, .data-grid-badge--pendingapproval, .data-grid-badge--returnedforchange { border-color: color-mix(in srgb, var(--support) 28%, var(--line)); color: var(--support); background: var(--support-soft); }
    .data-grid-badge--approved, .data-grid-badge--issued { border-color: color-mix(in srgb, var(--success) 30%, var(--line)); color: var(--success); background: color-mix(in srgb, var(--success) 11%, var(--surface-raised)); }
    .data-grid-badge--rejected, .data-grid-badge--cancelled { border-color: color-mix(in srgb, var(--danger) 28%, var(--line)); color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); }
    .data-grid-badge--partiallyconfirmed, .data-grid-badge--changedpendingapproval { border-color: color-mix(in srgb, var(--accent) 30%, var(--line)); color: var(--accent-strong); background: var(--accent-soft); }
    .data-grid-money { display: inline-flex; align-items: center; justify-content: flex-end; gap: .4rem; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .data-grid-money img { width: 20px; height: 20px; object-fit: contain; }
    .data-grid-table .numeric { text-align: end; font-variant-numeric: tabular-nums; }
    .data-grid-table th.numeric .grid-heading { justify-content: flex-end; }
    .data-grid-table th.numeric .grid-sort { justify-content: flex-end; text-align: end; }
    .data-grid-table tbody tr { transition: background-color var(--motion-fast) ease; }
    .data-grid-table tbody tr:nth-child(even) { background: color-mix(in srgb, var(--accent-soft) 18%, var(--surface-raised)); }
    .data-grid-table tbody tr:hover, .data-grid-table tbody tr.is-selected { background: color-mix(in srgb, var(--accent-soft) 52%, var(--surface-raised)); }
    .grid-heading { position: relative; display: flex; min-width: 0; align-items: center; gap: .2rem; padding-inline-end: .7rem; }
    .grid-sort { display: inline-flex; min-width: 0; align-items: center; gap: .3rem; border: 0; padding: .25rem 0; color: inherit; background: transparent; font: inherit; text-align: start; white-space: nowrap; }
    .grid-sort:hover { color: var(--accent); }
    .grid-sort.is-sorted { color: var(--accent); }
    .sort-icon { width: 16px; height: 16px; color: var(--ink-muted); }
    .sort-icon.is-sorted { color: var(--accent); }
    .grid-filter-button { display: inline-grid; width: 40px; height: 40px; place-items: center; flex: none; border: 1px solid transparent; border-radius: 10px; color: var(--ink-muted); background: transparent; font-size: 14px; }
    .grid-filter-button .icon { width: 15px; height: 15px; }
    .grid-filter-button:hover, .grid-filter-button.is-filtered, .grid-filter-button[aria-expanded='true'] { border-color: color-mix(in srgb, var(--accent) 26%, var(--line)); color: var(--accent); background: var(--accent-soft); }
    .grid-filter-popover { position: absolute; z-index: 70; display: grid; width: min(260px, calc(100vw - 24px)); gap: .7rem; border: 1px solid color-mix(in srgb, var(--accent) 20%, var(--line)); border-radius: 14px; padding: .8rem; color: var(--ink); background: var(--surface-glass); box-shadow: var(--shadow-overlay), inset 0 1px 0 var(--glass-highlight); backdrop-filter: blur(18px) saturate(150%); }
    .grid-filter-popover label, .grid-range-fields label { display: grid; min-width: 0; gap: .35rem; color: var(--ink-muted); font-size: 14px; font-weight: 700; }
    .grid-filter-popover input, .grid-filter-popover select { width: 100%; min-width: 0; min-height: 44px; }
    .grid-range-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; }
    .grid-filter-clear { min-height: 40px; justify-self: start; border: 0; padding: .25rem 0; color: var(--accent); background: transparent; font-size: 14px; font-weight: 800; }
    .grid-resize-handle { position: absolute; z-index: 6; inset-block: -1rem; inset-inline-end: -3px; width: 8px; cursor: col-resize; touch-action: none; }
    .grid-resize-handle::after { position: absolute; inset-block: .95rem; inset-inline-start: 3px; width: 2px; border-radius: 2px; background: transparent; content: ''; }
    .grid-heading:hover .grid-resize-handle::after, .grid-resize-handle:focus-visible::after { background: var(--accent); }
    .selection-cell { width: 44px; text-align: center !important; }
    .selection-cell input { display: inline-grid; vertical-align: middle; }
    .action-cell { position: relative; width: 58px; text-align: center !important; }
    .grid-row-menu-trigger { display: inline-grid; width: 40px; height: 40px; place-items: center; border: 1px solid var(--line); border-radius: 12px; color: var(--accent); background: var(--surface-raised); }
    .grid-row-menu-trigger .icon { width: 18px; height: 18px; }
    .grid-row-menu-trigger:hover, .grid-row-menu-trigger[aria-expanded='true'] { border-color: var(--accent); background: var(--accent-soft); }
    .grid-row-menu { position: absolute; z-index: 22; inset-block-start: calc(100% - .2rem); inset-inline-end: .5rem; display: grid; min-width: 138px; gap: .2rem; border: 1px solid var(--line); border-radius: 12px; padding: .35rem; background: var(--surface-glass); box-shadow: var(--shadow-overlay); backdrop-filter: blur(16px); }
    .grid-row-menu button { min-height: 40px; border: 0; border-radius: 8px; padding: .55rem .65rem; color: var(--ink); background: transparent; font-size: 14px; text-align: start; }
    .grid-row-menu button:hover, .grid-row-menu button:focus-visible { color: var(--accent); background: var(--accent-soft); }
    .data-grid-pager { display: flex; min-height: 60px; align-items: center; justify-content: space-between; gap: 1rem; padding: .55rem 1rem; }
    .pager-controls { display: inline-flex; align-items: center; gap: .65rem; color: var(--ink); font-size: 14px; font-weight: 700; }
    .pager-button { display: inline-grid; width: 40px; height: 40px; place-items: center; border: 1px solid var(--line); border-radius: 12px; color: var(--accent); background: var(--surface-raised); }
    .pager-button:hover:not(:disabled) { border-color: var(--accent); background: var(--accent-soft); }
    .pager-button:disabled { color: var(--ink-muted); cursor: not-allowed; opacity: .48; }
    .data-grid-state { display: grid; min-height: 230px; place-content: center; justify-items: center; gap: .6rem; padding: 2rem; color: var(--ink-muted); text-align: center; }
    .data-grid-state strong { color: var(--ink-strong); font-size: 1.05rem; }
    .grid-empty-icon { width: 30px; height: 30px; color: var(--accent); }
    .grid-clear-all { min-height: 40px; border: 1px solid var(--line-strong); border-radius: 999px; padding: .45rem .8rem; color: var(--accent); background: var(--surface-raised); font-size: 14px; font-weight: 700; }
    .grid-spinner { width: 26px; height: 26px; border: 3px solid var(--line); border-inline-start-color: var(--accent); border-radius: 50%; animation: grid-spin .8s linear infinite; }
    @keyframes grid-spin { to { transform: rotate(360deg); } }
    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    @media (max-width: 620px) { .data-grid-table { min-width: 840px; } .data-grid-scroll { max-height: 64vh; border-radius: var(--radius-md); } .data-grid-toolbar { align-items: flex-start; flex-direction: column; gap: .4rem; padding: .7rem; } .data-grid-pager { min-height: 0; flex-wrap: wrap; justify-content: center; padding: .7rem; } .pager-summary { flex-basis: 100%; text-align: center; } .pager-size { display: none; } }
    @media (prefers-reduced-motion: reduce) { .grid-spinner { animation: none; } }
  `,
})
export class DataGridComponent<T extends object> {
  readonly rows = input<readonly T[]>([]);
  readonly columns = input<readonly DataGridColumn<T>[]>([]);
  @Input() caption = 'Data grid';
  @Input() language: 'en' | 'ar' = 'en';
  @Input() rowKey: keyof T & string = 'id' as keyof T & string;
  @Input() rowActions: readonly { key: string; label: string }[] = [{ key: 'view', label: 'View' }];
  @Input() loading = false;
  @Input() pageSize = 7;
  @Input() countLabel = 'records';
  @Input() loadingLabel = 'Loading records';
  @Input() emptyLabel = 'No records';
  @Input() emptyHint = 'There are no records to show.';
  @Input() noMatchesLabel = 'No matching records';
  @Input() clearFiltersLabel = 'Clear filters';
  @Input() filterInputLabel = 'Filter';
  @Input() allValuesLabel = 'All';
  @Input() fromLabel = 'From';
  @Input() toLabel = 'To';
  @Input() selectAllLabel = 'Select visible rows';
  @Input() rowActionsLabel = 'Row actions';
  @Input() resizeLabelPrefix = 'Resize';
  @Input() previousPageLabel = 'Previous page';
  @Input() nextPageLabel = 'Next page';
  @Input() perPageLabel = 'per page';
  @Input() viewActionKey = 'view';
  @Output() rowAction = new EventEmitter<DataGridAction<T>>();
  @Output() selectionChange = new EventEmitter<readonly string[]>();

  readonly sortKey = signal<string | null>(null);
  readonly sortDirection = signal<'asc' | 'desc'>('asc');
  readonly filters = signal<Record<string, GridFilterValue>>({});
  readonly filterColumn = signal<string | null>(null);
  readonly filterPopoverPosition = signal({ top: 0, left: 12 });
  readonly actionRowId = signal<string | null>(null);
  readonly page = signal(0);
  readonly selectedIds = signal(new Set<string>());
  readonly columnWidths = signal<Record<string, number>>({});
  readonly filteredRows = computed(() => {
    let result = this.rows().filter((row) => this.columns().every((column) => this.matchesFilter(row, column)));
    const key = this.sortKey();
    const column = this.columns().find((candidate) => candidate.key === key);
    if (column) {
      const direction = this.sortDirection() === 'asc' ? 1 : -1;
      result = [...result].sort((first, second) => direction * this.compare(column.value(first), column.value(second)));
    }
    return result;
  });
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.filteredRows().length / Math.max(1, this.pageSize))));
  readonly currentPage = computed(() => Math.min(Math.max(0, this.page()), this.pageCount() - 1));
  readonly pageRows = computed(() => this.filteredRows().slice(this.currentPage() * this.pageSize, (this.currentPage() + 1) * this.pageSize));
  private resizing: { key: string; startX: number; startWidth: number } | null = null;

  constructor() {
    effect(() => {
      const page = this.page();
      const clampedPage = this.currentPage();
      if (page !== clampedPage) this.page.set(clampedPage);
    });
  }

  sortBy(column: DataGridColumn<T>): void {
    if (this.sortKey() === column.key) this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    else { this.sortKey.set(column.key); this.sortDirection.set('asc'); }
    this.page.set(0);
  }

  ariaSort(column: DataGridColumn<T>): string | null {
    return this.sortKey() === column.key ? (this.sortDirection() === 'asc' ? 'ascending' : 'descending') : null;
  }

  sortMark(column: DataGridColumn<T>): string {
    return this.sortKey() !== column.key ? 'arrow-up-down' : this.sortDirection() === 'asc' ? 'arrow-up' : 'arrow-down';
  }

  activeFilterColumn(): DataGridColumn<T> | null {
    const key = this.filterColumn();
    return key ? this.columns().find((column) => column.key === key) ?? null : null;
  }

  badgeClass(value: string | number | null): string {
    const tone = String(value ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '');
    return tone ? `data-grid-badge--${tone}` : 'data-grid-badge--neutral';
  }

  sortLabel(column: DataGridColumn<T>): string {
    const current = this.ariaSort(column);
    if (this.language === 'ar') return current ? `فرز ${column.label}؛ الترتيب الحالي ${current === 'ascending' ? 'تصاعدي' : 'تنازلي'}` : `فرز حسب ${column.label}`;
    return current ? `Sort ${column.label}; currently ${current}` : `Sort by ${column.label}`;
  }

  filterLabel(column: DataGridColumn<T>): string { return this.language === 'ar' ? `تصفية ${column.label}` : `Filter ${column.label}`; }
  resizeLabel(column: DataGridColumn<T>): string { return this.language === 'ar' ? `تغيير عرض ${column.label}` : `${this.resizeLabelPrefix} ${column.label}`; }
  filterPopoverId(column: DataGridColumn<T>): string { return `grid-filter-${column.key}`; }
  filterTriggerId(column: DataGridColumn<T>): string { return `filter-trigger-${column.key}`; }
  rowId(row: T): string { return String((row as Record<string, unknown>)[this.rowKey] ?? ''); }
  rowMenuButtonId(row: T): string { return `grid-row-menu-${this.rowId(row)}`; }
  rowActionButtonLabel(row: T): string { return `${this.rowActionsLabel}: ${this.columns()[0] ? this.cellText(this.columns()[0], row) : this.rowId(row)}`; }
  columnWidth(column: DataGridColumn<T>): number { return this.columnWidths()[column.key] ?? column.width ?? 176; }
  cellText(column: DataGridColumn<T>, row: T): string { return column.display?.(row) ?? String(column.value(row) ?? '—'); }
  linkFor(column: DataGridColumn<T>, row: T): string | null { return column.link?.(row) ?? null; }
  secondaryTextFor(column: DataGridColumn<T>, row: T): string | null { return column.secondaryText?.(row) ?? null; }
  textFilterValue(key: string): string { const value = this.filters()[key]; return typeof value === 'string' ? value : ''; }
  rangeFilterValue(key: string, part: 'from' | 'to'): string { const value = this.filters()[key]; return typeof value === 'object' ? value[part] : ''; }
  filterIsActive(key: string): boolean { const value = this.filters()[key]; return typeof value === 'string' ? value.length > 0 : !!value && (!!value.from || !!value.to); }

  optionsFor(column: DataGridColumn<T>): readonly { value: string; label: string }[] {
    if (column.filterOptions) return column.filterOptions;
    return [...new Set(this.rows().map((row) => String(column.value(row) ?? '')))].filter(Boolean).sort().map((value) => ({ value, label: value }));
  }

  toggleFilter(key: string, event?: MouseEvent): void {
    this.actionRowId.set(null);
    if (this.filterColumn() === key) { this.filterColumn.set(null); return; }
    const trigger = event?.currentTarget as HTMLElement | null;
    if (trigger) {
      const rect = trigger.getBoundingClientRect();
      const cardRect = trigger.closest<HTMLElement>('.data-grid-card')?.getBoundingClientRect();
      if (cardRect) {
        const width = Math.min(260, document.documentElement.clientWidth - 24);
        const maxLeft = document.documentElement.clientWidth - width - 12;
        const desiredLeft = document.documentElement.dir === 'rtl' ? rect.right - width : rect.left;
        const left = Math.max(12, Math.min(desiredLeft, maxLeft));
        const below = rect.bottom + 6;
        const top = below + 220 <= window.innerHeight - 12 ? below : Math.max(12, rect.top - 226);
        this.filterPopoverPosition.set({ top: top - cardRect.top, left: left - cardRect.left });
      }
    }
    this.filterColumn.set(key);
    queueMicrotask(() => document.getElementById(this.filterPopoverId(this.columns().find((column) => column.key === key)!))?.querySelector<HTMLElement>('input, select')?.focus({ preventScroll: true }));
  }

  closeFilter(key: string, restoreFocus = false): void {
    if (this.filterColumn() !== key) return;
    this.filterColumn.set(null);
    if (restoreFocus) document.getElementById(`filter-trigger-${key}`)?.focus();
  }

  setTextFilter(key: string, value: string): void {
    this.filters.update((filters) => ({ ...filters, [key]: value }));
    this.page.set(0);
  }

  setRangeFilter(key: string, part: 'from' | 'to', value: string): void {
    const current = this.filters()[key];
    const range = typeof current === 'object' ? current : { from: '', to: '' };
    this.filters.update((filters) => ({ ...filters, [key]: { ...range, [part]: value } }));
    this.page.set(0);
  }

  clearFilter(key: string): void {
    this.filters.update((filters) => { const next = { ...filters }; delete next[key]; return next; });
    this.page.set(0);
    this.closeFilter(key, true);
  }

  clearAllFilters(): void { this.filters.set({}); this.page.set(0); }

  toggleRowActions(row: T): void {
    const id = this.rowId(row);
    this.filterColumn.set(null);
    if (this.actionRowId() === id) { this.actionRowId.set(null); return; }
    this.actionRowId.set(id);
    requestAnimationFrame(() => document.getElementById(this.rowMenuButtonId(row))?.parentElement?.querySelector<HTMLElement>('.grid-row-menu [role="menuitem"]')?.focus());
  }

  closeRowActions(event: KeyboardEvent): void {
    event.preventDefault();
    const menu = event.currentTarget as HTMLElement;
    const trigger = menu.parentElement?.querySelector<HTMLButtonElement>('.grid-row-menu-trigger');
    this.actionRowId.set(null);
    trigger?.focus();
  }

  onRowMenuKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') { this.closeRowActions(event); return; }
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    const menu = event.currentTarget as HTMLElement;
    const items = Array.from(menu.querySelectorAll<HTMLButtonElement>('[role="menuitem"]'));
    const index = items.indexOf(event.target as HTMLButtonElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1
      : (index + (event.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length;
    event.preventDefault();
    items[next]?.focus();
  }

  runRowAction(key: string, row: T): void {
    this.rowAction.emit({ action: key, row });
    this.actionRowId.set(null);
  }

  isSelected(row: T): boolean { return this.selectedIds().has(this.rowId(row)); }
  selectRowLabel(row: T): string { return this.language === 'ar' ? `تحديد الصف ${this.rowId(row)}` : `Select row ${this.rowId(row)}`; }
  allVisibleSelected(): boolean { return this.pageRows().length > 0 && this.pageRows().every((row) => this.isSelected(row)); }
  someVisibleSelected(): boolean { return this.pageRows().some((row) => this.isSelected(row)); }

  toggleRowSelection(row: T, event: Event): void {
    const next = new Set(this.selectedIds());
    const id = this.rowId(row);
    if ((event.target as HTMLInputElement).checked) next.add(id); else next.delete(id);
    this.selectedIds.set(next);
    this.selectionChange.emit([...next]);
  }

  toggleVisibleSelection(event: Event): void {
    const next = new Set(this.selectedIds());
    for (const row of this.pageRows()) {
      if ((event.target as HTMLInputElement).checked) next.add(this.rowId(row)); else next.delete(this.rowId(row));
    }
    this.selectedIds.set(next);
    this.selectionChange.emit([...next]);
  }

  startResize(event: MouseEvent, column: DataGridColumn<T>): void {
    event.preventDefault();
    this.resizing = { key: column.key, startX: event.clientX, startWidth: this.columnWidth(column) };
  }

  @HostListener('document:mousemove', ['$event'])
  resizeWithMouse(event: MouseEvent): void {
    if (!this.resizing) return;
    const direction = document.documentElement.dir === 'rtl' ? -1 : 1;
    this.setColumnWidth(this.resizing.key, this.resizing.startWidth + (event.clientX - this.resizing.startX) * direction);
  }

  @HostListener('document:mouseup')
  stopResize(): void { this.resizing = null; }

  resizeByKeyboard(event: KeyboardEvent, column: DataGridColumn<T>): void {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    if (event.key === 'Home') this.setColumnWidth(column.key, column.width ?? 176);
    else if (event.key === 'End') this.setColumnWidth(column.key, 520);
    else {
      const direction = document.documentElement.dir === 'rtl' ? -1 : 1;
      const delta = (event.key === 'ArrowRight' ? 1 : -1) * direction * 8;
      this.setColumnWidth(column.key, this.columnWidth(column) + delta);
    }
  }

  changePage(delta: number): void { this.page.set(Math.max(0, Math.min(this.pageCount() - 1, this.currentPage() + delta))); }

  summaryLabel(): string {
    const total = this.filteredRows().length;
    const start = total === 0 ? 0 : this.currentPage() * this.pageSize + 1;
    const end = Math.min(total, (this.currentPage() + 1) * this.pageSize);
    return this.language === 'ar' ? `${start}–${end} من ${total} ${this.countLabel}` : `${start}–${end} of ${total} ${this.countLabel}`;
  }

  pagerSummaryLabel(): string { return `${this.currentPage() * this.pageSize + 1}–${Math.min((this.currentPage() + 1) * this.pageSize, this.filteredRows().length)} / ${this.filteredRows().length}`; }

  private matchesFilter(row: T, column: DataGridColumn<T>): boolean {
    const filter = this.filters()[column.key];
    if (!column.filter || filter === undefined || filter === '') return true;
    const raw = column.value(row);
    if (column.filter === 'text') return String(raw ?? '').toLocaleLowerCase().includes(String(filter).trim().toLocaleLowerCase());
    if (column.filter === 'select') return String(raw ?? '') === filter;
    if (typeof filter !== 'object') return true;
    if (column.filter === 'number-range') {
      const value = Number(raw);
      if (!Number.isFinite(value)) return false;
      return (!filter.from || value >= Number(filter.from)) && (!filter.to || value <= Number(filter.to));
    }
    const value = String(raw ?? '').slice(0, 10);
    return (!filter.from || value >= filter.from) && (!filter.to || value <= filter.to);
  }

  private compare(first: string | number | null, second: string | number | null): number {
    if (typeof first === 'number' && typeof second === 'number') return first - second;
    return String(first ?? '').localeCompare(String(second ?? ''), undefined, { numeric: true, sensitivity: 'base' });
  }

  private setColumnWidth(key: string, width: number): void {
    this.columnWidths.update((columns) => ({ ...columns, [key]: Math.max(112, Math.min(520, Math.round(width))) }));
  }
}
