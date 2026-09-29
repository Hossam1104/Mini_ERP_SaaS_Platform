import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, ParamMap, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import {
  MasterDataImportAuditResponse,
  MasterDataImportBatchResponse,
  MasterDataImportReconciliationResponse,
  MasterDataImportRowResponse,
} from './master-data-import.models';
import { MasterDataImportService } from './master-data-import.service';
import { MasterDataImportWorkspaceComponent } from './master-data-import-workspace.component';

@Component({ standalone: true, template: '' })
class RouterTargetComponent {}

function routeMap(id?: string): ParamMap {
  return convertToParamMap(id ? { id } : {});
}

const reconciliation: MasterDataImportReconciliationResponse = {
  totalRows: 3,
  accepted: 1,
  rejected: 1,
  quarantined: 1,
  committed: 0,
  skipped: 0,
  failed: 0,
  isConsistent: true,
  formula: '3 = 1 + 1 + 1',
};

function makeBatch(overrides: Partial<MasterDataImportBatchResponse> = {}): MasterDataImportBatchResponse {
  return {
    id: 'batch-1',
    tenantId: 'tenant-a',
    resourceKind: 'ProductCategory',
    source: { sourceSystemCategory: 'angular-client', sourceFileReference: 'categories.csv', batchReference: null },
    duplicatePolicy: 'Reject',
    mode: 'DryRun',
    status: 'Draft',
    submittedActorId: 'user-1',
    createdAt: '2026-08-14T10:00:00Z',
    startedAt: null,
    completedAt: null,
    correlationId: 'corr-1',
    totalRows: 3,
    acceptedCount: 1,
    rejectedCount: 1,
    quarantinedCount: 1,
    committedCount: 0,
    skippedCount: 0,
    failedCount: 0,
    idempotencyKey: null,
    version: 'v1',
    reconciliation,
    ...overrides,
  };
}

const acceptedRow: MasterDataImportRowResponse = {
  id: 'row-1', batchId: 'batch-1', originalRowNumber: 1, replaySequence: 0, isCurrent: true,
  resourceKind: 'ProductCategory', sourceFields: { code: 'CAT-001' }, normalizedFields: { code: 'CAT-001' },
  outcome: 'Accepted', diagnostics: [], highestSeverity: 'Info', mutationDisposition: 'Committed',
  resultingResourceId: 'res-1', resultingResourceCode: 'CAT-001', replayOfRowId: null, originalRowId: null,
  processedAt: '2026-08-14T10:00:01Z', version: 'v1',
};

const rejectedRow: MasterDataImportRowResponse = {
  id: 'row-2', batchId: 'batch-1', originalRowNumber: 2, replaySequence: 0, isCurrent: true,
  resourceKind: 'ProductCategory', sourceFields: { code: '' }, normalizedFields: { code: '' },
  outcome: 'Rejected',
  diagnostics: [{ code: 'import_field_required', message: 'Category Code is required.', field: 'code', severity: 'Error' }],
  highestSeverity: 'Error', mutationDisposition: 'Failed', resultingResourceId: null, resultingResourceCode: null,
  replayOfRowId: null, originalRowId: null, processedAt: '2026-08-14T10:00:01Z', version: 'v1',
};

const quarantinedRow: MasterDataImportRowResponse = {
  id: 'row-3', batchId: 'batch-1', originalRowNumber: 3, replaySequence: 0, isCurrent: true,
  resourceKind: 'ProductCategory', sourceFields: { code: 'CAT-003', parentcategoryid: 'not-a-guid' },
  normalizedFields: { code: 'CAT-003', parentcategoryid: 'not-a-guid' },
  outcome: 'Quarantined',
  diagnostics: [{ code: 'import_field_invalid', message: 'Parent Category ID is not a valid reference.', field: 'parentCategoryId', severity: 'Warning' }],
  highestSeverity: 'Warning', mutationDisposition: 'NotAttempted', resultingResourceId: null, resultingResourceCode: null,
  replayOfRowId: null, originalRowId: null, processedAt: '2026-08-14T10:00:01Z', version: 'v1',
};

