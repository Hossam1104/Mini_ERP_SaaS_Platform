import { DatePipe, LowerCasePipe, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SafeUiError } from '../../core/api/safe-error';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService, TranslationKey } from '../../core/i18n/language.service';
import { MAX_FILE_SIZE_BYTES } from './import-parser';
import { MasterDataImportFacade } from './master-data-import.facade';
import { DataGridColumn, DataGridComponent } from '../../shared/ui/data-grid.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import {
  IMPORT_RESOURCE_DEFINITIONS,
  ImportColumnMapping,
  ImportFieldDefinition,
  MasterDataImportAuditResponse,
  MasterDataImportBatchResponse,
  MasterDataImportDuplicatePolicy,
  MasterDataImportMode,
  MasterDataImportResourceKind,
  MasterDataImportRowInput,
  MasterDataImportRowResponse,
  MasterDataImportStatus,
  getImportResourceDefinition,
} from './master-data-import.models';

type ImportViewMode = 'list' | 'wizard' | 'detail';
type ImportWizardStep = 'resource' | 'file' | 'mapping' | 'preview' | 'validate' | 'execute';
type ImportDetailTab = 'summary' | 'rows' | 'reconciliation' | 'audit' | 'evidence';
type RowFilter = 'all' | 'accepted' | 'rejected' | 'quarantined' | 'warnings' | 'errors';
type LocalFileError = 'unsupported' | 'tooLarge' | 'empty' | 'parse' | null;
type SimulatePhase = 'idle' | 'creating' | 'simulating';

const WIZARD_STEPS: ImportWizardStep[] = ['resource', 'file', 'mapping', 'preview', 'validate', 'execute'];

