import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ReportingDefinition, ReportingJob, ReportingLineage, ReportingQuery, ReportingResult, ReportingRow, ReportingSchedule, ReportingService } from './reporting.service';

type Bilingual = { en: string; ar: string };
const copy: Record<string, Bilingual> = {
  metadata: { en: 'Report result metadata', ar: '\\u0645\\u0639\\u0644\\u0648\\u0645\\u0627\\u062a \u0646\\u062a\\u064a\\u062c\\u0629 \u0627\\u0644\\u062a\\u0642\\u0631\\u064a\\u0631' },
  readOnly: { en: 'Read-only publication', ar: '\\u0646\\u0634\\u0631 \u0644\\u0644\\u0642\\u0631\\u0627\\u0621\\u0629 \u0641\\u0642\\u0637' },
  awaitingRun: { en: 'Awaiting run', ar: '\\u0628\\u0627\\u0646\\u062a\\u0638\\u0627\\u0631 \u0627\\u0644\\u062a\\u0634\\u063a\\u064a\\u0644' },
  notEvaluated: { en: 'Not evaluated', ar: '\\u0644\\u0645 \u064a\\u062a\\u0645 \u0627\\u0644\\u062a\\u0642\\u064a\\u064a\\u0645' },
  definitions: { en: 'definitions', ar: '\\u062a\\u0639\\u0631\\u064a\\u0641\\u0627\\u062a' },
  sourceRows: { en: 'source rows', ar: '\\u0635\\u0641\\u0648\\u0641 \u0645\\u0635\\u062f\\u0631\\u064a\\u0629' },
  allStatuses: { en: 'All source statuses', ar: '\\u0643\\u0644 \u062d\\u0627\\u0644\\u0627\\u062a \u0627\\u0644\\u0645\\u0635\\u062f\\u0631' },
  selectedRow: { en: 'Selected row', ar: '\\u0627\\u0644\\u0635\\u0641 \u0627\\u0644\\u0645\\u062d\\u062f\\u062f' },
  selectRow: { en: 'Select a row to inspect its source path.', ar: '\\u062d\\u062f\\u062f \u0635\\u0641\\u064b\\u0627 \u0644\\u0641\\u062d\\u0635 \u0645\\u0633\\u0627\\u0631 \u0627\\u0644\\u0645\\u0635\\u062f\\u0631.' },
  noAsOf: { en: 'No as-of evidence', ar: '\\u0644\\u0627 \u062a\\u0648\\u062c\\u062f \u0623\\u062f\\u0644\\u0629 \u062a\\u0627\\u0631\\u064a\\u062e\\u064a\\u0629' },
  page: { en: 'Page', ar: '\\u0635\\u0641\\u062d\\u0629' },
  of: { en: 'of', ar: '\\u0645\\u0646' },
  serverSelected: { en: 'Server-selected; no raw identifier entry.', ar: '\\u064a\\u062d\\u062f\\u062f\\u0647 \u0627\\u0644\\u062e\\u0627\\u062f\\u0645؛ \u0644\\u0627 \u064a\\u0648\\u062c\\u062f \u0625\\u062f\\u062e\\u0627\\u0644 \u0644\\u0645\\u0639\\u0631\\u0641 \u0627\\u0644\\u062e\\u0627\\u0645.' },
  controlPlane: { en: 'Control plane only', ar: '\\u0644\\u0648\\u062d\\u0629 \u062a\\u062d\\u0643\\u0645 \u0641\\u0642\\u0637' },
  artifactReady: { en: 'Artifact ready', ar: '\\u0627\\u0644\\u0645\\u0644\\u0641 \u062c\\u0627\\u0647\\u0632' },
  noArtifact: { en: 'No artifact published', ar: '\\u0644\\u0645 \u064a\\u0646\\u0634\\u0631 \u0623\\u064a \u0645\\u0644\\u0641' },
  downloadCsv: { en: 'Download CSV', ar: '\\u062a\\u0646\\u0632\\u064a\\u0644 CSV' },
  noSchedules: { en: 'No local schedules have been recorded.', ar: '\\u0644\\u0645 \u064a\\u062a\\u0645 \u062a\\u0633\\u062c\\u064a\\u0644 \u062c\\u062f\\u0627\\u0648\\u0644 \u0645\\u062d\\u0644\\u064a\\u0629.' },
  metadataWaiting: { en: 'Catalogue metadata stays visible until a server-authorized query is run.', ar: '\\u062a\\u0628\\u0642\\u0649 \u0645\\u0639\\u0644\\u0648\\u0645\\u0627\\u062a \u0627\\u0644\\u0641\\u0647\\u0631\\u0633 \u0638\\u0627\\u0647\\u0631\\u0629 \u062d\\u062a\\u0649 \u062a\\u0646\\u0641\\u0630 \u0627\\u0644\\u0645\\u0635\\u0627\\u062f\\u0642\\u0629 \u0639\\u0644\\u0649 \u0627\\u0644\\u062e\\u0627\\u062f\\u0645.' },
  scope: { en: 'Authorized operational context', ar: '\\u0627\\u0644\\u0633\\u064a\\u0627\\u0642 \\u0627\\u0644\\u062a\\u0634\\u063a\\u064a\\u0644\\u064a \\u0627\\u0644\\u0645\\u0635\\u0631\\u0651\\u062d \\u0628\\u0647' },
  kicker: { en: 'Reporting / source signal', ar: '\u0627\u0644\u062a\u0642\u0627\u0631\u064a\u0631 / \u0625\u0634\u0627\u0631\u0629 \u0627\u0644\u0645\u0635\u062f\u0631' },
  title: { en: 'A clear line back to the source.', ar: '\u062e\u0637 \u0648\u0627\u0636\u062d \u064a\u0639\u0648\u062f \u0625\u0644\u0649 \u0627\u0644\u0645\u0635\u062f\u0631.' },
  lead: { en: 'Read-only views assembled from the owning domains. Every answer keeps its scope, definition, freshness, reconciliation state, and evidence path in view.', ar: '\u0639\u0631\u0648\u0636 \u0644\u0644\u0642\u0631\u0627\u0621\u0629 \u0641\u0642\u0637 \u0645\u0646 \u0627\u0644\u0645\u062c\u0627\u0644\u0627\u062a \u0627\u0644\u0645\u0645\u062a\u0644\u0643\u0629. \u064a\u0638\u0647\u0631 \u0643\u0644 \u0646\u062a\u064a\u062c\u0629 \u0646\u0637\u0627\u0642\u0647\u0627 \u0648\u062a\u0639\u0631\u064a\u0641\u0647\u0627 \u0648\u062d\u062f\u0627\u062b\u062a\u0647\u0627 \u0648\u0645\u0633\u0627\u0631 \u0627\u0644\u062f\u0644\u064a\u0644.' },
  catalogue: { en: 'Report catalogue', ar: '\u0641\u0647\u0631\u0633 \u0627\u0644\u062a\u0642\u0627\u0631\u064a\u0631' },
  choose: { en: 'Choose a report', ar: '\u0627\u062e\u062a\u0631 \u062a\u0642\u0631\u064a\u0631\u064b\u0627' },
  company: { en: 'Company ID (server-authorized)', ar: '\u0645\u0639\u0631\u0651\u0641 \u0627\u0644\u0634\u0631\u0643\u0629 (\u064a\u062a\u0645 \u0627\u0644\u062a\u062d\u0642\u0642 \u0645\u0646\u0647 \u0639\u0644\u0649 \u0627\u0644\u062e\u0627\u062f\u0645)' },
  asOf: { en: 'Data as of', ar: '\u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u062d\u062a\u0649' },
  from: { en: 'From', ar: '\u0645\u0646' },
  to: { en: 'To', ar: '\u0625\u0644\u0649' },
  status: { en: 'Status filter', ar: '\u0645\u0631\u0634\u0651\u062d \u0627\u0644\u062d\u0627\u0644\u0629' },
  run: { en: 'Run report', ar: '\u062a\u0634\u063a\u064a\u0644 \u0627\u0644\u062a\u0642\u0631\u064a\u0631' },
  refresh: { en: 'Refresh catalogue', ar: '\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0641\u0647\u0631\u0633' },
  export: { en: 'Prepare CSV export', ar: '\u062a\u062c\u0647\u064a\u0632 \u062a\u0635\u062f\u064a\u0631 CSV' },
  lineage: { en: 'Lineage & reconciliation', ar: '\u0627\u0644\u0646\u0633\u0628 \u0648\u0627\u0644\u062a\u0633\u0648\u064a\u0629' },
  schedules: { en: 'Local schedules', ar: '\u062c\u062f\u0627\u0648\u0644 \u0645\u062d\u0644\u064a\u0629' },
  scheduleLead: { en: 'Schedules are disabled by default and remain a local control-plane record until a delivery provider is approved.', ar: '\u062a\u0628\u0642\u0649 \u0627\u0644\u062c\u062f\u0627\u0648\u0644 \u0645\u0639\u0637\u0651\u0644\u0629 \u0627\u0641\u062a\u0631\u0627\u0636\u064a\u064b\u0627 \u0648\u0645\u062d\u0644\u064a\u0629 \u062d\u062a\u0649 \u0627\u0639\u062a\u0645\u0627\u062f \u0645\u0648\u0632\u0651\u0639.' },
  addSchedule: { en: 'Add disabled schedule', ar: '\u0625\u0636\u0627\u0641\u0629 \u062c\u062f\u0648\u0644 \u0645\u0639\u0637\u0651\u0644' },
  pending: { en: 'Pending approved source decision', ar: '\u0645\u0639\u0644\u0651\u0642 \u0644\u062d\u064a\u0646 \u0627\u0639\u062a\u0645\u0627\u062f \u0627\u0644\u0645\u0635\u062f\u0631' },
  sourceUnavailable: { en: 'Source capability unavailable', ar: '\u0642\u062f\u0631\u0629 \u0627\u0644\u0645\u0635\u062f\u0631 \u063a\u064a\u0631 \u0645\u062a\u0627\u062d\u0629' },
  productionPolicy: { en: 'Production policy pending', ar: '\u0633\u064a\u0627\u0633\u0629 \u0627\u0644\u0625\u0646\u062a\u0627\u062c \u0642\u064a\u062f \u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f' },
  freshState: { en: 'Fresh', ar: '\u062d\u062f\u064a\u062b' },
  staleState: { en: 'Stale', ar: '\u0642\u062f\u064a\u0645' },
  partialState: { en: 'Partial', ar: '\u062c\u0632\u0626\u064a' },
  unknownState: { en: 'Unknown', ar: '\u063a\u064a\u0631 \u0645\u0639\u0631\u0648\u0641' },
  unavailableState: { en: 'Unavailable', ar: '\u063a\u064a\u0631 \u0645\u062a\u0627\u062d' },
  failedState: { en: 'Failed', ar: '\u0641\u0634\u0644' },
  deniedState: { en: 'Not authorized', ar: '\u063a\u064a\u0631 \u0645\u0635\u0631\u062d \u0628\u0647' },
  scheduleControlPlane: { en: 'Control plane only', ar: '\u0644\u0648\u062d\u0629 \u062a\u062d\u0643\u0645 \u0641\u0642\u0637' },
  enabledLocalTest: { en: 'Enabled (local/test only)', ar: '\u0645\u0641\u0639\u0644 (\u0645\u062d\u0644\u064a/\u0627\u062e\u062a\u0628\u0627\u0631 \u0641\u0642\u0637)' },
  disabledSchedule: { en: 'Disabled', ar: '\u0645\u0639\u0637\u0651\u0644' },
  noRows: { en: 'The source returned no rows for these parameters.', ar: '\u0644\u0645 \u064a\u0639\u062f \u0627\u0644\u0645\u0635\u062f\u0631 \u0628\u0623\u064a \u0635\u0641\u0648\u0641 \u0644\u0647\u0630\u0647 \u0627\u0644\u0645\u0639\u0627\u064a\u064a\u0631.' },
  error: { en: 'The reporting operation could not be completed safely.', ar: '\u062a\u0639\u0630\u0651\u0631 \u0625\u0643\u0645\u0627\u0644 \u0639\u0645\u0644\u064a\u0629 \u0627\u0644\u062a\u0642\u0627\u0631\u064a\u0631 \u0628\u0623\u0645\u0627\u0646.' },
  enterCompany: { en: 'Enter an authorized Company identifier for this report.', ar: '\u0623\u062f\u062e\u0644 \u0645\u0639\u0631\u0651\u0641 \u0634\u0631\u0643\u0629 \u0645\u0635\u0631\u0651\u062d \u0628\u0647 \u0644\u0647\u0630\u0627 \u0627\u0644\u062a\u0642\u0631\u064a\u0631.' },
  previous: { en: 'Previous', ar: '\u0627\u0644\u0633\u0627\u0628\u0642' },
  next: { en: 'Next', ar: '\u0627\u0644\u062a\u0627\u0644\u064a' },
};

