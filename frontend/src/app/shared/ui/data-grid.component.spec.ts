import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DataGridColumn, DataGridComponent } from './data-grid.component';

interface Row { id: string; name: string; total: number; currencyCode: string; status: string; }

describe('DataGridComponent', () => {
  let fixture: ComponentFixture<DataGridComponent<Row>>;
  let grid: DataGridComponent<Row>;
  const rows: Row[] = [
    { id: 'po-2', name: 'Beta', total: 250, currencyCode: 'SAR', status: 'Approved' },
    { id: 'po-1', name: 'Alpha', total: 100, currencyCode: 'USD', status: 'Draft' },
  ];
  const columns: DataGridColumn<Row>[] = [
    { key: 'name', label: 'Supplier', value: (row) => row.name, filter: 'text' },
    { key: 'total', label: 'Total', value: (row) => row.total, display: (row) => row.total.toFixed(2), currencySymbol: (row) => ({ url: row.currencyCode === 'SAR' ? '/assets/Saudi_Riyal.svg' : null, text: row.currencyCode }), align: 'end', filter: 'number-range' },
    { key: 'status', label: 'Status', value: (row) => row.status, badge: true, filter: 'select' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DataGridComponent] }).compileComponents();
    fixture = TestBed.createComponent(DataGridComponent<Row>);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('columns', columns);
    fixture.detectChanges();
    grid = fixture.componentInstance;
  });

  it('sorts a column ascending and descending', () => {
    grid.sortBy(columns[0]);
    expect(grid.filteredRows().map((row) => row.name)).toEqual(['Alpha', 'Beta']);
    grid.sortBy(columns[0]);
    expect(grid.filteredRows().map((row) => row.name)).toEqual(['Beta', 'Alpha']);
    expect(grid.ariaSort(columns[0])).toBe('descending');
  });

  it('renders semantic status pills, currency labels, and SVG grid controls', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.data-grid-badge--approved')?.textContent).toContain('Approved');
    expect(element.querySelector('[role=\"img\"][aria-label=\"SAR\"]')?.getAttribute('style')).toContain('Saudi_Riyal.svg');
    expect(element.querySelector('[data-row-id="po-2"] .data-grid-money')?.textContent).toContain('250.00');
    expect(element.querySelector('[data-row-id=\"po-1\"] .data-grid-money')?.textContent).toContain('USD');
    expect(element.querySelector('[data-row-id=\"po-1\"] .currency-symbol-asset')).toBeNull();
    expect(element.querySelector('.sort-icon use')?.getAttribute('href')).toBe('#icon-arrow-up-down');
    expect(element.querySelector('.grid-filter-button use')?.getAttribute('href')).toBe('#icon-filter');
    const numericHeader = element.querySelector('th.numeric .grid-sort') as HTMLElement;
    const numericCell = element.querySelector('[data-row-id="po-2"] td.numeric') as HTMLElement;
    expect(getComputedStyle(numericHeader).textAlign).toBe(getComputedStyle(numericCell).textAlign);
    const textCell = element.querySelector('[data-row-id="po-2"] td') as HTMLElement;
    expect(getComputedStyle(textCell).overflowWrap).toBe('anywhere');
    expect(getComputedStyle(textCell).whiteSpace).toBe('normal');
    expect(grid.columnWidth({ ...columns[0], width: 80 })).toBe(grid.columnMinWidth);
  });

  it('applies text, select, and numeric range filters', () => {
    grid.setTextFilter('name', 'alp');
    expect(grid.filteredRows().map((row) => row.id)).toEqual(['po-1']);
    grid.clearFilter('name');
    grid.setTextFilter('status', 'Approved');
    expect(grid.filteredRows().map((row) => row.id)).toEqual(['po-2']);
    grid.clearFilter('status');
    grid.setRangeFilter('total', 'from', '150');
    expect(grid.filteredRows().map((row) => row.id)).toEqual(['po-2']);
  });

  it('clamps the current page when the row set shrinks', () => {
    const manyRows = Array.from({ length: 15 }, (_, index) => ({ ...rows[0], id: `po-${index + 1}`, name: `Row ${index + 1}` }));
    fixture.componentRef.setInput('rows', manyRows);
    fixture.detectChanges();
    grid.changePage(1);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.pager-controls span')?.textContent).toContain('2 / 3');

    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();

    expect(grid.page()).toBe(0);
    expect(grid.pageRows().map((row) => row.id)).toEqual(['po-2', 'po-1']);
    expect(grid.pagerSummaryLabel()).toBe('1–2 / 2');
    expect((fixture.nativeElement as HTMLElement).querySelector('.pager-controls span')?.textContent).toContain('1 / 1');
  });

  it('resizes with mouse and keyboard within the configured bounds', () => {
    grid.startResize(new MouseEvent('mousedown', { clientX: 100 }), columns[0]);
    grid.resizeWithMouse(new MouseEvent('mousemove', { clientX: 150 }));
    expect(grid.columnWidth(columns[0])).toBe(226);

    grid.resizeByKeyboard(new KeyboardEvent('keydown', { key: 'ArrowRight', cancelable: true }), columns[0]);
    expect(grid.columnWidth(columns[0])).toBe(234);
    grid.resizeByKeyboard(new KeyboardEvent('keydown', { key: 'End', cancelable: true }), columns[0]);
    expect(grid.columnWidth(columns[0])).toBe(520);
    grid.resizeByKeyboard(new KeyboardEvent('keydown', { key: 'Home', cancelable: true }), columns[0]);
    expect(grid.columnWidth(columns[0])).toBe(176);
  });

  it('selects rows and exposes empty/loading states', () => {
    const changed: string[][] = [];
    grid.selectionChange.subscribe((ids) => changed.push([...ids]));
    const input = document.createElement('input');
    input.checked = true;
    const event = new Event('change');
    input.dispatchEvent(event);
    grid.toggleRowSelection(rows[0], event);
    expect(grid.selectedIds().has('po-2')).toBe(true);
    expect(changed).toEqual([['po-2']]);

    fixture.componentRef.setInput('rows', []);
    fixture.detectChanges();
    const emptyElement = fixture.nativeElement as HTMLElement;
    expect(emptyElement.textContent).toContain('No records');
    expect(emptyElement.querySelector('thead')?.textContent).toContain('Supplier');
    fixture.componentRef.setInput('loading', true);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    const loadingElement = fixture.nativeElement as HTMLElement;
    expect(loadingElement.textContent).toContain('Loading records');
    expect(loadingElement.querySelector('thead')?.textContent).toContain('Supplier');
  });
});