@Component({
  selector: 'app-master-data-import-workspace',
  standalone: true,
  imports: [DatePipe, LowerCasePipe, FormsModule, NgTemplateOutlet, RouterLink, DataGridComponent, PageHeaderComponent],
  template: `
    <section class="import-workspace" aria-labelledby="import-title">
      <app-page-header>
        <div page-header-copy>
          <p class="eyebrow">{{ language.text('masterData') }} / {{ language.text('tenantCatalog') }}</p>
          <h1 id="import-title">{{ language.text('importWorkspaceTitle') }}</h1>
          <p>{{ language.text('importWorkspaceLead') }}</p>
        </div>
        <div page-header-actions>
          @if (viewMode() === 'list') {
            <button class="button button--primary" type="button" (click)="openNewImport()" [disabled]="!canMutate()" [title]="canMutate() ? '' : language.text('accessUnavailable')">＋ {{ language.text('newImport') }}</button>
          } @else {
            <a class="back-link" routerLink="/app/master-data/imports">← {{ language.text('backToImports') }}</a>
          }
        </div>
      </app-page-header>

      <div class="workspace-panel">
        @switch (viewMode()) {
          @case ('list') { <ng-container *ngTemplateOutlet="listView" /> }
          @case ('wizard') { <ng-container *ngTemplateOutlet="wizardView" /> }
          @case ('detail') { <ng-container *ngTemplateOutlet="detailView" /> }
        }
      </div>
    </section>

    <!-- ==================== LIST / BATCH HISTORY ==================== -->
    <ng-template #listView>
      <section class="list-view" aria-labelledby="import-list-title">
        <div class="section-heading">
          <div>
            <p class="eyebrow eyebrow--soft">{{ language.text('importBatchHistory') }}</p>
            <h2 id="import-list-title">{{ language.text('importBatchListTitle') }}</h2>
          </div>
          <button class="button button--quiet" type="button" (click)="facade.refreshBatchList()" [disabled]="facade.busy()" [attr.aria-label]="language.text('refresh')">↻ <span>{{ language.text('refresh') }}</span></button>
        </div>

        @if (facade.busy() && facade.batchList().length === 0) {
          <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
        } @else if (facade.error()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(facade.error()) }}</b><button class="text-button" type="button" (click)="facade.refreshBatchList()">{{ language.text('retry') }} ↗</button></div></div>
        } @else if (facade.batchList().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><div><b>{{ language.text('importNoBatchesYet') }}</b><p>{{ language.text('importNoBatchesYetLead') }}</p></div></div>
        } @else {
          <app-data-grid [rows]="facade.batchList()" [columns]="batchColumns" [rowActionsTemplate]="batchActions" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importBatchListTitle')" [countLabel]="language.text('importBatchListTitle')">
            <ng-template #batchActions let-batch>
              <button class="text-button" type="button" (click)="openBatch(batch.id)">{{ language.text('importOpenBatch') }} ↗</button>
            </ng-template>
          </app-data-grid>
        }
      </section>
    </ng-template>

    <!-- ==================== WIZARD (NEW IMPORT) ==================== -->
    <ng-template #wizardView>
      <section class="wizard-view" aria-labelledby="import-wizard-title">
        <div class="section-heading">
          <div>
            <p class="eyebrow eyebrow--soft">{{ language.text('newImport') }}</p>
            <h2 id="import-wizard-title">{{ resourceLabel(facade.selectedResourceKind()) }}</h2>
          </div>
          <button class="text-button" type="button" (click)="resetWizard()">{{ language.text('importResetWizard') }}</button>
        </div>

        <ol class="stepper" [attr.aria-label]="language.text('importWorkspaceTitle')">
          @for (step of wizardSteps; track step) {
            <li>
              <button
                type="button"
                class="stepper__item"
                [class.is-complete]="stepStatus(step) === 'complete'"
                [class.is-current]="stepStatus(step) === 'current'"
                [disabled]="stepStatus(step) === 'pending'"
                [attr.aria-current]="stepStatus(step) === 'current' ? 'step' : null"
                (click)="goToStep(step)"
              >
                <span class="stepper__mark" aria-hidden="true">{{ stepStatus(step) === 'complete' ? '✓' : wizardSteps.indexOf(step) + 1 }}</span>
                <span class="stepper__label">{{ stepLabel(step) }}<small>{{ stepStatusLabel(stepStatus(step)) }}</small></span>
              </button>
            </li>
          }
        </ol>

        @if (facade.error() && wizardStep() !== 'execute') {
          <div class="inline-alert" role="alert"><b>{{ errorMessage(facade.error()) }}</b></div>
        }

        @switch (wizardStep()) {
          @case ('resource') { <ng-container *ngTemplateOutlet="resourceStep" /> }
          @case ('file') { <ng-container *ngTemplateOutlet="fileStep" /> }
          @case ('mapping') { <ng-container *ngTemplateOutlet="mappingStep" /> }
          @case ('preview') { <ng-container *ngTemplateOutlet="previewStep" /> }
          @case ('validate') { <ng-container *ngTemplateOutlet="validateStep" /> }
          @case ('execute') { <ng-container *ngTemplateOutlet="executeStep" /> }
        }
      </section>
    </ng-template>

    <!-- Step 1: Resource & Policy -->
    <ng-template #resourceStep>
      <div class="wizard-step">
        <h3>{{ language.text('importResourceType') }}</h3>
        <p class="muted-line">{{ language.text('importSelectResourceType') }}</p>
        <div class="resource-grid" role="radiogroup" [attr.aria-label]="language.text('importResourceType')">
          @for (def of resourceDefinitions; track def.kind) {
            <button
              type="button"
              class="resource-card"
              role="radio"
              [attr.aria-checked]="facade.selectedResourceKind() === def.kind"
              [class.is-selected]="facade.selectedResourceKind() === def.kind"
              (click)="onSelectResource(def.kind)"
            >
              <span class="resource-card__name">{{ resourceLabel(def.kind) }}</span>
            </button>
          }
        </div>

        <div class="panel-block">
          <h3>{{ language.text('importDuplicatePolicy') }}</h3>
          <div class="policy-grid" role="radiogroup" [attr.aria-label]="language.text('importDuplicatePolicy')">
            <label class="policy-option" [class.is-selected]="facade.duplicatePolicy() === 'Reject'">
              <input type="radio" name="duplicatePolicy" value="Reject" [checked]="facade.duplicatePolicy() === 'Reject'" (change)="onSelectPolicy('Reject')" />
              <span><b>{{ language.text('importDuplicatePolicyReject') }}</b><small>{{ language.text('importDuplicatePolicyRejectHint') }}</small></span>
            </label>
            <label class="policy-option" [class.is-selected]="facade.duplicatePolicy() === 'SkipExisting'">
              <input type="radio" name="duplicatePolicy" value="SkipExisting" [checked]="facade.duplicatePolicy() === 'SkipExisting'" (change)="onSelectPolicy('SkipExisting')" />
              <span><b>{{ language.text('importDuplicatePolicySkip') }}</b><small>{{ language.text('importDuplicatePolicySkipHint') }}</small></span>
            </label>
            <label class="policy-option" [class.is-selected]="facade.duplicatePolicy() === 'UpdateMutableFields'">
              <input type="radio" name="duplicatePolicy" value="UpdateMutableFields" [checked]="facade.duplicatePolicy() === 'UpdateMutableFields'" (change)="onSelectPolicy('UpdateMutableFields')" />
              <span><b>{{ language.text('importDuplicatePolicyUpdate') }}</b><small>{{ language.text('importDuplicatePolicyUpdateHint') }}</small></span>
            </label>
          </div>
          <p class="boundary-note">{{ language.text('importMutableFieldsNote') }}</p>
        </div>

        <div class="panel-block">
          <h3>{{ language.text('importMode') }}</h3>
          <div class="policy-grid" role="radiogroup" [attr.aria-label]="language.text('importMode')">
            <label class="policy-option" [class.is-selected]="facade.importMode() === 'DryRun'">
              <input type="radio" name="importMode" value="DryRun" [checked]="facade.importMode() === 'DryRun'" (change)="onSelectMode('DryRun')" />
              <span><b>{{ language.text('importModeDryRun') }}</b><small>{{ language.text('importModeDryRunHint') }}</small></span>
            </label>
            <label class="policy-option" [class.is-selected]="facade.importMode() === 'Commit'">
              <input type="radio" name="importMode" value="Commit" [checked]="facade.importMode() === 'Commit'" (change)="onSelectMode('Commit')" />
              <span><b>{{ language.text('importModeCommit') }}</b><small>{{ language.text('importModeCommitHint') }}</small></span>
            </label>
          </div>
          <p class="boundary-note">{{ language.text('importModeCommitSimulationNote') }}</p>
        </div>

        <div class="wizard-actions">
          <span></span>
          <button class="button button--primary" type="button" (click)="goNext()" [disabled]="!canAdvance('resource')">{{ language.text('importNext') }} →</button>
        </div>
      </div>
    </ng-template>

    <!-- Step 2: File Upload -->
    <ng-template #fileStep>
      <div class="wizard-step">
        <h3>{{ language.text('importFileTitle') }}</h3>

        @if (!facade.fileMetadata()) {
          <div
            class="dropzone"
            role="button"
            tabindex="0"
            [class.is-dragover]="fileDragOver()"
            [attr.aria-label]="language.text('importFileDropHint')"
            (click)="fileInput.click()"
            (keydown.enter)="fileInput.click()"
            (keydown.space)="fileInput.click(); $event.preventDefault()"
            (dragover)="onDragOver($event)"
            (dragleave)="onDragLeave($event)"
            (drop)="onDrop($event)"
          >
            <p>{{ language.text('importFileDropHint') }}</p>
            <span class="button button--quiet">{{ language.text('importFileBrowse') }}</span>
          </div>
          <input #fileInput type="file" accept=".csv,.json" class="sr-only" (change)="onFileInputChange($event)" [attr.aria-label]="language.text('importFileBrowse')" />
        } @else {
          <div class="file-meta">
            <div>
              <b>{{ facade.fileMetadata()!.name }}</b>
              <small>{{ language.text('importFileSize') }}: {{ formatFileSize(facade.fileMetadata()!.size) }} · {{ language.text('importFileType') }}: {{ facade.fileMetadata()!.format.toUpperCase() }}</small>
            </div>
            <div class="file-meta__actions">
              <button class="button button--quiet" type="button" (click)="fileInput.click()">{{ language.text('importFileReplace') }}</button>
              <button class="button button--quiet" type="button" (click)="removeFile()">{{ language.text('importFileRemove') }}</button>
            </div>
          </div>
          <input #fileInput type="file" accept=".csv,.json" class="sr-only" (change)="onFileInputChange($event)" [attr.aria-label]="language.text('importFileBrowse')" />

          @if (facade.parseResult()?.valid) {
            <div class="inline-alert inline-alert--success" role="status">{{ language.text('importFileParsedOk') }} ({{ facade.parseResult()!.totalRows }} {{ language.text('importTotalRows') | lowercase }})</div>
          } @else if (facade.parseResult()) {
            <div class="inline-alert" role="alert">{{ facade.parseResult()!.error || language.text('importFileParseError') }}</div>
          }
        }

        @if (localFileError()) {
          <div class="inline-alert" role="alert">{{ localFileErrorMessage() }}</div>
        }

        <div class="wizard-actions">
          <button class="button button--quiet" type="button" (click)="goBack()">← {{ language.text('importBack') }}</button>
          <button class="button button--primary" type="button" (click)="goNext()" [disabled]="!canAdvance('file')">{{ language.text('importNext') }} →</button>
        </div>
      </div>
    </ng-template>

    <!-- Step 3: Column Mapping -->
    <ng-template #mappingStep>
      <div class="wizard-step">
        <h3>{{ language.text('importMappingTitle') }}</h3>
        <p class="muted-line">{{ language.text('importMappingLead') }}</p>

        @if (missingRequiredLabels().length > 0) {
          <div class="inline-alert" role="alert"><b>{{ language.text('importMappingMissingRequired') }}</b> {{ missingRequiredLabels().join(', ') }}</div>
        }
        @if (duplicateTargetLabels().length > 0) {
          <div class="inline-alert" role="alert"><b>{{ language.text('importMappingDuplicateTarget') }}</b> {{ duplicateTargetLabels().join(', ') }}</div>
        }

        <div class="section-heading section-heading--tight">
          <span></span>
          <button class="text-button" type="button" (click)="reapplyAutoMapping()">↻ {{ language.text('importMappingReapplyAuto') }}</button>
        </div>

        <app-data-grid [rows]="facade.columnMappings()" [columns]="mappingColumns" [rowActionsTemplate]="mappingActions" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importMappingTitle')" [countLabel]="language.text('importMappingTitle')">
          <ng-template #mappingActions let-mapping>
            <select [ngModel]="mapping.targetField ?? ''" (ngModelChange)="onMappingChange(mapping.sourceColumn, $event)" [ngModelOptions]="{standalone: true}" [attr.aria-label]="language.text('importMapsTo') + ' ' + mapping.sourceColumn">
              <option value="">{{ language.text('importMappingIgnoreOption') }}</option>
              @for (field of currentResourceFields(); track field.name) {
                <option [value]="field.name">{{ field.label }}{{ field.required ? ' *' : '' }}</option>
              }
            </select>
          </ng-template>
        </app-data-grid>

        <div class="wizard-actions">
          <button class="button button--quiet" type="button" (click)="goBack()">← {{ language.text('importBack') }}</button>
          <button class="button button--primary" type="button" (click)="goNext()" [disabled]="!canAdvance('mapping')">{{ language.text('importNext') }} →</button>
        </div>
      </div>
    </ng-template>

    <!-- Step 4: Preview -->
    <ng-template #previewStep>
      <div class="wizard-step">
        <h3>{{ language.text('importPreviewTitle') }}</h3>
        <p class="muted-line">{{ language.text('importPreviewLead') }}</p>
        <p class="boundary-note">{{ language.text('importPreviewStructuralNote') }} {{ language.text('importPreviewServerNote') }}</p>

        @if (previewRows().length === 0) {
          <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><b>{{ language.text('importPreviewNoData') }}</b></div>
        } @else {
          <p class="muted-line">{{ language.text('importPreviewShowing').replace('{shown}', previewRows().length.toString()).replace('{total}', previewTotal().toString()) }}</p>
          <app-data-grid [rows]="previewRows()" [columns]="previewGridColumns()" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importPreviewTitle')" [countLabel]="language.text('importPreviewTitle')" [emptyLabel]="language.text('importPreviewNoData')" />
        }

        <div class="wizard-actions">
          <button class="button button--quiet" type="button" (click)="goBack()">← {{ language.text('importBack') }}</button>
          <button class="button button--primary" type="button" (click)="goNext()" [disabled]="!canAdvance('preview')">{{ language.text('importNext') }} →</button>
        </div>
      </div>
    </ng-template>

    <!-- Step 5: Create & Simulate -->
    <ng-template #validateStep>
      <div class="wizard-step">
        <h3>{{ language.text('importCreateAndSimulate') }}</h3>
        <div class="summary-grid">
          <div><span>{{ language.text('importResourceType') }}</span><b>{{ resourceLabel(facade.selectedResourceKind()) }}</b></div>
          <div><span>{{ language.text('importFileName') }}</span><b>{{ facade.fileMetadata()?.name }}</b></div>
          <div><span>{{ language.text('importTotalRows') }}</span><b>{{ previewTotal() }}</b></div>
          <div><span>{{ language.text('importDuplicatePolicy') }}</span><b>{{ policyLabel(facade.duplicatePolicy()) }}</b></div>
          <div><span>{{ language.text('importMode') }}</span><b>{{ modeLabel(facade.importMode()) }}</b></div>
        </div>

        @if (simulatePhase() !== 'idle') {
          <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><b>{{ simulatePhase() === 'creating' ? language.text('importCreatingBatch') : language.text('importSimulating') }}</b></div>
        }

        <div class="wizard-actions">
          <button class="button button--quiet" type="button" (click)="goBack()" [disabled]="simulatePhase() !== 'idle'">← {{ language.text('importBack') }}</button>
          <button class="button button--primary" type="button" (click)="onCreateAndSimulate()" [disabled]="simulatePhase() !== 'idle'">{{ language.text('importCreateAndSimulate') }}</button>
        </div>
      </div>
    </ng-template>

    <!-- Step 6: Reconciliation & Execute -->
    <ng-template #executeStep>
      <div class="wizard-step">
        <ng-container *ngTemplateOutlet="batchOutcomePanel" />
        <div class="wizard-actions">
          <span></span>
          @if (isWizardTerminal()) {
            <button class="button button--primary" type="button" (click)="startNewImport()">{{ language.text('importStartNewImport') }}</button>
          }
        </div>
      </div>
    </ng-template>

    <!-- ==================== DETAIL (EXISTING BATCH) ==================== -->
    <ng-template #detailView>
      <section class="detail-view" aria-labelledby="import-detail-title">
        @if (facade.busy() && !facade.currentBatch()) {
          <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
        } @else if (facade.error() && !facade.currentBatch()) {
          <div class="state-card state-card--error" role="alert"><span class="state-icon" aria-hidden="true">!</span><div><b>{{ errorMessage(facade.error()) }}</b><button class="text-button" type="button" (click)="reloadDetail()">{{ language.text('retryLoad') || language.text('retry') }} ↗</button></div></div>
        } @else if (facade.currentBatch(); as batch) {
          <div class="detail-heading">
            <div>
              <p class="eyebrow eyebrow--soft">{{ language.text('importBatchDetailTitle') }}</p>
              <h2 id="import-detail-title">{{ resourceLabel(batch.resourceKind) }}</h2>
              <span class="status-pill" [class]="statusPillClass(batch.status)"><i aria-hidden="true"></i>{{ statusLabel(batch.status) }}</span>
            </div>
          </div>

          <nav class="tabs" role="tablist" [attr.aria-label]="language.text('importBatchDetailTitle')" (keydown)="onTabsKeydown($event)">
            @for (t of detailTabs; track t.tab) {
              <button
                [id]="'import-tab-' + t.tab"
                role="tab"
                type="button"
                [class.is-active]="detailTab() === t.tab"
                [attr.aria-selected]="detailTab() === t.tab"
                [attr.aria-controls]="'import-tabpanel-' + t.tab"
                [attr.tabindex]="detailTab() === t.tab ? 0 : -1"
                (click)="setDetailTab(t.tab)"
              >{{ language.text(t.labelKey) }}</button>
            }
          </nav>

          <div [id]="'import-tabpanel-' + detailTab()" role="tabpanel" [attr.aria-labelledby]="'import-tab-' + detailTab()" tabindex="0">
            @switch (detailTab()) {
              @case ('summary') {
                <div class="summary-grid">
                  <div><span>{{ language.text('importResourceType') }}</span><b>{{ resourceLabel(batch.resourceKind) }}</b></div>
                  <div><span>{{ language.text('importMode') }}</span><b>{{ modeLabel(batch.mode) }}</b></div>
                  <div><span>{{ language.text('importDuplicatePolicy') }}</span><b>{{ policyLabel(batch.duplicatePolicy) }}</b></div>
                  <div><span>{{ language.text('importBatchStatusCol') }}</span><b>{{ statusLabel(batch.status) }}</b></div>
                  <div><span>{{ language.text('importBatchSubmittedCol') }}</span><b>{{ batch.createdAt | date:'medium' }}</b></div>
                  <div><span>{{ language.text('importCorrelationId') }}</span><b>{{ batch.correlationId }}</b></div>
                  <div><span>{{ language.text('importBatchReference') }}</span><b>{{ batch.id }}</b></div>
                </div>
                @if (facade.canExecute()) {
                  <div class="wizard-actions">
                    <span></span>
                    <button class="button button--primary" type="button" (click)="openExecuteConfirm()" [disabled]="!facade.batchReconciliation() || facade.batchReconciliation()?.isConsistent === false">{{ language.text('importExecute') }}</button>
                  </div>
                }
              }
              @case ('rows') { <ng-container *ngTemplateOutlet="rowOutcomeTable" /> }
              @case ('reconciliation') { <ng-container *ngTemplateOutlet="reconciliationPanel" /> }
              @case ('audit') { <ng-container *ngTemplateOutlet="auditPanel" /> }
              @case ('evidence') { <ng-container *ngTemplateOutlet="evidencePanel" /> }
            }
          </div>
        }
      </section>
    </ng-template>

    <!-- Shared: full outcome panel used by wizard execute step -->
    <ng-template #batchOutcomePanel>
      @if (facade.currentBatch(); as batch) {
        <h3>{{ language.text('importReconciliationTitle') }}</h3>
        <ng-container *ngTemplateOutlet="reconciliationPanel" />

        @if (batch.status === 'Completed') {
          <div class="inline-alert inline-alert--success" role="status">{{ language.text('importExecutionCompleted') }}</div>
        } @else if (batch.status === 'CompletedWithErrors') {
          <div class="inline-alert inline-alert--warning" role="status">{{ language.text('importExecutionCompletedWithErrors') }}</div>
        } @else if (batch.status === 'Failed') {
          <div class="inline-alert" role="alert">{{ language.text('importExecutionFailedState') }}</div>
        } @else if (batch.status === 'Validated' && batch.mode === 'DryRun') {
          <div class="inline-alert inline-alert--info" role="status">{{ language.text('importDryRunNoCommitErrorMsg') }}</div>
        }

        @if (facade.canExecute()) {
          <div class="wizard-actions">
            <span></span>
            <button class="button button--primary" type="button" (click)="openExecuteConfirm()" [disabled]="!facade.batchReconciliation() || facade.batchReconciliation()?.isConsistent === false">{{ language.text('importExecute') }}</button>
          </div>
        }

        <ng-container *ngTemplateOutlet="rowOutcomeTable" />
      }
    </ng-template>

    <!-- Shared: reconciliation tiles -->
    <ng-template #reconciliationPanel>
      @if (facade.batchReconciliation(); as recon) {
        @if (recon.isConsistent) {
          <div class="inline-alert inline-alert--success" role="status">{{ language.text('importReconciliationConsistent') }}</div>
        } @else {
          <div class="inline-alert" role="alert"><b>{{ language.text('importReconciliationInconsistent') }}</b></div>
        }
        <div class="reconciliation-tiles">
          <div class="tile"><span>{{ language.text('importTotalRows') }}</span><b>{{ recon.totalRows }}</b></div>
          <div class="tile"><span>{{ language.text('importAccepted') }}</span><b>{{ recon.accepted }}</b></div>
          <div class="tile"><span>{{ language.text('importRejected') }}</span><b>{{ recon.rejected }}</b></div>
          <div class="tile"><span>{{ language.text('importQuarantined') }}</span><b>{{ recon.quarantined }}</b></div>
          <div class="tile"><span>{{ language.text('importCommitted') }}</span><b>{{ recon.committed }}</b></div>
          <div class="tile"><span>{{ language.text('importSkipped') }}</span><b>{{ recon.skipped }}</b></div>
          <div class="tile"><span>{{ language.text('importFailed') }}</span><b>{{ recon.failed }}</b></div>
        </div>
        <p class="muted-line">{{ recon.formula }}</p>
      }
    </ng-template>

    <!-- Shared: row outcome table with filters/search -->
    <ng-template #rowOutcomeTable>
      <div class="section-heading section-heading--tight">
        <h3>{{ language.text('importRowOutcomesTitle') }}</h3>
      </div>
      <div class="row-filters" role="group" [attr.aria-label]="language.text('importRowOutcomesTitle')">
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'all'" (click)="setRowFilter('all')">{{ language.text('importFilterAll') }}</button>
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'accepted'" (click)="setRowFilter('accepted')">{{ language.text('importFilterAccepted') }}</button>
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'rejected'" (click)="setRowFilter('rejected')">{{ language.text('importFilterRejected') }}</button>
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'quarantined'" (click)="setRowFilter('quarantined')">{{ language.text('importFilterQuarantined') }}</button>
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'warnings'" (click)="setRowFilter('warnings')">{{ language.text('importFilterWarnings') }}</button>
        <button type="button" class="filter-chip" [class.is-active]="rowFilter() === 'errors'" (click)="setRowFilter('errors')">{{ language.text('importFilterErrors') }}</button>
        <label class="search-field">
          <span class="sr-only">{{ language.text('importSearchRows') }}</span>
          <span class="search-field__icon" aria-hidden="true">⌕</span>
          <input type="search" [value]="rowSearch()" (input)="onRowSearch($event)" [placeholder]="language.text('importSearchRows')" />
        </label>
      </div>

      <app-data-grid [rows]="filteredRows()" [columns]="outcomeColumns" [rowActionsTemplate]="rowOutcomeActions" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importRowOutcomesTitle')" [countLabel]="language.text('importRowOutcomesTitle')" [emptyLabel]="language.text('importNoRowsMatchFilter')">
        <ng-template #rowOutcomeActions let-row>
          <button class="text-button" type="button" (click)="openRowDetail(row)">{{ language.text('importViewRowDetails') }} ↗</button>
        </ng-template>
      </app-data-grid>
    </ng-template>

    <!-- Shared: audit tab -->
    <ng-template #auditPanel>
      @if (facade.busy() && facade.batchAudit().length === 0) {
        <div class="state-card state-card--loading" role="status"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
      } @else if (facade.batchAudit().length === 0) {
        <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><b>{{ language.text('importAuditNoRecords') }}</b></div>
      } @else {
        <app-data-grid [rows]="facade.batchAudit()" [columns]="auditColumns" [rowIdFor]="auditRowId" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importEvidenceAuditSection')" [countLabel]="language.text('importEvidenceAuditSection')" [emptyLabel]="language.text('importAuditNoRecords')" />
      }
    </ng-template>

    <!-- Shared: evidence tab (Finding 2: consolidated evidence bundle) -->
    <ng-template #evidencePanel>
      @if (evidenceStatus() === 'loading') {
        <div class="state-card state-card--loading" role="status" aria-live="polite"><span class="loader" aria-hidden="true"></span><b>{{ language.text('loading') }}…</b></div>
      } @else if (evidenceStatus() === 'error') {
        <div class="state-card state-card--error" role="alert">
          <span class="state-icon" aria-hidden="true">!</span>
          <div>
            <b>{{ errorMessage(evidenceError()) }}</b>
            <button class="text-button" type="button" (click)="retryEvidenceLoad()">{{ language.text('retry') }} ↗</button>
          </div>
        </div>
      } @else if (evidenceStatus() === 'loaded' && facade.currentBatch(); as batch) {
        <h3>{{ language.text('importEvidenceBatchSection') }}</h3>
        <div class="summary-grid">
          <div><span>{{ language.text('importBatchReference') }}</span><b>{{ batch.id }}</b></div>
          <div><span>{{ language.text('importResourceType') }}</span><b>{{ resourceLabel(batch.resourceKind) }}</b></div>
          <div><span>{{ language.text('importMode') }}</span><b>{{ modeLabel(batch.mode) }}</b></div>
          <div><span>{{ language.text('importDuplicatePolicy') }}</span><b>{{ policyLabel(batch.duplicatePolicy) }}</b></div>
          <div><span>{{ language.text('importBatchStatusCol') }}</span><b>{{ statusLabel(batch.status) }}</b></div>
          <div><span>{{ language.text('importBatchSubmittedCol') }}</span><b>{{ batch.createdAt | date:'medium' }}</b></div>
          <div><span>{{ language.text('importCorrelationId') }}</span><b>{{ batch.correlationId }}</b></div>
          <div><span>{{ language.text('importSubmittedActor') }}</span><b>{{ batch.submittedActorId || '—' }}</b></div>
        </div>

        <h3>{{ language.text('importEvidenceSourceSection') }}</h3>
        <div class="summary-grid">
          <div><span>{{ language.text('importEvidenceSourceSystem') }}</span><b>{{ batch.source.sourceSystemCategory || '—' }}</b></div>
          <div><span>{{ language.text('importEvidenceSourceFile') }}</span><b>{{ batch.source.sourceFileReference || '—' }}</b></div>
          <div><span>{{ language.text('importBatchReference') }}</span><b>{{ batch.source.batchReference || '—' }}</b></div>
        </div>

        <h3>{{ language.text('importEvidenceReconciliationSection') }}</h3>
        <ng-container *ngTemplateOutlet="reconciliationPanel" />

        <h3>{{ language.text('importEvidenceRowSection') }}</h3>
        <ng-container *ngTemplateOutlet="evidenceRowTable" />

        <h3>{{ language.text('importEvidenceAuditSection') }}</h3>
        <ng-container *ngTemplateOutlet="auditPanel" />
      } @else {
        <div class="state-card state-card--error" role="alert">{{ language.text('importEvidenceUnavailable') }}</div>
      }
    </ng-template>

    <!-- Shared: evidence row-level table (Finding 2 section D) -->
    <ng-template #evidenceRowTable>
      @if (facade.batchRows().length === 0) {
        <div class="state-card state-card--empty"><span class="state-icon" aria-hidden="true">∅</span><b>{{ language.text('importNoRowsMatchFilter') }}</b></div>
      } @else {
        <app-data-grid [rows]="evidenceRowsSorted()" [columns]="evidenceColumns" [rowActionsTemplate]="evidenceRowActions" [language]="language.language()" [clientPaging]="true" [showPager]="false" [caption]="language.text('importEvidenceRowSection')" [countLabel]="language.text('importEvidenceRowSection')" [emptyLabel]="language.text('importNoRowsMatchFilter')">
          <ng-template #evidenceRowActions let-row>
            <button class="text-button" type="button" (click)="openRowDetail(row)">{{ language.text('importViewRowDetails') }} ↗</button>
          </ng-template>
        </app-data-grid>
      }
    </ng-template>

    <!-- Row detail / quarantine correction dialog -->
    @if (selectedRow(); as row) {
      <div class="dialog-backdrop" role="presentation" (click)="closeRowDetail()">
        <div class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="row-detail-title" (click)="$event.stopPropagation()" (keydown)="onRowDetailDialogKeydown($event)">
          <div class="dialog-panel__header">
            <h3 id="row-detail-title" tabindex="-1">{{ language.text('importRowDetailTitle') }} — {{ language.text('importPreviewRowNumber') }} {{ row.originalRowNumber }}</h3>
            <span class="badge" [class]="outcomeBadgeClass(row.outcome)">{{ outcomeIcon(row.outcome) }} {{ outcomeLabel(row.outcome) }}</span>
          </div>

          <div class="dialog-panel__body">
            @if (!row.isCurrent) {
              <p class="boundary-note">{{ language.text('importHistoricalRowNote') }}</p>
            }
            <h4>{{ language.text('importOriginalEvidence') }}</h4>
            <div class="field-read-grid">
              @for (key of objectKeys(originalEvidenceRow(row).sourceFields); track key) {
                <div><span>{{ key }}</span><b>{{ originalEvidenceRow(row).sourceFields[key] || '—' }}</b></div>
              }
            </div>

            @if (row.diagnostics.length === 0) {
              <p class="muted-line">{{ language.text('importNoRowErrors') }}</p>
            } @else {
              <h4>{{ language.text('importDiagnostics') }}</h4>
              <ul class="diagnostics-list">
                @for (diag of row.diagnostics; track diag.code) {
                  <li>
                    <span class="badge" [class]="severityBadgeClass(diag.severity)">{{ severityIcon(diag.severity) }} {{ severityLabel(diag.severity) }}</span>
                    <span>{{ diag.message }} <small>({{ diag.code }})</small></span>
                  </li>
                }
              </ul>
            }

            @if (row.outcome === 'Quarantined' && row.isCurrent) {
              <h4>{{ language.text('importCorrectedValues') }}</h4>
              <div class="form-grid">
                @for (key of replayFieldKeys(); track key) {
                  <label class="form-field"><span>{{ key }}</span><input [ngModel]="replayDraft()[key] ?? ''" (ngModelChange)="setReplayField(key, $event)" [name]="'replay-' + key" /></label>
                }
              </div>
              @if (facade.error()) { <div class="inline-alert" role="alert">{{ errorMessage(facade.error()) }}</div> }
              <div class="wizard-actions">
                <button class="button button--quiet" type="button" (click)="closeRowDetail()" [disabled]="replaySaving()">{{ language.text('importBack') }}</button>
                <button class="button button--primary" type="button" (click)="submitReplay()" [disabled]="replaySaving()">{{ replaySaving() ? language.text('importReplaying') : language.text('importCorrectAndReplay') }}</button>
              </div>
            } @else if (row.outcome === 'Rejected') {
              <p class="boundary-note">{{ language.text('importRejectedNoReplayNote') }}</p>
              <div class="wizard-actions"><span></span><button class="button button--quiet" type="button" (click)="closeRowDetail()">{{ language.text('importBack') }}</button></div>
            } @else {
              <div class="wizard-actions"><span></span><button class="button button--quiet" type="button" (click)="closeRowDetail()">{{ language.text('importBack') }}</button></div>
            }
          </div>
        </div>
      </div>
    }

    <!-- Execute confirmation dialog -->
    @if (executeConfirmOpen()) {
      <div class="dialog-backdrop" role="presentation" (click)="closeExecuteConfirm()">
        <div class="dialog-panel" role="dialog" aria-modal="true" aria-labelledby="execute-confirm-title" (click)="$event.stopPropagation()" (keydown)="onExecuteDialogKeydown($event)">
          <div class="dialog-panel__header"><h3 id="execute-confirm-title">{{ language.text('importExecuteConfirmTitle') }}</h3></div>
          <div class="dialog-panel__body">
            <p class="boundary-note">{{ language.text('importExecuteConfirmLead') }}</p>

            @if (facade.batchReconciliation(); as recon) {
              @if (facade.currentBatch(); as batch) {
                <div class="summary-grid">
                  <div><span>{{ language.text('importResourceType') }}</span><b>{{ resourceLabel(batch.resourceKind) }}</b></div>
                  <div><span>{{ language.text('importTotalRows') }}</span><b>{{ recon.totalRows }}</b></div>
                  <div><span>{{ language.text('importAccepted') }}</span><b>{{ recon.accepted }}</b></div>
                  <div><span>{{ language.text('importRejected') }}</span><b>{{ recon.rejected }}</b></div>
                  <div><span>{{ language.text('importQuarantined') }}</span><b>{{ recon.quarantined }}</b></div>
                  <div><span>{{ language.text('importDuplicatePolicy') }}</span><b>{{ policyLabel(batch.duplicatePolicy) }}</b></div>
                  <div><span>{{ language.text('importBatchReference') }}</span><b>{{ batch.id }}</b></div>
                </div>
              }
              @if (recon.rejected > 0 || recon.quarantined > 0) {
                <div class="inline-alert inline-alert--warning" role="alert"><b>{{ language.text('importExecutePartialEligibilityWarning') }}</b></div>
              }
            } @else {
              <div class="inline-alert" role="alert"><b>{{ language.text('importExecuteReconciliationMissing') }}</b></div>
            }

            @if (facade.error()) { <div class="inline-alert" role="alert">{{ errorMessage(facade.error()) }}</div> }

            <div class="wizard-actions">
              <button id="execute-confirm-cancel" class="button button--quiet" type="button" (click)="closeExecuteConfirm()" [disabled]="facade.busy()">{{ language.text('importBack') }}</button>
              <button class="button button--danger" type="button" (click)="confirmExecute()" [disabled]="facade.busy() || !facade.batchReconciliation()">{{ facade.busy() ? language.text('importExecuting') : language.text('importExecute') }}</button>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .back-link { border: 1px solid var(--line); padding: .5rem .8rem; border-radius: .5rem; color: var(--primary); background: var(--surface); font-weight: 700; text-decoration: none; }
    .workspace-panel { min-width: 0; margin-block-start: 1.2rem; border: 1px solid var(--line); border-radius: 1.15rem; background: var(--surface-raised); box-shadow: var(--shadow-soft); }
    .list-view, .wizard-view, .detail-view { padding: 1.4rem; }
    .section-heading { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-start; gap: 1rem; margin-block-end: 1rem; }
    .section-heading--tight { margin-block-end: .6rem; }
    .section-heading h2, .detail-heading h2 { margin: 0; color: var(--ink); font: 800 clamp(1.2rem, 3vw, 1.7rem)/1 var(--font-display); letter-spacing: -.04em; }
    .eyebrow-soft, .eyebrow--soft { color: var(--accent-strong); font: 800 .68rem/1 var(--font-sans); letter-spacing: .1em; text-transform: uppercase; }
    h3 { margin: 1.1rem 0 .5rem; color: var(--ink); font: 800 1rem/1.3 var(--font-display); }
    h4 { margin: 1rem 0 .4rem; color: var(--ink); font: 800 .8rem/1.3 var(--font-sans); }
    .muted-line { margin: 0 0 .6rem; color: var(--ink-muted); font-size: .78rem; line-height: 1.5; }
    .boundary-note { margin: .5rem 0; border-inline-start: 3px solid var(--accent); padding-inline-start: .7rem; color: var(--ink-muted); font-size: .72rem; line-height: 1.5; }

    .button { display: inline-flex; align-items: center; gap: .4rem; border: 1px solid transparent; border-radius: .6rem; padding: .6rem 1rem; font: 800 .78rem/1 var(--font-sans); cursor: pointer; }
    .button--primary { color: #173b35; background: var(--accent); }
    .button--quiet { border-color: var(--line); color: var(--ink-muted); background: transparent; }
    .button--quiet:hover:not(:disabled) { border-color: var(--line-strong); color: var(--ink); background: var(--canvas); }
    .button--danger { color: #fff; background: var(--danger); }
    .button:disabled { cursor: not-allowed; opacity: .55; }
    .text-button { border: 0; padding: 0; color: var(--accent-strong); background: transparent; font: 800 .74rem/1 var(--font-sans); cursor: pointer; }

    .state-card { display: flex; align-items: flex-start; gap: .8rem; border: 1px dashed var(--line-strong); border-radius: .8rem; padding: 1.35rem; background: var(--canvas); }
    .state-card b { color: var(--ink); font-size: .85rem; }
    .state-card--error { border-style: solid; border-color: color-mix(in srgb, var(--danger) 35%, var(--line)); background: color-mix(in srgb, var(--danger) 5%, var(--surface-raised)); }
    .state-card--empty { min-height: 6rem; align-items: center; }
    .state-icon { display: grid; flex: 0 0 1.8rem; place-items: center; width: 1.8rem; height: 1.8rem; border-radius: .5rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); font-weight: 900; }
    .state-card--empty .state-icon { color: var(--accent-strong); background: var(--accent-soft); }
    .loader { width: 1.2rem; height: 1.2rem; border: 2px solid var(--line); border-top-color: var(--accent-strong); border-radius: 50%; animation: spin .8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }

    .inline-alert { margin: .8rem 0; border-radius: .55rem; padding: .65rem .8rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .78rem; line-height: 1.5; }
    .inline-alert--success { color: var(--success); background: var(--accent-soft); }
    .inline-alert--warning { color: #8a5a00; background: color-mix(in srgb, #d99a00 14%, var(--surface-raised)); }
    .inline-alert--info { color: var(--ink-muted); background: var(--canvas); border: 1px solid var(--line); }

    .stepper { display: flex; flex-wrap: wrap; gap: .5rem; margin: 0 0 1.2rem; padding: 0; list-style: none; }
    .stepper li { flex: 1 1 8rem; }
    .stepper__item { display: flex; align-items: center; gap: .5rem; width: 100%; border: 1px solid var(--line); border-radius: .7rem; padding: .55rem .7rem; color: var(--ink-muted); background: var(--canvas); cursor: pointer; text-align: start; }
    .stepper__item:disabled { cursor: not-allowed; opacity: .6; }
    .stepper__item.is-current { border-color: var(--accent-strong); background: var(--accent-soft); color: var(--ink); }
    .stepper__item.is-complete { border-color: var(--line-strong); color: var(--accent-strong); }
    .stepper__mark { display: grid; flex: 0 0 1.5rem; place-items: center; width: 1.5rem; height: 1.5rem; border-radius: 50%; background: var(--surface-raised); font: 800 .68rem/1 ui-monospace, monospace; }
    .stepper__label { display: flex; flex-direction: column; font-size: .72rem; font-weight: 800; }
    .stepper__label small { color: var(--ink-muted); font-size: .6rem; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; }

    .wizard-step { padding-block-start: .4rem; }
    .wizard-actions { display: flex; justify-content: space-between; align-items: center; margin-block-start: 1.2rem; padding-block-start: 1rem; border-block-start: 1px solid var(--line); }

    .resource-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr)); gap: .6rem; }
    .resource-card { border: 1px solid var(--line); border-radius: .7rem; padding: .8rem; color: var(--ink); background: var(--canvas); font-weight: 700; font-size: .78rem; cursor: pointer; text-align: start; }
    .resource-card.is-selected { border-color: var(--accent-strong); background: var(--accent-soft); box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent-strong) 30%, transparent); }

    .panel-block { margin-block-start: 1rem; border-block-start: 1px solid var(--line); padding-block-start: 1rem; }
    .policy-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); gap: .6rem; }
    .policy-option { display: flex; align-items: flex-start; gap: .55rem; border: 1px solid var(--line); border-radius: .7rem; padding: .7rem .8rem; background: var(--canvas); cursor: pointer; }
    .policy-option.is-selected { border-color: var(--accent-strong); background: var(--accent-soft); }
    .policy-option input { margin-block-start: .2rem; accent-color: var(--accent-strong); }
    .policy-option span { display: flex; flex-direction: column; gap: .2rem; }
    .policy-option b { color: var(--ink); font-size: .78rem; }
    .policy-option small { color: var(--ink-muted); font-size: .7rem; line-height: 1.4; }

    .dropzone { display: grid; place-items: center; gap: .6rem; min-height: 9rem; border: 2px dashed var(--line-strong); border-radius: .9rem; padding: 1.5rem; color: var(--ink-muted); background: var(--canvas); cursor: pointer; text-align: center; }
    .dropzone.is-dragover { border-color: var(--accent-strong); background: var(--accent-soft); color: var(--ink); }
    .file-meta { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: .8rem; border: 1px solid var(--line); border-radius: .8rem; padding: .85rem 1rem; background: var(--canvas); }
    .file-meta b { display: block; color: var(--ink); font-size: .82rem; }
    .file-meta small { color: var(--ink-muted); font-size: .68rem; }
    .file-meta__actions { display: flex; gap: .5rem; }

    .form-field select { width: 100%; border: 1px solid var(--line); border-radius: .45rem; padding: .5rem .6rem; color: var(--ink); background: var(--surface-raised); font-size: .76rem; }
    .mapping-badges { display: flex; flex-wrap: wrap; gap: .3rem; }

    .badge { display: inline-flex; align-items: center; gap: .3rem; border-radius: 99px; padding: .25rem .55rem; font-size: .64rem; font-weight: 800; white-space: nowrap; }
    .badge--success { color: var(--success); background: var(--accent-soft); }
    .badge--warning { color: #8a5a00; background: color-mix(in srgb, #d99a00 16%, var(--surface-raised)); }
    .badge--danger { color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); }
    .badge--info { color: var(--accent-strong); background: var(--accent-soft); }
    .badge--neutral { color: var(--ink-muted); background: var(--canvas); border: 1px solid var(--line); }

    .status-pill { display: inline-flex; align-items: center; gap: .35rem; border-radius: 99px; padding: .3rem .5rem; color: var(--success); background: var(--accent-soft); font-size: .66rem; font-weight: 800; white-space: nowrap; }
    .status-pill i { width: .38rem; height: .38rem; border-radius: 50%; background: currentColor; }
    .status-pill--warning { color: #8a5a00; background: color-mix(in srgb, #d99a00 16%, var(--surface-raised)); }
    .status-pill--danger { color: var(--danger); background: color-mix(in srgb, var(--danger) 12%, var(--surface-raised)); }
    .status-pill--neutral { color: var(--ink-muted); background: var(--canvas); }

    .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr)); gap: .8rem; margin-block-end: 1rem; }
    .summary-grid > div { border-block-start: 2px solid var(--line); padding-block-start: .45rem; }
    .summary-grid span { display: block; color: var(--ink-muted); font-size: .64rem; font-weight: 800; text-transform: uppercase; letter-spacing: .05em; }
    .summary-grid b { display: block; margin-block-start: .25rem; overflow-wrap: anywhere; color: var(--ink); font-size: .8rem; }

    .reconciliation-tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(7rem, 1fr)); gap: .6rem; margin-block: .8rem; }
    .tile { border: 1px solid var(--line); border-radius: .7rem; padding: .7rem; background: var(--canvas); text-align: center; }
    .tile span { display: block; color: var(--ink-muted); font-size: .62rem; font-weight: 800; text-transform: uppercase; letter-spacing: .05em; }
    .tile b { display: block; margin-block-start: .3rem; color: var(--ink); font: 800 1.2rem/1 var(--font-display); }

    .row-filters { display: flex; flex-wrap: wrap; align-items: center; gap: .4rem; margin-block-end: .8rem; }
    .filter-chip { border: 1px solid var(--line); border-radius: 99px; padding: .35rem .7rem; color: var(--ink-muted); background: transparent; font: 800 .68rem/1 var(--font-sans); cursor: pointer; }
    .filter-chip.is-active { border-color: var(--accent-strong); color: var(--ink); background: var(--accent-soft); }
    .search-field { display: flex; align-items: center; flex: 1 1 12rem; gap: .5rem; border: 1px solid var(--line); border-radius: .55rem; padding-inline: .7rem; background: var(--canvas); }
    .search-field:focus-within { border-color: var(--focus); box-shadow: 0 0 0 3px rgb(13 138 131 / 12%); }
    .search-field input { min-width: 0; width: 100%; border: 0; outline: 0; padding-block: .5rem; color: var(--ink); background: transparent; font-size: .78rem; }

    .record-code { display: block; border: 0; padding: 0; color: var(--accent-strong); background: none; font: 800 .82rem/1.2 ui-monospace, monospace; cursor: pointer; text-align: start; }

    .tabs { display: flex; gap: .2rem; margin-block-end: 1.1rem; border-block-end: 1px solid var(--line); overflow-x: auto; }
    .tabs button { border: 0; border-block-end: 2px solid transparent; padding: .65rem .2rem; margin-inline-end: 1.2rem; color: var(--ink-muted); background: transparent; font: 800 .78rem/1 var(--font-sans); cursor: pointer; white-space: nowrap; }
    .tabs button.is-active { color: var(--accent-strong); border-block-end-color: var(--accent-strong); }

    .field-read-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .6rem; margin-block-end: .8rem; }
    .field-read-grid > div { min-width: 0; border-block-start: 2px solid var(--line); padding-block-start: .4rem; }
    .field-read-grid span { display: block; color: var(--ink-muted); font-size: .64rem; font-weight: 700; }
    .field-read-grid b { display: block; margin-block-start: .25rem; overflow-wrap: anywhere; color: var(--ink); font-size: .78rem; }

    .diagnostics-list { display: grid; gap: .5rem; margin: 0 0 .8rem; padding: 0; list-style: none; }
    .diagnostics-list li { display: flex; align-items: flex-start; gap: .5rem; font-size: .76rem; color: var(--ink); }
    .diagnostics-list small { color: var(--ink-muted); }

    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); gap: .7rem; margin-block-end: .8rem; }
    .form-field { display: flex; flex-direction: column; gap: .3rem; }
    .form-field > span { color: var(--ink-muted); font-size: .68rem; font-weight: 800; }
    .form-field input { width: 100%; border: 1px solid var(--line); border-radius: .45rem; padding: .55rem .65rem; color: var(--ink); background: var(--surface-raised); font-size: .78rem; }
    .form-field input:focus { border-color: var(--focus); outline: 0; box-shadow: 0 0 0 3px rgb(13 138 131 / 10%); }

    .dialog-backdrop { display: grid; position: fixed; z-index: 5; inset: 0; place-items: center; padding: 1rem; background: rgb(16 39 37 / 48%); }
    .dialog-panel { width: min(38rem, 100%); max-height: 88vh; overflow-y: auto; border-radius: 1rem; background: var(--surface-raised); box-shadow: var(--shadow-card); }
    .dialog-panel__header { display: flex; justify-content: space-between; align-items: center; gap: .8rem; padding: 1rem 1.2rem; border-block-end: 1px solid var(--line); }
    .dialog-panel__header h3 { margin: 0; }
    .dialog-panel__header h3:focus { outline: 0; }
    .dialog-panel__body { padding: 1rem 1.2rem 1.2rem; }
    .dialog-panel :focus-visible, .tabs button:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }


    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }

    @media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; } }
    @media (max-width: 720px) {
      .stepper li { flex-basis: 45%; }
    }
    @media (max-width: 460px) {
      .list-view, .wizard-view, .detail-view { padding: .8rem; }
      .wizard-actions { flex-wrap: wrap; gap: .6rem; }
      .stepper li { flex-basis: 100%; }
    }
  `,
})
export class MasterDataImportWorkspaceComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly language = inject(LanguageService);
  readonly facade = inject(MasterDataImportFacade);

  readonly resourceDefinitions = IMPORT_RESOURCE_DEFINITIONS;
  readonly wizardSteps = WIZARD_STEPS;

  readonly viewMode = signal<ImportViewMode>('list');
  readonly wizardStep = signal<ImportWizardStep>('resource');
  readonly detailTab = signal<ImportDetailTab>('summary');

  readonly fileDragOver = signal(false);
  readonly localFileError = signal<LocalFileError>(null);
  readonly autoMappingSnapshot = signal<ImportColumnMapping[]>([]);
  readonly simulatePhase = signal<SimulatePhase>('idle');

  readonly rowFilter = signal<RowFilter>('all');
  readonly rowSearch = signal('');

  readonly selectedRow = signal<MasterDataImportRowResponse | null>(null);
  readonly replayDraft = signal<Record<string, string | null>>({});
  readonly replaySaving = signal(false);

  readonly executeConfirmOpen = signal(false);
  readonly auditLoadedForBatch = signal<string | null>(null);
  readonly evidenceLoadedForBatch = signal<string | null>(null);
  readonly evidenceStatus = signal<'idle' | 'loading' | 'loaded' | 'error'>('idle');
  readonly evidenceError = signal<SafeUiError | null>(null);

  private lastFocusedElement: HTMLElement | null = null;

  readonly detailTabs: { tab: ImportDetailTab; labelKey: TranslationKey }[] = [
    { tab: 'summary', labelKey: 'importBatchSummaryTab' },
    { tab: 'rows', labelKey: 'importBatchRowsTab' },
    { tab: 'reconciliation', labelKey: 'importBatchReconciliationTab' },
    { tab: 'audit', labelKey: 'importBatchAuditTab' },
    { tab: 'evidence', labelKey: 'importBatchEvidenceTab' },
  ];

  readonly batchColumns: DataGridColumn<MasterDataImportBatchResponse>[] = [
    { key: 'resourceKind', label: this.language.text('importBatchResourceCol'), value: row => this.resourceLabel(row.resourceKind), filter: 'select' },
    { key: 'mode', label: this.language.text('importBatchModeCol'), value: row => this.modeLabel(row.mode), filter: 'select' },
    { key: 'duplicatePolicy', label: this.language.text('importBatchPolicyCol'), value: row => this.policyLabel(row.duplicatePolicy), filter: 'select' },
    { key: 'status', label: this.language.text('importBatchStatusCol'), value: row => this.statusLabel(row.status), badge: true, filter: 'select' },
    { key: 'createdAt', label: this.language.text('importBatchSubmittedCol'), value: row => row.createdAt, display: row => new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(row.createdAt)), filter: 'date-range' },
    { key: 'acceptedCount', label: this.language.text('importBatchTotalsCol'), value: row => row.acceptedCount, display: row => `${row.acceptedCount}/${row.totalRows} ${this.language.text('importAccepted').toLowerCase()}`, filter: 'number-range', align: 'end' },
  ];
  readonly mappingColumns: DataGridColumn<ImportColumnMapping>[] = [
    { key: 'sourceColumn', label: this.language.text('importSourceColumn'), value: row => row.sourceColumn, filter: 'text' },
    { key: 'targetField', label: this.language.text('importMapsTo'), value: row => this.fieldForMapping(row)?.label ?? this.language.text('importMappingIgnoreOption'), filter: 'select' },
    { key: 'mappingStatus', label: this.language.text('importMappingStatus'), value: row => this.mappingStatus(row), badge: true, filter: 'select' },
  ];
  readonly previewGridColumns = computed<DataGridColumn<MasterDataImportRowInput>[]>(() => [
    { key: 'rowNumber', label: this.language.text('importPreviewRowNumber'), value: row => row.rowNumber, filter: 'number-range' },
    ...this.previewColumns().map(field => ({ key: field.name, label: field.label, value: (row: MasterDataImportRowInput) => this.previewFieldValue(row, field), filter: 'text' as const })),
  ]);
  readonly outcomeColumns: DataGridColumn<MasterDataImportRowResponse>[] = [
    { key: 'originalRowNumber', label: this.language.text('importPreviewRowNumber'), value: row => row.originalRowNumber, display: row => `${row.originalRowNumber}${row.replaySequence > 0 ? ` · ${this.language.text('importReplaySequence')}${row.replaySequence}` : ''}`, filter: 'number-range' },
    { key: 'outcome', label: this.language.text('importRowOutcomesTitle'), value: row => this.outcomeLabel(row.outcome), badge: true, filter: 'select' },
    { key: 'highestSeverity', label: this.language.text('importSeverity'), value: row => this.severityLabel(row.highestSeverity), badge: true, filter: 'select' },
    { key: 'resultingResourceCode', label: this.language.text('importResultingRecord'), value: row => row.resultingResourceCode ?? '—', filter: 'text' },
  ];
  readonly auditColumns: DataGridColumn<MasterDataImportAuditResponse>[] = [
    { key: 'operationId', label: this.language.text('importAuditOperation'), value: row => row.operationId, secondaryText: row => row.detail ?? '', filter: 'text' },
    { key: 'actorId', label: this.language.text('importAuditActor'), value: row => row.actorId, filter: 'text' },
    { key: 'occurredAt', label: this.language.text('importAuditTimestamp'), value: row => row.occurredAt, display: row => new Intl.DateTimeFormat(this.language.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(row.occurredAt)), filter: 'date-range' },
    { key: 'correlationId', label: this.language.text('importAuditCorrelation'), value: row => row.correlationId, filter: 'text' },
    { key: 'outcome', label: this.language.text('importAuditOutcome'), value: row => row.outcome, badge: true, filter: 'select' },
  ];
  readonly evidenceColumns: DataGridColumn<MasterDataImportRowResponse>[] = [
    this.outcomeColumns[0],
    this.outcomeColumns[1],
    { key: 'mutationDisposition', label: this.language.text('importEvidenceMutationDisposition'), value: row => this.dispositionLabel(row.mutationDisposition), filter: 'select' },
    { key: 'resultingResourceCode', label: this.language.text('importResultingRecord'), value: row => row.resultingResourceCode ?? '—', filter: 'text' },
    { key: 'diagnostics', label: this.language.text('importDiagnostics'), value: row => row.diagnostics.length, secondaryText: row => this.severityLabel(row.highestSeverity), filter: 'number-range' },
    { key: 'lineage', label: this.language.text('importEvidenceLineage'), value: row => row.originalRowId ? this.language.text('importReplayLineageReplay') : this.language.text('importReplayLineageOriginal'), secondaryText: row => row.isCurrent ? '' : this.language.text('importEvidenceHistorical'), filter: 'select' },
  ];
  readonly auditRowId = (row: MasterDataImportAuditResponse): string => row.evidenceId;

  readonly evidenceRowsSorted = computed<MasterDataImportRowResponse[]>(() =>
    [...this.facade.batchRows()].sort((a, b) =>
      a.originalRowNumber !== b.originalRowNumber
        ? a.originalRowNumber - b.originalRowNumber
        : a.replaySequence - b.replaySequence,
    ),
  );

  readonly currentResourceFields = computed<ImportFieldDefinition[]>(() => {
    const kind = this.facade.selectedResourceKind();
    return kind ? (getImportResourceDefinition(kind)?.fields ?? []) : [];
  });

  readonly missingRequiredLabels = computed<string[]>(() => {
    const validation = this.facade.mappingValidation();
    const kind = this.facade.selectedResourceKind();
    if (!validation || !kind) return [];
    const def = getImportResourceDefinition(kind);
    return validation.missingRequiredFields.map((name) => def?.fields.find((f) => f.name === name)?.label ?? name);
  });

  readonly duplicateTargetLabels = computed<string[]>(() => {
    const validation = this.facade.mappingValidation();
    const kind = this.facade.selectedResourceKind();
    if (!validation || !kind) return [];
    const def = getImportResourceDefinition(kind);
    return validation.duplicateMappings.map((name) => def?.fields.find((f) => f.name === name)?.label ?? name);
  });

  readonly previewColumns = computed<ImportFieldDefinition[]>(() => {
    const kind = this.facade.selectedResourceKind();
    if (!kind) return [];
    const mappedTargets = new Set(this.facade.columnMappings().filter((m) => m.targetField).map((m) => m.targetField));
    return (getImportResourceDefinition(kind)?.fields ?? []).filter((f) => mappedTargets.has(f.name));
  });

  readonly previewRows = computed<MasterDataImportRowInput[]>(() => this.facade.normalizedRows().slice(0, 50));
  readonly previewTotal = computed<number>(() => this.facade.normalizedRows().length);

  readonly filteredRows = computed<MasterDataImportRowResponse[]>(() => {
    let rows = this.facade.batchRows().filter((r) => r.isCurrent);
    const filter = this.rowFilter();
    if (filter === 'accepted') rows = rows.filter((r) => r.outcome === 'Accepted');
    else if (filter === 'rejected') rows = rows.filter((r) => r.outcome === 'Rejected');
    else if (filter === 'quarantined') rows = rows.filter((r) => r.outcome === 'Quarantined');
    else if (filter === 'warnings') rows = rows.filter((r) => r.highestSeverity === 'Warning');
    else if (filter === 'errors') rows = rows.filter((r) => r.highestSeverity === 'Error');

    const search = this.rowSearch().trim().toLowerCase();
    if (search) {
      rows = rows.filter(
        (r) =>
          String(r.originalRowNumber).includes(search) ||
          (r.resultingResourceCode ?? '').toLowerCase().includes(search) ||
          r.diagnostics.some((d) => d.message.toLowerCase().includes(search) || d.code.toLowerCase().includes(search)),
      );
    }
    return [...rows].sort((a, b) => a.originalRowNumber - b.originalRowNumber);
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      this.selectedRow.set(null);
      this.executeConfirmOpen.set(false);

      if (!id) {
        this.viewMode.set('list');
        this.facade.reset();
        void this.facade.refreshBatchList();
        return;
      }

      if (id === 'new') {
        this.viewMode.set('wizard');
        this.facade.reset();
        this.wizardStep.set('resource');
        this.localFileError.set(null);
        this.autoMappingSnapshot.set([]);
        this.simulatePhase.set('idle');
        return;
      }

      this.viewMode.set('detail');
      this.detailTab.set('summary');
      this.auditLoadedForBatch.set(null);
      this.evidenceLoadedForBatch.set(null);
      this.evidenceStatus.set('idle');
      this.evidenceError.set(null);
      void this.facade.loadBatch(id);
    });
  }

  canMutate(): boolean {
    return this.auth.status() === 'authenticated' && this.auth.session()?.selectedContextId !== null && this.auth.session()?.selectedContextId !== undefined;
  }

  openNewImport(): void {
    void this.router.navigate(['/app/master-data/imports/new']);
  }

  openBatch(id: string): void {
    void this.router.navigate(['/app/master-data/imports', id]);
  }

  startNewImport(): void {
    void this.router.navigate(['/app/master-data/imports/new']);
  }

  reloadDetail(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') void this.facade.loadBatch(id);
  }

  resetWizard(): void {
    this.facade.reset();
    this.wizardStep.set('resource');
    this.localFileError.set(null);
    this.autoMappingSnapshot.set([]);
    this.simulatePhase.set('idle');
  }

  // ---- Wizard navigation ----
  canAdvance(step: ImportWizardStep): boolean {
    switch (step) {
      case 'resource':
        return this.facade.selectedResourceKind() !== null;
      case 'file':
        return this.facade.parseResult()?.valid === true;
      case 'mapping':
        return this.facade.mappingValidation()?.valid === true;
      case 'preview':
        return this.facade.normalizedRows().length > 0;
      default:
        return false;
    }
  }

  goNext(): void {
    const step = this.wizardStep();
    if (!this.canAdvance(step)) return;
    const idx = WIZARD_STEPS.indexOf(step);
    if (idx >= 0 && idx < WIZARD_STEPS.length - 1) {
      this.wizardStep.set(WIZARD_STEPS[idx + 1]);
    }
  }

  goBack(): void {
    const idx = WIZARD_STEPS.indexOf(this.wizardStep());
    if (idx > 0) this.wizardStep.set(WIZARD_STEPS[idx - 1]);
  }

  goToStep(step: ImportWizardStep): void {
    const targetIdx = WIZARD_STEPS.indexOf(step);
    const currentIdx = WIZARD_STEPS.indexOf(this.wizardStep());
    if (targetIdx <= currentIdx) this.wizardStep.set(step);
  }

  stepStatus(step: ImportWizardStep): 'complete' | 'current' | 'pending' {
    const idx = WIZARD_STEPS.indexOf(step);
    const currentIdx = WIZARD_STEPS.indexOf(this.wizardStep());
    if (idx < currentIdx) return 'complete';
    if (idx === currentIdx) return 'current';
    return 'pending';
  }

  stepLabel(step: ImportWizardStep): string {
    switch (step) {
      case 'resource':
        return this.language.text('importStepResource');
      case 'file':
        return this.language.text('importStepFile');
      case 'mapping':
        return this.language.text('importStepMapping');
      case 'preview':
        return this.language.text('importStepPreview');
      case 'validate':
        return this.language.text('importStepValidate');
      case 'execute':
        return this.language.text('importStepExecute');
    }
  }

  stepStatusLabel(status: 'complete' | 'current' | 'pending'): string {
    if (status === 'complete') return this.language.text('stepStatusComplete');
    if (status === 'current') return this.language.text('stepStatusCurrent');
    return this.language.text('stepStatusPending');
  }

  isWizardTerminal(): boolean {
    const batch = this.facade.currentBatch();
    if (!batch) return false;
    if (batch.status === 'Completed' || batch.status === 'CompletedWithErrors' || batch.status === 'Failed') return true;
    return batch.status === 'Validated' && batch.mode === 'DryRun';
  }

  // ---- Step 1: Resource & policy ----
  onSelectResource(kind: MasterDataImportResourceKind): void {
    this.facade.selectResource(kind);
    this.autoMappingSnapshot.set(this.facade.columnMappings().map((m) => ({ ...m })));
  }

  onSelectPolicy(policy: MasterDataImportDuplicatePolicy): void {
    this.facade.setDuplicatePolicy(policy);
  }

  onSelectMode(mode: MasterDataImportMode): void {
    this.facade.setImportMode(mode);
  }

  // ---- Step 2: File upload ----
  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.handleFile(file);
    input.value = '';
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.fileDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.fileDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.fileDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.handleFile(file);
  }

  private handleFile(file: File): void {
    this.localFileError.set(null);
    const lowerName = file.name.toLowerCase();
    if (!lowerName.endsWith('.csv') && !lowerName.endsWith('.json')) {
      this.localFileError.set('unsupported');
      return;
    }
    if (file.size === 0) {
      this.localFileError.set('empty');
      return;
    }
    if (file.size > MAX_FILE_SIZE_BYTES) {
      this.localFileError.set('tooLarge');
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const content = typeof reader.result === 'string' ? reader.result : '';
      this.facade.loadFileContent(content, file.name, file.size);
      this.autoMappingSnapshot.set(this.facade.columnMappings().map((m) => ({ ...m })));
    };
    reader.onerror = () => this.localFileError.set('parse');
    reader.readAsText(file);
  }

  removeFile(): void {
    this.facade.removeFile();
    this.localFileError.set(null);
    this.autoMappingSnapshot.set([]);
  }

  localFileErrorMessage(): string {
    switch (this.localFileError()) {
      case 'unsupported':
        return this.language.text('importFileUnsupportedType');
      case 'tooLarge':
        return this.language.text('importFileTooLarge');
      case 'empty':
        return this.language.text('importFileEmpty');
      case 'parse':
        return this.language.text('importFileParseError');
      default:
        return '';
    }
  }

  // ---- Step 3: Mapping ----
  onMappingChange(sourceColumn: string, value: string): void {
    this.facade.updateMapping(sourceColumn, value === '' ? null : value);
  }

  mappingOrigin(mapping: ImportColumnMapping): 'auto' | 'manual' | 'ignored' {
    if (mapping.targetField === null) return 'ignored';
    const snapshotEntry = this.autoMappingSnapshot().find((m) => m.sourceColumn === mapping.sourceColumn);
    return snapshotEntry && snapshotEntry.targetField === mapping.targetField ? 'auto' : 'manual';
  }

  fieldForMapping(mapping: ImportColumnMapping): ImportFieldDefinition | undefined {
    if (!mapping.targetField) return undefined;
    return this.currentResourceFields().find((f) => f.name === mapping.targetField);
  }

  mappingStatus(mapping: ImportColumnMapping): string {
    const origin = this.mappingOrigin(mapping);
    if (origin === 'ignored') return this.language.text('importMappingIgnoredBadge');
    const field = this.fieldForMapping(mapping);
    const requirement = field
      ? this.language.text(field.required ? 'importMappingRequiredBadge' : 'importMappingOptionalBadge')
      : this.language.text('importMappingNeedsMapping');
    return origin === 'auto' ? `${this.language.text('importMappingAutoMatched')} · ${requirement}` : requirement;
  }

  reapplyAutoMapping(): void {
    this.facade.applyAutoMapping();
    this.autoMappingSnapshot.set(this.facade.columnMappings().map((m) => ({ ...m })));
  }

  // ---- Step 4: Preview ----
  previewFieldValue(row: MasterDataImportRowInput, field: ImportFieldDefinition): string {
    return row.fields?.[field.name.toLowerCase()] ?? '';
  }

  // ---- Step 5: Create & simulate ----
  async onCreateAndSimulate(): Promise<void> {
    this.simulatePhase.set('creating');
    const batch = await this.facade.createBatch(this.facade.fileMetadata()?.name);
    if (!batch) {
      this.simulatePhase.set('idle');
      return;
    }
    this.simulatePhase.set('simulating');
    const simulated = await this.facade.simulateBatch();
    this.simulatePhase.set('idle');
    if (simulated) this.wizardStep.set('execute');
  }

  // ---- Step 6: Execute ----
  openExecuteConfirm(): void {
    if (!this.facade.canExecute()) return;
    if (!this.facade.batchReconciliation()) return;
    if (this.facade.batchReconciliation()?.isConsistent === false) return;
    this.lastFocusedElement = document.activeElement as HTMLElement | null;
    this.executeConfirmOpen.set(true);
    setTimeout(() => document.getElementById('execute-confirm-cancel')?.focus(), 0);
  }

  closeExecuteConfirm(): void {
    if (this.facade.busy()) return;
    this.executeConfirmOpen.set(false);
    this.restoreFocusToOpener();
  }

  async confirmExecute(): Promise<void> {
    await this.facade.executeBatch();
    this.executeConfirmOpen.set(false);
    this.restoreFocusToOpener();
  }

  onExecuteDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.trapTabKey(event, event.currentTarget as HTMLElement);
      return;
    }
    if (event.key === 'Escape') {
      if (this.facade.busy()) return;
      event.preventDefault();
      this.closeExecuteConfirm();
    }
  }

  // ---- Row outcomes / filters ----
  setRowFilter(filter: RowFilter): void {
    this.rowFilter.set(filter);
  }

  onRowSearch(event: Event): void {
    this.rowSearch.set((event.target as HTMLInputElement).value);
  }

  // ---- Row detail / quarantine replay ----
  openRowDetail(row: MasterDataImportRowResponse): void {
    this.lastFocusedElement = document.activeElement as HTMLElement | null;
    this.selectedRow.set(row);
    this.replayDraft.set(row.outcome === 'Quarantined' && row.isCurrent ? { ...row.normalizedFields } : {});
    setTimeout(() => document.getElementById('row-detail-title')?.focus(), 0);
  }

  closeRowDetail(): void {
    if (this.replaySaving()) return;
    this.selectedRow.set(null);
    this.replayDraft.set({});
    this.restoreFocusToOpener();
  }

  onRowDetailDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Tab') {
      this.trapTabKey(event, event.currentTarget as HTMLElement);
      return;
    }
    if (event.key === 'Escape') {
      if (this.replaySaving()) return;
      event.preventDefault();
      this.closeRowDetail();
    }
  }

  setReplayField(key: string, value: string): void {
    this.replayDraft.update((draft) => ({ ...draft, [key]: value.length > 0 ? value : null }));
  }

  replayFieldKeys(): string[] {
    const row = this.selectedRow();
    return row ? Object.keys(row.normalizedFields ?? {}) : [];
  }

  originalEvidenceRow(row: MasterDataImportRowResponse): MasterDataImportRowResponse {
    if (!row.originalRowId) return row;
    return this.facade.batchRows().find((r) => r.id === row.originalRowId) ?? row;
  }

  objectKeys(record: Record<string, string | null> | null | undefined): string[] {
    return record ? Object.keys(record) : [];
  }

  async submitReplay(): Promise<void> {
    const row = this.selectedRow();
    if (!row) return;
    this.replaySaving.set(true);
    const result = await this.facade.replayRow(row.id, this.replayDraft());
    this.replaySaving.set(false);
    if (result) {
      this.selectedRow.set(null);
      this.replayDraft.set({});
      this.restoreFocusToOpener();
    }
  }

  // ---- Detail tabs ----
  setDetailTab(tab: ImportDetailTab): void {
    this.detailTab.set(tab);
    const batch = this.facade.currentBatch();
    if (!batch) return;
    if (tab === 'audit' && this.auditLoadedForBatch() !== batch.id) {
      this.auditLoadedForBatch.set(batch.id);
      void this.facade.loadBatchAudit(batch.id);
    }
    if (tab === 'evidence' && this.evidenceLoadedForBatch() !== batch.id) {
      this.evidenceLoadedForBatch.set(batch.id);
      void this.loadEvidenceForBatch(batch.id);
    }
  }

  onTabsKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key !== 'ArrowLeft' && key !== 'ArrowRight' && key !== 'Home' && key !== 'End') return;
    event.preventDefault();
    const tabs = this.detailTabs.map((t) => t.tab);
    const currentIdx = tabs.indexOf(this.detailTab());
    const isRtl = this.language.language() === 'ar';
    let nextIdx: number;

    if (key === 'Home') {
      nextIdx = 0;
    } else if (key === 'End') {
      nextIdx = tabs.length - 1;
    } else {
      const forward = key === 'ArrowRight';
      const movesNext = isRtl ? !forward : forward;
      nextIdx = movesNext ? (currentIdx + 1) % tabs.length : (currentIdx - 1 + tabs.length) % tabs.length;
    }

    const nextTab = tabs[nextIdx];
    this.setDetailTab(nextTab);
    setTimeout(() => document.getElementById('import-tab-' + nextTab)?.focus(), 0);
  }

  private async loadEvidenceForBatch(batchId: string): Promise<void> {
    this.evidenceStatus.set('loading');
    this.evidenceError.set(null);
    await this.facade.loadBatchEvidence(batchId);

    if (this.facade.currentBatch()?.id !== batchId) {
      // A newer navigation superseded this request; leave state for the current request to settle.
      return;
    }

    const error = this.facade.error();
    if (error) {
      this.evidenceStatus.set('error');
      this.evidenceError.set(error);
    } else {
      this.evidenceStatus.set('loaded');
    }
  }

  retryEvidenceLoad(): void {
    const batch = this.facade.currentBatch();
    if (!batch) return;
    void this.loadEvidenceForBatch(batch.id);
  }

  dispositionLabel(disposition: MasterDataImportRowResponse['mutationDisposition']): string {
    switch (disposition) {
      case 'NotAttempted':
        return this.language.text('importDispositionNotAttempted');
      case 'Eligible':
        return this.language.text('importDispositionEligible');
      case 'SkippedExisting':
        return this.language.text('importDispositionSkippedExisting');
      case 'Committed':
        return this.language.text('importDispositionCommitted');
      case 'Updated':
        return this.language.text('importDispositionUpdated');
      case 'Failed':
        return this.language.text('importDispositionFailed');
    }
  }

  // ---- Dialog accessibility helpers (Finding 3A) ----
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
    const selector =
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(panel.querySelectorAll<HTMLElement>(selector));
  }

  private restoreFocusToOpener(): void {
    const el = this.lastFocusedElement;
    this.lastFocusedElement = null;
    if (el && document.contains(el)) {
      setTimeout(() => el.focus(), 0);
    }
  }

  // ---- Display helpers ----
  resourceLabel(kind: MasterDataImportResourceKind | null): string {
    switch (kind) {
      case 'ProductCategory':
        return this.language.text('importResourceCategoryLabel');
      case 'UnitOfMeasure':
        return this.language.text('importResourceUnitLabel');
      case 'Product':
        return this.language.text('importResourceProductLabel');
      case 'Supplier':
        return this.language.text('importResourceSupplierLabel');
      case 'BusinessCustomer':
        return this.language.text('importResourceCustomerLabel');
      case 'PriceList':
        return this.language.text('importResourcePriceListLabel');
      case 'Tax':
        return this.language.text('importResourceTaxLabel');
      case 'PaymentTerm':
        return this.language.text('importResourcePaymentTermLabel');
      case 'Currency':
        return this.language.text('importResourceCurrencyLabel');
      case 'ExchangeRate':
        return this.language.text('importResourceExchangeRateLabel');
      default:
        return '—';
    }
  }

  statusLabel(status: MasterDataImportStatus): string {
    switch (status) {
      case 'Draft':
        return this.language.text('importStatusDraft');
      case 'Simulating':
        return this.language.text('importStatusSimulating');
      case 'Validated':
        return this.language.text('importStatusValidated');
      case 'Completed':
        return this.language.text('importStatusCompleted');
      case 'CompletedWithErrors':
        return this.language.text('importStatusCompletedWithErrors');
      case 'Failed':
        return this.language.text('importStatusFailed');
    }
  }

  statusPillClass(status: MasterDataImportStatus): string {
    if (status === 'Completed') return 'status-pill--success';
    if (status === 'CompletedWithErrors') return 'status-pill--warning';
    if (status === 'Failed') return 'status-pill--danger';
    return 'status-pill--neutral';
  }

  policyLabel(policy: MasterDataImportDuplicatePolicy): string {
    switch (policy) {
      case 'Reject':
        return this.language.text('importDuplicatePolicyReject');
      case 'SkipExisting':
        return this.language.text('importDuplicatePolicySkip');
      case 'UpdateMutableFields':
        return this.language.text('importDuplicatePolicyUpdate');
    }
  }

  modeLabel(mode: MasterDataImportMode): string {
    return mode === 'DryRun' ? this.language.text('importModeDryRun') : this.language.text('importModeCommit');
  }

  outcomeLabel(outcome: MasterDataImportRowResponse['outcome']): string {
    switch (outcome) {
      case 'Accepted':
        return this.language.text('importOutcomeAccepted');
      case 'Rejected':
        return this.language.text('importOutcomeRejected');
      case 'Quarantined':
        return this.language.text('importOutcomeQuarantined');
      case 'NotProcessed':
        return this.language.text('importOutcomeNotProcessed');
    }
  }

  outcomeIcon(outcome: MasterDataImportRowResponse['outcome']): string {
    switch (outcome) {
      case 'Accepted':
        return '✓';
      case 'Rejected':
        return '✕';
      case 'Quarantined':
        return '⚠';
      default:
        return '…';
    }
  }

  outcomeBadgeClass(outcome: MasterDataImportRowResponse['outcome']): string {
    if (outcome === 'Accepted') return 'badge--success';
    if (outcome === 'Rejected') return 'badge--danger';
    if (outcome === 'Quarantined') return 'badge--warning';
    return 'badge--neutral';
  }

  severityLabel(severity: MasterDataImportRowResponse['highestSeverity']): string {
    switch (severity) {
      case 'Info':
        return this.language.text('severityInfo');
      case 'Warning':
        return this.language.text('severityWarning');
      case 'Error':
        return this.language.text('severityError');
    }
  }

  severityIcon(severity: MasterDataImportRowResponse['highestSeverity']): string {
    if (severity === 'Error') return '✕';
    if (severity === 'Warning') return '!';
    return 'i';
  }

  severityBadgeClass(severity: MasterDataImportRowResponse['highestSeverity']): string {
    if (severity === 'Error') return 'badge--danger';
    if (severity === 'Warning') return 'badge--warning';
    return 'badge--info';
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(2)} MB`;
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
      case 'import_concurrency_conflict':
        return this.language.text('importConcurrencyConflictError');
      case 'validation_failed':
      case 'import_request_invalid':
      case 'import_row_invalid':
      case 'import_row_number_invalid':
      case 'import_field_invalid':
      case 'import_field_required':
      case 'import_source_invalid':
        return this.language.text('validationError');
      case 'network_error':
        return this.language.text('networkError');
      case 'import_batch_not_found':
        return this.language.text('importBatchNotFoundError');
      case 'import_row_not_found':
        return this.language.text('importRowNotFoundError');
      case 'import_replay_not_allowed':
        return this.language.text('importReplayNotAllowedError');
      case 'import_duplicate':
      case 'import_field_duplicate':
        return this.language.text('importDuplicateErrorMsg');
      case 'import_reference_invalid':
      case 'import_reference_unavailable':
        return this.language.text('importReferenceInvalidErrorMsg');
      case 'import_row_limit_exceeded':
        return this.language.text('importRowLimitExceededErrorMsg');
      case 'import_dry_run_no_commit':
        return this.language.text('importDryRunNoCommitErrorMsg');
      case 'import_batch_invalid_state':
        return this.language.text('importBatchInvalidStateErrorMsg');
      case 'import_resource_unsupported':
      case 'import_unavailable':
      case 'import_commit_failed':
      case 'import_commit_reparse_failed':
      case 'import_persistence_failed':
      case 'import_audit_persistence_failed':
      case 'import_row_payload_too_large':
      case 'import_field_too_large':
      case 'import_replay_idempotency_required':
      case 'import_replay_idempotency_conflict':
      case 'import_untrusted_field':
      case 'import_update_not_permitted':
      case 'idempotency_conflict':
      case 'idempotency_key_invalid':
      case 'persistence_unavailable':
        return this.language.text('importUnavailableErrorMsg');
      default:
        return this.language.text('requestError');
    }
  }
}