const auditEntry: MasterDataImportAuditResponse = {
  evidenceId: 'ev-1', occurredAt: '2026-08-14T10:00:00Z', operationId: 'master-data-imports.create',
  correlationId: 'corr-1', tenantId: 'tenant-a', actorId: 'user-1', batchId: 'batch-1', rowId: null,
  originalRowNumber: null, resourceKind: 'ProductCategory', outcome: 'Created', sourceReference: 'categories.csv',
  detail: 'Batch created.',
};

describe('MasterDataImportWorkspaceComponent', () => {
  let fixture: ComponentFixture<MasterDataImportWorkspaceComponent>;
  let routeParams: BehaviorSubject<ParamMap>;
  let importService: {
    listBatches: ReturnType<typeof vi.fn>;
    getBatch: ReturnType<typeof vi.fn>;
    getStatus: ReturnType<typeof vi.fn>;
    getRows: ReturnType<typeof vi.fn>;
    getReconciliation: ReturnType<typeof vi.fn>;
    getAudit: ReturnType<typeof vi.fn>;
    getEvidence: ReturnType<typeof vi.fn>;
    createBatch: ReturnType<typeof vi.fn>;
    simulate: ReturnType<typeof vi.fn>;
    execute: ReturnType<typeof vi.fn>;
    replayQuarantinedRow: ReturnType<typeof vi.fn>;
  };
  let language: LanguageService;
  let router: Router;

  async function settleAsyncWork(): Promise<void> {
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  }

  async function navigateTo(id?: string): Promise<void> {
    routeParams.next(routeMap(id));
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  }

  async function uploadFile(content: string, name = 'categories.csv', type = 'text/csv'): Promise<void> {
    const file = new File([content], name, { type });
    fixture.componentInstance.onDrop({
      preventDefault: () => {},
      dataTransfer: { files: [file] },
    } as unknown as DragEvent);
    await new Promise<void>((resolve) => setTimeout(resolve, 20));
    fixture.detectChanges();
    await settleAsyncWork();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    routeParams = new BehaviorSubject(routeMap());
    importService = {
      listBatches: vi.fn(() => of([makeBatch()])),
      getBatch: vi.fn(() => of(makeBatch())),
      getStatus: vi.fn(),
      getRows: vi.fn(() => of([acceptedRow, rejectedRow, quarantinedRow])),
      getReconciliation: vi.fn(() => of(reconciliation)),
      getAudit: vi.fn(() => of([auditEntry])),
      getEvidence: vi.fn(() => of({ batch: makeBatch(), rows: [acceptedRow, rejectedRow, quarantinedRow], audit: [auditEntry], reconciliation })),
      createBatch: vi.fn(() => Promise.resolve(makeBatch({ status: 'Draft' }))),
      simulate: vi.fn(() => Promise.resolve(makeBatch({ status: 'Validated' }))),
      execute: vi.fn(() => Promise.resolve(makeBatch({ status: 'Completed', mode: 'Commit' }))),
      replayQuarantinedRow: vi.fn(() => Promise.resolve(makeBatch({ status: 'Validated' }))),
    };

    await TestBed.configureTestingModule({
      imports: [MasterDataImportWorkspaceComponent],
      providers: [
        provideRouter([
          { path: 'app/master-data/imports', component: RouterTargetComponent },
          { path: 'app/master-data/imports/:id', component: RouterTargetComponent },
        ]),
        LanguageService,
        { provide: MasterDataImportService, useValue: importService },
        { provide: AuthService, useValue: { status: signal('authenticated'), session: signal({ selectedContextId: 'context-a' }) } },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: routeParams.asObservable(), get snapshot() { return { paramMap: routeParams.value }; } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MasterDataImportWorkspaceComponent);
    language = TestBed.inject(LanguageService);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    await fixture.whenStable();
    await settleAsyncWork();
    fixture.detectChanges();
  });

  it('loads batch history on the list view', () => {
    expect(importService.listBatches).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Category');
    expect(fixture.nativeElement.textContent).toContain('Draft');
  });

  it('shows an empty state when there are no batches yet', async () => {
    importService.listBatches.mockReturnValueOnce(of([]));
    await navigateTo();
    expect(fixture.nativeElement.textContent).toContain(language.text('importNoBatchesYet'));
  });

  it('never renders a Delete action in batch history', () => {
    expect(fixture.nativeElement.textContent).not.toContain('Delete');
  });

  it('navigates to the wizard when "New Import" is clicked', () => {
    fixture.componentInstance.openNewImport();
    expect(router.navigate).toHaveBeenCalledWith(['/app/master-data/imports/new']);
  });

  it('resets facade state and starts at the resource step when entering the wizard', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    expect(component.viewMode()).toBe('wizard');
    expect(component.wizardStep()).toBe('resource');
    expect(component.facade.selectedResourceKind()).toBeNull();
  });

  it('cannot advance past the resource step until a resource kind is selected', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    expect(component.canAdvance('resource')).toBe(false);
    component.onSelectResource('ProductCategory');
    fixture.detectChanges();
    expect(component.canAdvance('resource')).toBe(true);
  });

  it('selects a resource card by click and highlights it', async () => {
    await navigateTo('new');
    const cards = fixture.nativeElement.querySelectorAll('.resource-card') as NodeListOf<HTMLButtonElement>;
    expect(cards.length).toBe(10);
    cards[0].click();
    fixture.detectChanges();
    expect(fixture.componentInstance.facade.selectedResourceKind()).not.toBeNull();
  });

  it('rejects an unsupported file type before parsing', async () => {
    await navigateTo('new');
    fixture.componentInstance.onSelectResource('ProductCategory');
    fixture.componentInstance.goNext();
    fixture.detectChanges();
    await uploadFile('code\nCAT-001', 'categories.txt', 'text/plain');
    expect(fixture.componentInstance.localFileErrorMessage()).toBe(language.text('importFileUnsupportedType'));
    expect(fixture.componentInstance.facade.fileMetadata()).toBeNull();
  });

  it('parses a valid CSV file and advances mapping readiness', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics\nCAT-002,Hardware');

    expect(component.facade.parseResult()?.valid).toBe(true);
    expect(component.facade.fileMetadata()?.name).toBe('categories.csv');
    expect(fixture.nativeElement.textContent).toContain(language.text('importFileParsedOk'));
  });

  it('auto-matches columns and flags the required Category Code as auto-matched', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    fixture.detectChanges();

    const codeMapping = component.facade.columnMappings().find((m) => m.sourceColumn === 'code');
    expect(codeMapping?.targetField).toBe('code');
    expect(component.mappingOrigin(codeMapping!)).toBe('auto');
  });

  it('flags a missing required field mapping and blocks advancing past mapping', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('label\nElectronics');
    component.goNext();
    fixture.detectChanges();

    expect(component.canAdvance('mapping')).toBe(false);
    expect(fixture.nativeElement.textContent).toContain(language.text('importMappingMissingRequired'));
  });

  it('lets the user manually remap a column and marks it as manual, not auto', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,label\nCAT-001,Electronics');
    component.onMappingChange('label', 'englishName');
    fixture.detectChanges();

    const labelMapping = component.facade.columnMappings().find((m) => m.sourceColumn === 'label');
    expect(component.mappingOrigin(labelMapping!)).toBe('manual');
  });

  it('caps the preview at 50 rows while reporting the true total', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    const rows = ['code,englishName', ...Array.from({ length: 60 }, (_, i) => `CAT-${i},Category ${i}`)].join('\n');
    await uploadFile(rows);
    component.goNext();
    fixture.detectChanges();

    expect(component.previewTotal()).toBe(60);
    expect(component.previewRows().length).toBe(50);
  });

  it('removing a file clears parse state and mapping', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    expect(component.facade.fileMetadata()).not.toBeNull();

    component.removeFile();
    fixture.detectChanges();
    expect(component.facade.fileMetadata()).toBeNull();
    expect(component.facade.columnMappings().length).toBe(0);
  });

  it('creates and simulates a batch, then advances to the execute step', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    fixture.detectChanges();
    expect(component.wizardStep()).toBe('validate');

    await component.onCreateAndSimulate();
    fixture.detectChanges();

    expect(importService.createBatch).toHaveBeenCalled();
    expect(importService.simulate).toHaveBeenCalled();
    expect(component.wizardStep()).toBe('execute');
  });

  it('shows the reconciliation tiles and consistency banner after simulation', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('importReconciliationConsistent'));
    expect(fixture.nativeElement.textContent).toContain('3');
  });

  it('blocks execution and hides the Execute button for Dry Run mode', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.onSelectMode('DryRun');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    expect(component.facade.canExecute()).toBe(false);
    expect(fixture.nativeElement.querySelector('.button--danger')).toBeNull();
  });

  it('opens the execute confirmation dialog for a Commit-mode validated batch and executes on confirm', async () => {
    importService.simulate.mockResolvedValueOnce(makeBatch({ status: 'Validated', mode: 'Commit' }));
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.onSelectMode('Commit');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    expect(component.facade.canExecute()).toBe(true);
    component.openExecuteConfirm();
    fixture.detectChanges();
    expect(component.executeConfirmOpen()).toBe(true);

    await component.confirmExecute();
    fixture.detectChanges();

    expect(importService.execute).toHaveBeenCalledWith('batch-1', 'v1');
    expect(component.executeConfirmOpen()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain(language.text('importExecutionCompleted'));
  });

  it('blocks execution when server reconciliation reports inconsistency', async () => {
    importService.simulate.mockResolvedValueOnce(
      makeBatch({ status: 'Validated', mode: 'Commit', reconciliation: { ...reconciliation, isConsistent: false } }),
    );
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.onSelectMode('Commit');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.openExecuteConfirm();
    expect(component.executeConfirmOpen()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain(language.text('importReconciliationInconsistent'));
  });

  it('shows "Completed with errors" for a partial-success batch', async () => {
    importService.execute.mockResolvedValueOnce(makeBatch({ status: 'CompletedWithErrors', mode: 'Commit' }));
    importService.simulate.mockResolvedValueOnce(makeBatch({ status: 'Validated', mode: 'Commit' }));
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.onSelectMode('Commit');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    await component.confirmExecute();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('importExecutionCompletedWithErrors'));
  });

  it('filters row outcomes by outcome type', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.setRowFilter('quarantined');
    fixture.detectChanges();
    expect(component.filteredRows().length).toBe(1);
    expect(component.filteredRows()[0].outcome).toBe('Quarantined');
  });

  it('searches row outcomes by resulting record code', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.rowSearch.set('cat-001');
    fixture.detectChanges();
    expect(component.filteredRows().length).toBe(1);
    expect(component.filteredRows()[0].id).toBe('row-1');
  });

  it('opens a row detail dialog showing diagnostics with icon, text, and severity badge', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.openRowDetail(rejectedRow);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Category Code is required.');
    expect(fixture.nativeElement.textContent).toContain(language.text('severityError'));
  });

  it('does not offer a replay path for rejected rows', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.openRowDetail(rejectedRow);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(language.text('importRejectedNoReplayNote'));
    expect(fixture.nativeElement.querySelector('input[name^="replay-"]')).toBeNull();
  });

  it('allows correcting and replaying a quarantined row', async () => {
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();

    component.openRowDetail(quarantinedRow);
    fixture.detectChanges();
    expect(component.replayDraft()).toEqual(quarantinedRow.normalizedFields);

    component.setReplayField('parentcategoryid', '');
    await component.submitReplay();
    fixture.detectChanges();

    expect(importService.replayQuarantinedRow).toHaveBeenCalledWith(
      'batch-1', 'row-3', { correctedFields: expect.objectContaining({ parentcategoryid: null }) }, expect.any(String),
    );
    expect(component.selectedRow()).toBeNull();
  });

  it('opens an existing batch by id and loads its rows', async () => {
    await navigateTo('batch-1');
    expect(importService.getBatch).toHaveBeenCalledWith('batch-1');
    expect(fixture.componentInstance.viewMode()).toBe('detail');
    expect(fixture.nativeElement.textContent).toContain('corr-1');

    fixture.componentInstance.setDetailTab('rows');
    fixture.detectChanges();

    expect(importService.getRows).toHaveBeenCalledWith('batch-1');
    expect(fixture.nativeElement.textContent).toContain('CAT-001');
  });

  it('lazy-loads the audit tab only on first activation', async () => {
    await navigateTo('batch-1');
    expect(importService.getAudit).not.toHaveBeenCalled();

    fixture.componentInstance.setDetailTab('audit');
    fixture.detectChanges();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(importService.getAudit).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Batch created.');

    fixture.componentInstance.setDetailTab('rows');
    fixture.componentInstance.setDetailTab('audit');
    fixture.detectChanges();
    expect(importService.getAudit).toHaveBeenCalledTimes(1);
  });

  it('lazy-loads the evidence tab and shows the batch reference', async () => {
    await navigateTo('batch-1');
    fixture.componentInstance.setDetailTab('evidence');
    fixture.detectChanges();
    await settleAsyncWork();
    fixture.detectChanges();

    expect(importService.getEvidence).toHaveBeenCalledWith('batch-1');
    expect(fixture.nativeElement.textContent).toContain('batch-1');
  });

  it('surfaces a safe message for an unknown import error code without leaking internals', async () => {
    importService.getBatch.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 500, error: { code: 'import_persistence_failed' } })),
    );
    await navigateTo('batch-1');

    expect(fixture.nativeElement.textContent).toContain(language.text('importUnavailableErrorMsg'));
    expect(fixture.nativeElement.textContent).not.toContain('SqlException');
  });

  it('renders no <img> tags of its own, leaving owner brand assets untouched', () => {
    expect(fixture.nativeElement.querySelectorAll('img').length).toBe(0);
  });

  it('renders wizard step labels in Arabic and flips document direction to RTL after toggling language', async () => {
    await navigateTo('new');
    language.toggle();
    fixture.detectChanges();

    expect(document.documentElement.dir).toBe('rtl');
    expect(fixture.nativeElement.textContent).toContain(language.text('importStepResource'));
    expect(fixture.nativeElement.textContent).not.toContain('Resource & Policy'.split(' ')[0]);
  });

  // ==================== MESP-122 Phase C1: planner-detected corrections ====================

  async function reachCommitExecuteStep() {
    importService.simulate.mockResolvedValueOnce(makeBatch({ status: 'Validated', mode: 'Commit' }));
    await navigateTo('new');
    const component = fixture.componentInstance;
    component.onSelectResource('ProductCategory');
    component.onSelectMode('Commit');
    component.goNext();
    await uploadFile('code,englishName\nCAT-001,Electronics');
    component.goNext();
    component.goNext();
    component.goNext();
    await component.onCreateAndSimulate();
    fixture.detectChanges();
    return component;
  }

  describe('Finding 1: execute confirmation dialog', () => {
    it('sources counts from server reconciliation and warns when not all rows are eligible', async () => {
      const component = await reachCommitExecuteStep();

      component.openExecuteConfirm();
      fixture.detectChanges();

      const dialogText = (fixture.nativeElement.querySelector('.dialog-panel') as HTMLElement).textContent ?? '';
      expect(dialogText).toContain(String(reconciliation.accepted));
      expect(dialogText).toContain(String(reconciliation.rejected));
      expect(dialogText).toContain(String(reconciliation.quarantined));
      expect(dialogText).toContain(language.text('importExecutePartialEligibilityWarning'));
    });

    it('blocks opening and disables Execute when server reconciliation evidence is unavailable', async () => {
      const component = await reachCommitExecuteStep();

      component.facade.batchReconciliation.set(null);
      fixture.detectChanges();

      component.openExecuteConfirm();
      expect(component.executeConfirmOpen()).toBe(false);

      const executeButton = fixture.nativeElement.querySelector('.button--primary') as HTMLButtonElement;
      expect(executeButton.disabled).toBe(true);
    });

    it('shows the reconciliation-missing alert and disables Execute if reconciliation disappears while the dialog is open', async () => {
      const component = await reachCommitExecuteStep();

      component.openExecuteConfirm();
      fixture.detectChanges();
      expect(component.executeConfirmOpen()).toBe(true);

      component.facade.batchReconciliation.set(null);
      fixture.detectChanges();

      const dialogText = (fixture.nativeElement.querySelector('.dialog-panel') as HTMLElement).textContent ?? '';
      expect(dialogText).toContain(language.text('importExecuteReconciliationMissing'));
      const executeButton = fixture.nativeElement.querySelector('.dialog-panel .button--danger') as HTMLButtonElement;
      expect(executeButton.disabled).toBe(true);
    });
  });

  describe('Finding 2: evidence tab', () => {
    it('renders the full consolidated evidence bundle with batch, source, reconciliation, row, and audit sections', async () => {
      await navigateTo('batch-1');
      fixture.componentInstance.setDetailTab('evidence');
      fixture.detectChanges();
      await settleAsyncWork();
      fixture.detectChanges();

      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain(language.text('importEvidenceBatchSection'));
      expect(text).toContain(language.text('importEvidenceSourceSection'));
      expect(text).toContain(language.text('importEvidenceReconciliationSection'));
      expect(text).toContain(language.text('importEvidenceRowSection'));
      expect(text).toContain(language.text('importEvidenceAuditSection'));
      expect(text).toContain('categories.csv');
      expect(text).toContain('Batch created.');
      expect(fixture.componentInstance.evidenceStatus()).toBe('loaded');
    });

    it('marks historical (superseded) rows distinctly in the row evidence table', async () => {
      const historicalOriginal: MasterDataImportRowResponse = {
        ...quarantinedRow,
        id: 'row-3-original',
        isCurrent: false,
        replaySequence: 0,
      };
      const replayRow: MasterDataImportRowResponse = {
        ...quarantinedRow,
        id: 'row-3-replay',
        isCurrent: true,
        replaySequence: 1,
        originalRowId: 'row-3-original',
        outcome: 'Accepted',
        mutationDisposition: 'Committed',
      };
      importService.getEvidence.mockReturnValueOnce(
        of({
          batch: makeBatch(),
          rows: [acceptedRow, rejectedRow, historicalOriginal, replayRow],
          audit: [auditEntry],
          reconciliation,
        }),
      );
      await navigateTo('batch-1');
      fixture.componentInstance.setDetailTab('evidence');
      fixture.detectChanges();
      await settleAsyncWork();
      fixture.detectChanges();

      const historicalCell = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.grid-cell-detail'))
        .find((cell) => cell.textContent?.trim() === language.text('importEvidenceHistorical'));
      const historicalRow = historicalCell?.closest('tr') as HTMLElement | null;
      expect(historicalRow).not.toBeNull();
      expect(historicalRow?.textContent ?? '').toContain(language.text('importEvidenceHistorical'));
    });

    it('shows a loading state, then a safe error with Retry when the evidence request fails, and recovers on retry', async () => {
      importService.getEvidence.mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 500, error: { code: 'import_persistence_failed' } })),
      );
      await navigateTo('batch-1');
      const component = fixture.componentInstance;

      component.setDetailTab('evidence');
      expect(component.evidenceStatus()).toBe('loading');
      fixture.detectChanges();

      await settleAsyncWork();
      fixture.detectChanges();

      expect(component.evidenceStatus()).toBe('error');
      expect(fixture.nativeElement.textContent).toContain(language.text('importUnavailableErrorMsg'));
      expect(fixture.nativeElement.textContent).not.toContain('import_persistence_failed');

      importService.getEvidence.mockReturnValueOnce(
        of({ batch: makeBatch(), rows: [acceptedRow, rejectedRow, quarantinedRow], audit: [auditEntry], reconciliation }),
      );
      const retryButton = Array.from(fixture.nativeElement.querySelectorAll('.text-button') as NodeListOf<HTMLButtonElement>).find(
        (btn) => btn.textContent?.includes(language.text('retry')),
      );
      retryButton?.click();
      fixture.detectChanges();
      await settleAsyncWork();
      fixture.detectChanges();

      expect(component.evidenceStatus()).toBe('loaded');
      expect(importService.getEvidence).toHaveBeenCalledTimes(2);
    });
  });

  describe('Finding 3A: dialog accessibility', () => {
    it('moves initial focus to the Cancel/Back action (not the destructive Execute button) when the execute dialog opens', async () => {
      const component = await reachCommitExecuteStep();

      component.openExecuteConfirm();
      fixture.detectChanges();
      await settleAsyncWork();

      expect(document.activeElement?.id).toBe('execute-confirm-cancel');
    });

    it('traps Tab focus within the execute confirmation dialog', async () => {
      const component = await reachCommitExecuteStep();

      component.openExecuteConfirm();
      fixture.detectChanges();
      await settleAsyncWork();

      const panel = fixture.nativeElement.querySelector('.dialog-panel') as HTMLElement;
      const focusable = Array.from(
        panel.querySelectorAll('button:not([disabled]), [tabindex]:not([tabindex="-1"])'),
      ) as HTMLElement[];
      expect(focusable.length).toBeGreaterThan(1);

      const last = focusable[focusable.length - 1];
      last.focus();
      last.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
      fixture.detectChanges();

      expect(document.activeElement).toBe(focusable[0]);
    });

    it('does not allow Escape to close the execute dialog while an execution request is in flight', async () => {
      const component = await reachCommitExecuteStep();

      component.openExecuteConfirm();
      fixture.detectChanges();
      component.facade.busy.set(true);

      const panel = fixture.nativeElement.querySelector('.dialog-panel') as HTMLElement;
      panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
      fixture.detectChanges();

      expect(component.executeConfirmOpen()).toBe(true);
      component.facade.busy.set(false);
    });

    it('restores focus to the opener element when the execute confirmation dialog closes', async () => {
      const component = await reachCommitExecuteStep();

      const opener = fixture.nativeElement.querySelector('.button--primary') as HTMLButtonElement;
      opener.focus();
      component.openExecuteConfirm();
      fixture.detectChanges();
      await settleAsyncWork();

      component.closeExecuteConfirm();
      fixture.detectChanges();
      await settleAsyncWork();

      expect(document.activeElement).toBe(opener);
    });

    it('moves initial focus to the row detail heading (never the replay input) for a quarantined row', async () => {
      const component = await reachCommitExecuteStep();

      component.openRowDetail(quarantinedRow);
      fixture.detectChanges();
      await settleAsyncWork();

      expect(document.activeElement?.id).toBe('row-detail-title');
      expect(document.activeElement?.tagName.toLowerCase()).not.toBe('input');
    });

    it('closes the row detail dialog on Escape and restores focus to the previously focused element', async () => {
      const component = await reachCommitExecuteStep();

      const opener = document.createElement('button');
      document.body.appendChild(opener);
      opener.focus();

      component.openRowDetail(rejectedRow);
      fixture.detectChanges();

      const panel = fixture.nativeElement.querySelector('.dialog-panel') as HTMLElement;
      panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();

      expect(component.selectedRow()).toBeNull();
      expect(document.activeElement).toBe(opener);
      document.body.removeChild(opener);
    });

    it('does not offer a replay form for historical (superseded) quarantined rows in the dialog', async () => {
      const component = await reachCommitExecuteStep();
      const historicalQuarantined: MasterDataImportRowResponse = { ...quarantinedRow, isCurrent: false };

      component.openRowDetail(historicalQuarantined);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('input[name^="replay-"]')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain(language.text('importHistoricalRowNote'));
    });
  });

  describe('Finding 3B: tabs accessibility', () => {
    it('uses roving tabindex with only the active tab focusable', async () => {
      await navigateTo('batch-1');
      fixture.detectChanges();

      const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]') as NodeListOf<HTMLButtonElement>;
      expect(tabs.length).toBe(5);
      expect(tabs[0].getAttribute('tabindex')).toBe('0');
      expect(tabs[1].getAttribute('tabindex')).toBe('-1');
      expect(tabs[0].getAttribute('aria-controls')).toBe('import-tabpanel-summary');
    });

    it('moves focus and selection to the next tab with ArrowRight, and wraps from the last tab', async () => {
      await navigateTo('batch-1');
      const component = fixture.componentInstance;
      const nav = fixture.nativeElement.querySelector('[role="tablist"]') as HTMLElement;

      nav.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();
      fixture.detectChanges();
      expect(component.detailTab()).toBe('rows');
      expect(document.activeElement?.id).toBe('import-tab-rows');

      component.setDetailTab('evidence');
      fixture.detectChanges();
      nav.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();
      expect(component.detailTab()).toBe('summary');
    });

    it('jumps to the first/last tab with Home/End', async () => {
      await navigateTo('batch-1');
      const component = fixture.componentInstance;
      const nav = fixture.nativeElement.querySelector('[role="tablist"]') as HTMLElement;

      nav.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();
      expect(component.detailTab()).toBe('evidence');

      nav.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();
      expect(component.detailTab()).toBe('summary');
    });

    it('reverses ArrowRight/ArrowLeft direction for the RTL tab order', async () => {
      await navigateTo('batch-1');
      const component = fixture.componentInstance;
      language.toggle();
      fixture.detectChanges();
      expect(component.detailTab()).toBe('summary');

      const nav = fixture.nativeElement.querySelector('[role="tablist"]') as HTMLElement;
      nav.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
      fixture.detectChanges();
      await settleAsyncWork();

      expect(component.detailTab()).toBe('evidence');
    });
  });
});
