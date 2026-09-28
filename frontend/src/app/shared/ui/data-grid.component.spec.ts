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
    { key: 'total', label: 'Total', value: (row) => row.total, display: (row) => row.total.toFixed(2), currencySymbol: (row) => ({ url: null, text: row.currencyCode }), align: 'end', filter: 'number-range' },
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
    expect(element.querySelector('.data-grid-money')?.textContent).toContain('SAR');
    expect(element.querySelector('.sort-icon use')?.getAttribute('href')).toBe('#icon-arrow-up-down');
    expect(element.querySelector('.grid-filter-button use')?.getAttribute('href')).toBe('#icon-filter');
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
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No records');
    fixture.componentRef.setInput('loading', true);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading records');
  });
});