@Component({
  selector: 'app-reporting-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="reporting-page" [attr.dir]="language.language() === 'ar' ? 'rtl' : 'ltr'">
      <header class="hero">
        <div class="hero__copy"><p class="eyebrow">{{ text('kicker') }}</p><h1>{{ text('title') }}</h1><p class="lead">{{ text('lead') }}</p></div>
        <div class="hero__stamp"><span>R1</span><small>{{ text('readOnly') }}</small></div>
      </header>

      <div class="signal-strip" [attr.aria-label]="text('metadata')">
        <div><span class="signal-strip__label">{{ text('status') }}</span><strong>{{ result()?.metadata?.state ? stateLabel(result()!.metadata.state) : '—' }}</strong></div>
        <div><span class="signal-strip__label">{{ text('asOf') }}</span><strong>{{ result()?.metadata?.dataAsOf ? (result()?.metadata?.dataAsOf | date:'medium') : '—' }}</strong></div>
        <div><span class="signal-strip__label">{{ text('freshness') }}</span><strong>{{ result()?.metadata?.freshness ? stateLabel(result()!.metadata.freshness) : text('awaitingRun') }}</strong></div>
        <div><span class="signal-strip__label">{{ text('reconciliation') }}</span><strong>{{ result()?.metadata?.reconciliationStatus ?? text('notEvaluated') }}</strong></div>
      </div>

      @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }

      <div class="workspace-grid">
        <aside class="catalogue" aria-labelledby="catalogue-title">
          <div class="section-heading"><div><p class="eyebrow">{{ text('catalogue') }}</p><h2 id="catalogue-title">{{ definitions().length }} definitions</h2></div><button class="icon-button" type="button" (click)="loadCatalogue()" [attr.aria-label]="text('refresh')">↻</button></div>
          @for (group of domains(); track group) {
            <div class="catalogue-group"><span>{{ group }}</span>
              @for (definition of definitionsByDomain(group); track definition.code) {
                <button type="button" class="catalogue-item" [class.is-selected]="selectedCode() === definition.code" (click)="select(definition)">
                  <strong>{{ language.language() === 'ar' ? definition.arabicName : definition.name }}</strong><small>{{ definition.code }}</small>
                  @if (definition.pendingDecision || definition.implementationState === 'OPEN_PRODUCTION_POLICY_ONLY') { <em>{{ text('productionPolicy') }}</em> }
                  @if (definition.implementationState === 'SOURCE_CAPABILITY_UNAVAILABLE') { <em>{{ text('sourceUnavailable') }}</em> }
                </button>
              }
            </div>
          }
        </aside>

        <main class="results-column">
          <section class="control-room">
            <div class="control-room__heading"><div><p class="eyebrow">{{ selectedDefinition()?.domain ?? text('choose') }}</p><h2>{{ selectedDefinition() ? (language.language() === 'ar' ? selectedDefinition()!.arabicName : selectedDefinition()!.name) : text('choose') }}</h2></div><span class="definition-tag">v{{ selectedDefinition()?.definitionVersion ?? '—' }}</span></div>
            <p class="ownership">{{ selectedDefinition()?.sourceOwnership ?? 'Select a definition to inspect its source contract.' }}</p>
            <div class="filters">
              @if (selectedDefinition()?.requiresCompany) { <div class="scope-readout"><span>{{ text('scope') }}</span><strong>{{ operationalContextLabel() }}</strong><small>{{ text('serverSelected') }}</small></div> }
              <label><span>{{ text('asOf') }}</span><input type="date" [(ngModel)]="asOfDate" /></label>
              <label><span>{{ text('from') }}</span><input type="date" [(ngModel)]="fromDate" /></label>
              <label><span>{{ text('to') }}</span><input type="date" [(ngModel)]="toDate" /></label>
              <label><span>{{ text('status') }}</span><input [(ngModel)]="status" [placeholder]="text('allStatuses')" /></label>
              <button class="primary-button" type="button" (click)="run()" [disabled]="busy()">{{ busy() ? '…' : text('run') }}</button>
            </div>
          </section>

          @if (result(); as current) {
            <section class="result-panel" aria-live="polite">
              <div class="result-panel__heading"><div><span class="eyebrow">{{ current.metadata.scope }}</span><h2>{{ current.totalRows }} source rows</h2></div><div class="result-actions"><button class="secondary-button" type="button" (click)="exportReport()" [disabled]="busy() || !current.definition.exportEnabled">{{ exportBusy() ? '…' : text('export') }}</button><span class="correlation">{{ current.metadata.correlationId }}</span></div></div>
              @if (current.metadata.explanation) { <p class="result-note" [class.result-note--pending]="current.metadata.state === 'Pending'" [class.result-note--unavailable]="current.metadata.state === 'Unavailable'">{{ current.metadata.state === 'Pending' ? text('pending') : current.metadata.state === 'Unavailable' ? text('sourceUnavailable') : current.metadata.explanation }}</p> }
              @if (current.rows.length) {
                <div class="table-wrap"><table><thead><tr>@for (column of current.columns; track column.key) { <th><button type="button" (click)="sort(column.key)">{{ language.language() === 'ar' ? column.arabicLabel : column.label }} <span aria-hidden="true">{{ sortBy() === column.key ? (sortDirection() === 'asc' ? '↑' : '↓') : '↕' }}</span></button></th> }</tr></thead><tbody>@for (row of current.rows; track row.key) { <tr (click)="selectRow(row)" [class.is-focused]="focusedRow()?.key === row.key">@for (column of current.columns; track column.key) { <td>{{ value(row, column.key) ?? '—' }}</td> }</tr> }</tbody></table></div>
                <div class="pager"><span>Page {{ page() }} / {{ pageCount() }}</span><div><button class="secondary-button" type="button" (click)="previousPage()" [disabled]="page() <= 1 || busy()">{{ text('previous') }}</button><button class="secondary-button" type="button" (click)="nextPage()" [disabled]="page() >= pageCount() || busy()">{{ text('next') }}</button></div></div>
              } @else { <div class="empty-result"><strong>{{ current.metadata.state === 'Pending' ? text('pending') : current.metadata.state === 'Unavailable' ? text('sourceUnavailable') : text('noRows') }}</strong><span>{{ current.metadata.reconciliationStatus }}</span></div> }
            </section>

            <section class="evidence-grid">
              <article class="evidence-card"><div class="section-heading"><h2>{{ text('lineage') }}</h2><span class="status-dot"></span></div><p>{{ current.definition.reconciliationPath }}</p>@for (source of current.metadata.sources; track source.sourceDomain) { <div class="evidence-row"><strong>{{ source.sourceDomain }}</strong><span>{{ source.status }}</span><small>{{ source.dataAsOf ? (source.dataAsOf | date:'medium') : 'No as-of evidence' }}</small></div> }</article>
              <article class="evidence-card"><div class="section-heading"><h2>Selected row</h2><span class="definition-tag">{{ focusedRow()?.key ?? '—' }}</span></div>@if (focusedRow()?.lineage?.length) { @for (item of focusedRow()!.lineage; track item.sourceReference) { <div class="lineage-card"><strong>{{ item.sourceDomain }}</strong><span>{{ item.sourceStatus }}</span><small>{{ item.sourceReference }}</small><small>{{ item.reconciliationReference }}</small></div> } } @else { <p class="muted">Select a row to inspect its source path.</p> }</article>
            </section>
          } @else { <section class="empty-result"><strong>{{ text('choose') }}</strong><span>Catalogue metadata stays visible until a server-authorized query is run.</span></section> }

          <section class="schedule-panel"><div class="section-heading"><div><p class="eyebrow">{{ text('schedules') }}</p><h2>{{ text('scheduleControlPlane') }}</h2></div><button class="secondary-button" type="button" (click)="addSchedule()" [disabled]="!selectedDefinition()?.schedulingEnabled || scheduleBusy()">{{ text('addSchedule') }}</button></div><p>{{ text('scheduleLead') }}</p>@if (job()) { <div class="job-callout"><strong>Export {{ job()!.status }}</strong><span>{{ job()!.failureCode ?? (job()!.artifactId ? text('artifactReady') : text('noArtifact')) }}</span>@if (job()!.artifactId) { <a [href]="service.artifactUrl(job()!.artifactId!)" target="_blank" rel="noopener">{{ text('downloadCsv') }}</a> }</div> } @for (schedule of schedules(); track schedule.scheduleId) { <div class="schedule-row"><div><strong>{{ schedule.reportCode }}</strong><small>{{ schedule.recurrence }} · {{ schedule.timeZone }} · {{ schedule.destinationKind }}</small></div><button class="secondary-button" type="button" (click)="toggleSchedule(schedule)">{{ schedule.status === 'Enabled' ? text('enabledLocalTest') : text('disabledSchedule') }}</button></div> } @if (!schedules().length) { <p class="muted">{{ text('noSchedules') }}</p> }</section>
        </main>
      </div>
    </section>
  `,
  styles: [`
    :host{display:block}.reporting-page{display:grid;gap:1rem;color:var(--ink)}.hero{display:flex;justify-content:space-between;gap:2rem;align-items:flex-end;padding:clamp(1.4rem,4vw,3rem);border-radius:1.25rem;background:linear-gradient(120deg,#102b2a 0%,#183c38 62%,#2d6655 100%);color:#f5fbf5;box-shadow:0 1.3rem 3rem rgb(16 43 42 / 18%)}.hero__copy{max-width:52rem}.hero .eyebrow{color:#a9d9bb}.eyebrow{margin:0 0 .5rem;color:var(--accent-strong);font-size:.67rem;font-weight:800;letter-spacing:.14em;text-transform:uppercase}.hero h1{margin:0;max-width:12ch;font:800 clamp(2.1rem,5vw,4.8rem)/.96 var(--font-display);letter-spacing:-.06em}.lead{max-width:48rem;margin:1rem 0 0;color:#c2d9cf;line-height:1.65}.hero__stamp{display:flex;align-items:center;gap:.65rem;color:#a9d9bb;font:800 1.4rem/1 var(--font-display);text-transform:uppercase}.hero__stamp small{color:#d9eee2;font:700 .62rem/1.3 var(--font-sans);letter-spacing:.1em}.signal-strip{display:grid;grid-template-columns:repeat(4,1fr);border-block:1px solid var(--line);background:var(--surface-raised)}.signal-strip>div{display:grid;gap:.28rem;padding:.85rem 1rem;border-inline-end:1px solid var(--line)}.signal-strip>div:last-child{border:0}.signal-strip__label{color:var(--ink-muted);font-size:.62rem;font-weight:800;letter-spacing:.09em;text-transform:uppercase}.signal-strip strong{font-size:.8rem}.alert{padding:.8rem 1rem;border:1px solid color-mix(in srgb,var(--danger) 40%,var(--line));border-radius:.65rem;color:var(--danger);background:#fff5f4}.workspace-grid{display:grid;grid-template-columns:18rem minmax(0,1fr);gap:1rem;align-items:start}.catalogue,.control-room,.result-panel,.evidence-card,.schedule-panel{border:1px solid var(--line);background:var(--surface-raised);box-shadow:var(--shadow-soft)}.catalogue{position:sticky;top:1rem;padding:.9rem}.section-heading,.control-room__heading,.result-panel__heading{display:flex;align-items:flex-start;justify-content:space-between;gap:1rem}.section-heading h2,.control-room h2,.result-panel h2,.schedule-panel h2{margin:0;font:800 1.1rem/1.15 var(--font-display);letter-spacing:-.03em}.section-heading{align-items:center}.icon-button{border:0;background:transparent;color:var(--accent-strong);font-size:1.2rem;cursor:pointer}.catalogue-group{display:grid;gap:.32rem;margin-top:1.2rem}.catalogue-group>span{padding:.2rem .45rem;color:var(--ink-muted);font-size:.63rem;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.catalogue-item{display:grid;gap:.18rem;width:100%;padding:.65rem .55rem;border:1px solid transparent;border-radius:.6rem;text-align:start;background:transparent;color:var(--ink);cursor:pointer}.catalogue-item:hover,.catalogue-item.is-selected{border-color:color-mix(in srgb,var(--accent-strong) 28%,var(--line));background:var(--accent-soft)}.catalogue-item strong{font-size:.75rem}.catalogue-item small{color:var(--ink-muted);font:600 .62rem/1.2 var(--font-mono)}.catalogue-item em{color:var(--support);font-size:.62rem;font-style:normal}.results-column{display:grid;gap:1rem;min-width:0}.control-room,.result-panel,.evidence-card,.schedule-panel{padding:clamp(1rem,2.5vw,1.4rem)}.definition-tag{display:inline-flex;border:1px solid var(--line-strong);border-radius:99px;padding:.35rem .55rem;color:var(--ink-muted);font:700 .64rem/1 var(--font-mono);white-space:nowrap}.ownership{max-width:60rem;margin:.7rem 0;color:var(--ink-muted);font-size:.8rem;line-height:1.5}.filters{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:.65rem;align-items:end;margin-top:1.1rem}label{display:grid;gap:.3rem;min-width:0}label span{color:var(--ink-muted);font-size:.62rem;font-weight:800;letter-spacing:.08em;text-transform:uppercase}input{width:100%;min-height:2.55rem;padding:.55rem .65rem;border:1px solid var(--line-strong);border-radius:.5rem;color:var(--ink);background:var(--surface);font:inherit}.primary-button,.secondary-button{min-height:2.55rem;padding:.55rem .8rem;border-radius:.5rem;font:700 .75rem/1 var(--font-sans);cursor:pointer}.primary-button{border:1px solid var(--ink);color:#fff;background:var(--ink)}.secondary-button{border:1px solid var(--line-strong);color:var(--ink);background:var(--surface-raised)}button:disabled{cursor:not-allowed;opacity:.5}.hint,.result-note,.schedule-panel>p{color:var(--ink-muted);font-size:.76rem;line-height:1.5}.result-note--pending{border-inline-start:3px solid var(--support);padding-inline-start:.7rem;color:var(--support)}.result-panel__heading{align-items:center}.result-actions{display:flex;align-items:center;gap:.7rem;flex-wrap:wrap;justify-content:flex-end}.correlation{max-width:14rem;overflow:hidden;color:var(--ink-muted);font:600 .59rem/1.2 var(--font-mono);text-overflow:ellipsis;white-space:nowrap}.table-wrap{overflow:auto;margin-top:1rem;border:1px solid var(--line);border-radius:.65rem}.table-wrap table{width:100%;min-width:38rem;border-collapse:collapse;font-size:.76rem}.table-wrap th,.table-wrap td{padding:.7rem;border-block-end:1px solid var(--line);text-align:start;vertical-align:top}.table-wrap th{background:var(--grid-header)}.table-wrap th button{border:0;color:var(--ink-muted);background:transparent;font:800 .64rem/1.2 var(--font-sans);letter-spacing:.07em;text-transform:uppercase;cursor:pointer}.table-wrap tbody tr{cursor:pointer}.table-wrap tbody tr:hover,.table-wrap tbody tr.is-focused{background:var(--accent-soft)}.pager{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin-top:.8rem;color:var(--ink-muted);font-size:.72rem}.pager div{display:flex;gap:.45rem}.empty-result{display:grid;gap:.45rem;place-items:center;min-height:10rem;padding:1.3rem;border:1px dashed var(--line-strong);color:var(--ink-muted);text-align:center}.empty-result strong{color:var(--ink)}.evidence-grid{display:grid;grid-template-columns:1.1fr .9fr;gap:1rem}.evidence-card{min-width:0}.evidence-card>p{color:var(--ink-muted);font-size:.78rem;line-height:1.5}.status-dot{width:.55rem;height:.55rem;border-radius:50%;background:var(--success);box-shadow:0 0 0 .25rem var(--accent-soft)}.evidence-row,.schedule-row{display:grid;grid-template-columns:1fr auto;gap:.25rem .8rem;padding:.7rem 0;border-block-start:1px solid var(--line)}.evidence-row small,.schedule-row small{grid-column:1/-1;color:var(--ink-muted);font-size:.67rem}.lineage-card{display:grid;gap:.2rem;padding:.7rem 0;border-block-start:1px solid var(--line)}.lineage-card span{color:var(--accent-strong);font-size:.7rem;font-weight:800}.lineage-card small{overflow-wrap:anywhere;color:var(--ink-muted);font:600 .64rem/1.4 var(--font-mono)}.job-callout{display:flex;align-items:center;gap:.8rem;flex-wrap:wrap;margin:.8rem 0;padding:.7rem;border:1px solid color-mix(in srgb,var(--accent-strong) 24%,var(--line));border-radius:.6rem;background:var(--accent-soft);font-size:.75rem}.job-callout span{color:var(--ink-muted)}.job-callout a{color:var(--accent-strong);font-weight:800}.schedule-row{align-items:center}.schedule-row button{min-height:2rem;padding:.35rem .6rem;font-size:.68rem}.muted{color:var(--ink-muted);font-size:.78rem}@media(max-width:980px){.workspace-grid{grid-template-columns:1fr}.catalogue{position:static}.catalogue{display:grid;grid-template-columns:repeat(3,1fr);gap:.7rem}.catalogue>.section-heading{grid-column:1/-1}.catalogue-group{margin-top:0}.filters{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:620px){.hero{align-items:flex-start;flex-direction:column}.hero__stamp{align-self:flex-end}.signal-strip{grid-template-columns:repeat(2,1fr)}.signal-strip>div:nth-child(2){border-inline-end:0}.signal-strip>div:nth-child(-n+2){border-block-end:1px solid var(--line)}.catalogue{display:block}.catalogue-group{margin-top:1rem}.filters{grid-template-columns:1fr}.result-panel__heading{align-items:flex-start;flex-direction:column}.result-actions{justify-content:flex-start}.evidence-grid{grid-template-columns:1fr}}@media(prefers-reduced-motion:reduce){.table-wrap tbody tr{transition:none}}
  `],
})
export class ReportingWorkspaceComponent implements OnInit {
  readonly language = inject(LanguageService);
  readonly context = inject(ContextService);
  readonly service = inject(ReportingService);
  readonly definitions = signal<ReportingDefinition[]>([]);
  readonly selectedCode = signal('finance.trial-balance');
  readonly result = signal<ReportingResult | null>(null);
  readonly focusedRow = signal<ReportingRow | null>(null);
  readonly schedules = signal<ReportingSchedule[]>([]);
  readonly job = signal<ReportingJob | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly exportBusy = signal(false);
  readonly scheduleBusy = signal(false);
  readonly page = signal(1);
  readonly pageSize = 25;
  readonly sortBy = signal('');
  readonly sortDirection = signal<'asc' | 'desc'>('asc');
  asOfDate = this.today();
  fromDate = `${new Date().getUTCFullYear()}-01-01`;
  toDate = this.today();
  status = '';
  readonly selectedDefinition = computed(() => this.definitions().find(item => item.code === this.selectedCode()) ?? null);
  readonly domains = computed(() => [...new Set(this.definitions().map(item => item.domain))]);
  readonly pageCount = computed(() => Math.max(1, Math.ceil((this.result()?.totalRows ?? 0) / this.pageSize)));

  ngOnInit(): void { this.loadCatalogue(); this.loadSchedules(); }
  text(key: string): string {
    if (this.language.language() === 'ar') {
      const arabic: Record<string, string> = {
        metadata: 'معلومات نتيجة التقرير', readOnly: 'نشر للقراءة فقط', awaitingRun: 'بانتظار التشغيل', notEvaluated: 'لم يتم التقييم',
        definitions: 'تعريفات', sourceRows: 'صفوف مصدرية', allStatuses: 'كل حالات المصدر', selectedRow: 'الصف المحدد',
        selectRow: 'حدد صفًا لفحص مسار المصدر.', noAsOf: 'لا توجد أدلة تاريخية', page: 'صفحة',
        serverSelected: 'يحدده الخادم؛ لا يوجد إدخال للمعرف الخام.', controlPlane: 'لوحة تحكم فقط', artifactReady: 'الملف جاهز',
        noArtifact: 'لم ينشر أي ملف', downloadCsv: 'تنزيل CSV', noSchedules: 'لم يتم تسجيل جداول محلية.',
        metadataWaiting: 'تبقى معلومات الفهرس ظاهرة حتى تنفذ المصادقة على الخادم.'
      };
      if (arabic[key]) return arabic[key];
    }
    const arabicOverrides: Record<string, string> = {
      metadata: '\\u0645\\u0639\\u0644\\u0648\\u0645\\u0627\\u062a \\u0646\\u062a\\u064a\\u062c\\u0629 \\u0627\\u0644\\u062a\\u0642\\u0631\\u064a\\u0631',
      readOnly: '\\u0646\\u0634\\u0631 \\u0644\\u0644\\u0642\\u0631\\u0627\\u0621\\u0629 \\u0641\\u0642\\u0637',
      awaitingRun: '\\u0628\\u0627\\u0646\\u062a\\u0638\\u0627\\u0631 \\u0627\\u0644\\u062a\\u0634\\u063a\\u064a\\u0644',
      notEvaluated: '\\u0644\\u0645 \\u064a\\u062a\\u0645 \\u0627\\u0644\\u062a\\u0642\\u064a\\u064a\\u0645',
      definitions: '\\u062a\\u0639\\u0631\\u064a\\u0641\\u0627\\u062a',
      sourceRows: '\\u0635\\u0641\\u0648\\u0641 \\u0645\\u0635\\u062f\\u0631\\u064a\\u0629',
      allStatuses: '\\u0643\\u0644 \\u062d\\u0627\\u0644\\u0627\\u062a \\u0627\\u0644\\u0645\\u0635\\u062f\\u0631',
      selectedRow: '\\u0627\\u0644\\u0635\\u0641 \\u0627\\u0644\\u0645\\u062d\\u062f\\u062f',
      selectRow: '\\u062d\\u062f\\u062f \\u0635\\u0641\\u064b\\u0627 \\u0644\\u0641\\u062d\\u0635 \\u0645\\u0633\\u0627\\u0631 \\u0627\\u0644\\u0645\\u0635\\u062f\\u0631.',
      noAsOf: '\\u0644\\u0627 \\u062a\\u0648\\u062c\\u062f \\u0623\\u062f\\u0644\\u0629 \\u062a\\u0627\\u0631\\u064a\\u062e\\u064a\\u0629',
      page: '\\u0635\\u0641\\u062d\\u0629',
      serverSelected: '\\u064a\\u062d\\u062f\\u062f\\u0647 \\u0627\\u0644\\u062e\\u0627\\u062f\\u0645\\u061b \\u0644\\u0627 \\u064a\\u0648\\u062c\\u062f \\u0625\\u062f\\u062e\\u0627\\u0644 \\u0644\\u0644\\u0645\\u0639\\u0631\\u0641 \\u0627\\u0644\\u062e\\u0627\\u0645.',
      controlPlane: '\\u0644\\u0648\\u062d\\u0629 \\u062a\\u062d\\u0643\\u0645 \\u0641\\u0642\\u0637',
      artifactReady: '\\u0627\\u0644\\u0645\\u0644\\u0641 \\u062c\\u0627\\u0647\\u0632',
      noArtifact: '\\u0644\\u0645 \\u064a\\u0646\\u0634\\u0631 \\u0623\\u064a \\u0645\\u0644\\u0641',
      downloadCsv: '\\u062a\\u0646\\u0632\\u064a\\u0644 CSV',
      noSchedules: '\\u0644\\u0645 \\u064a\\u062a\\u0645 \\u062a\\u0633\\u062c\\u064a\\u0644 \\u062c\\u062f\\u0627\\u0648\\u0644 \\u0645\\u062d\\u0644\\u064a\\u0629.',
      metadataWaiting: '\\u062a\\u0628\\u0642\\u0649 \\u0645\\u0639\\u0644\\u0648\\u0645\\u0627\\u062a \\u0627\\u0644\\u0641\\u0647\\u0631\\u0633 \\u0638\\u0627\\u0647\\u0631\\u0629 \\u062d\\u062a\\u0649 \\u062a\\u0646\\u0641\\u0630 \\u0627\\u0644\\u0645\\u0635\\u0627\\u062f\\u0642\\u0629 \\u0639\\u0644\\u0649 \\u0627\\u0644\\u062e\\u0627\\u062f\\u0645.'
    };
    const value = copy[key];
    return this.language.language() === 'ar' ? arabicOverrides[key] ?? value?.ar ?? value?.en ?? key : value?.en ?? key;
  }
  stateLabel(state: string): string {
    const key = ({ Fresh: 'freshState', Stale: 'staleState', Partial: 'partialState', Unknown: 'unknownState', Unavailable: 'unavailableState', Failed: 'failedState', Denied: 'deniedState', Pending: 'pending' } as Record<string, string>)[state] ?? state;
    return this.text(key);
  }
  definitionsByDomain(domain: string): ReportingDefinition[] { return this.definitions().filter(item => item.domain === domain); }
  operationalContextLabel(): string { return this.context.currentOperationalContext()?.displayName ?? this.context.entry()?.candidateTenantDisplayName ?? 'Server-selected Tenant context'; }
  select(definition: ReportingDefinition): void { this.selectedCode.set(definition.code); this.result.set(null); this.focusedRow.set(null); this.page.set(1); this.error.set(null); }
  loadCatalogue(): void { this.service.catalogue().subscribe({ next: items => this.definitions.set(items), error: () => this.error.set(this.text('error')) }); }
  loadSchedules(): void { this.service.schedules().subscribe({ next: items => this.schedules.set(items), error: () => undefined }); }
  run(): void {
    const definition = this.selectedDefinition();
    if (!definition) return;
    this.busy.set(true); this.error.set(null); this.page.set(1);
    this.service.execute(definition.code, this.query()).subscribe({ next: value => { this.result.set(value); this.focusedRow.set(value.rows[0] ?? null); this.busy.set(false); }, error: () => { this.error.set(this.text('error')); this.busy.set(false); } });
  }
  exportReport(): void {
    const definition = this.selectedDefinition();
    if (!definition?.exportEnabled) return;
    this.exportBusy.set(true); this.error.set(null);
    void this.service.exportReport(definition.code, this.query()).then(value => this.job.set(value)).catch(() => this.error.set(this.text('error'))).finally(() => this.exportBusy.set(false));
  }
  selectRow(row: ReportingRow): void { this.focusedRow.set(row); }
  value(row: ReportingRow, key: string): string | null { return row.values[key] ?? null; }
  sort(key: string): void { this.sortDirection.set(this.sortBy() === key && this.sortDirection() === 'asc' ? 'desc' : 'asc'); this.sortBy.set(key); this.run(); }
  previousPage(): void { if (this.page() > 1) { this.page.update(value => value - 1); this.run(); } }
  nextPage(): void { if (this.page() < this.pageCount()) { this.page.update(value => value + 1); this.run(); } }
  addSchedule(): void {
    const definition = this.selectedDefinition();
    if (!definition?.schedulingEnabled) return;
    this.scheduleBusy.set(true); this.error.set(null);
    void this.service.createSchedule({ reportCode: definition.code, query: this.query(), recurrence: '0 09 * * 1', timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, destinationKind: 'local-test-sink' }).then(() => this.loadSchedules()).catch(() => this.error.set(this.text('error'))).finally(() => this.scheduleBusy.set(false));
  }
  toggleSchedule(schedule: ReportingSchedule): void { this.scheduleBusy.set(true); void this.service.setScheduleStatus(schedule, schedule.status === 'Enabled' ? 'Disabled' : 'Enabled').then(() => this.loadSchedules()).catch(() => this.error.set(this.text('error'))).finally(() => this.scheduleBusy.set(false)); }
  private query(): ReportingQuery {
    const allowed = new Set(this.selectedDefinition()?.allowedFilters ?? []);
    return {
      asOfDate: allowed.has('asOfDate') && this.asOfDate ? this.asOfDate : undefined,
      fromDate: allowed.has('fromDate') && this.fromDate ? this.fromDate : undefined,
      toDate: allowed.has('toDate') && this.toDate ? this.toDate : undefined,
      status: allowed.has('status') && this.status.trim() ? this.status.trim() : undefined,
      sortBy: this.sortBy() || undefined,
      sortDirection: this.sortDirection(),
      page: this.page(),
      pageSize: this.pageSize
    };
  }
  private today(): string { return new Date().toISOString().slice(0, 10); }
}
