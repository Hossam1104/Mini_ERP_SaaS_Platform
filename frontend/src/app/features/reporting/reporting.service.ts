import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiClientService } from '../../core/api/api-client.service';
import { AuthService } from '../../core/auth/auth.service';

export type ReportingResultState = 'Fresh' | 'Stale' | 'Partial' | 'Unavailable' | 'Failed' | 'Unknown' | 'Pending' | 'Denied';

export interface ReportingQuery {
  companyId?: string;
  branchId?: string;
  warehouseId?: string;
  fromDate?: string;
  toDate?: string;
  asOfDate?: string;
  status?: string;
  currencyCode?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface ReportingDefinition {
  code: string;
  name: string;
  arabicName: string;
  domain: string;
  definitionVersion: string;
  sourceOwnership: string;
  reconciliationPath: string;
  allowedFilters: string[];
  exportEnabled: boolean;
  schedulingEnabled: boolean;
  requiresCompany: boolean;
  pendingDecision: boolean;
  pendingDecisionCode?: string;
}

export interface ReportingColumn { key: string; label: string; arabicLabel: string; dataType: string; }
export interface ReportingLineage { sourceDomain: string; sourceReference: string; sourceStatus: string; sourceVersion?: string; reconciliationReference?: string; }
export interface ReportingRow { key: string; values: Record<string, string | null>; lineage: ReportingLineage[]; }
export interface ReportingSourceEvidence { sourceDomain: string; status: string; sourceVersion?: string; dataAsOf?: string; detail?: string; }
export interface ReportingResultMetadata {
  resultId: string;
  tenantId: string;
  scope: string;
  reportCode: string;
  definitionVersion: string;
  parameters: Record<string, string | null>;
  sources: ReportingSourceEvidence[];
  generatedAt: string;
  dataAsOf?: string;
  state: ReportingResultState;
  freshness: string;
  reconciliationStatus: string;
  reconciliationOwner?: string;
  correlationId: string;
  explanation?: string;
  isProjected: boolean;
}
export interface ReportingResult { definition: ReportingDefinition; metadata: ReportingResultMetadata; columns: ReportingColumn[]; rows: ReportingRow[]; totalRows: number; }
export interface ReportingJob { jobId: string; reportCode: string; status: string; createdAt: string; updatedAt: string; artifactId?: string; failureCode?: string; resultMetadata?: ReportingResultMetadata; }
export interface ReportingSchedule { scheduleId: string; reportCode: string; query: ReportingQuery; recurrence: string; timeZone: string; status: string; destinationKind: string; createdAt: string; updatedAt: string; version: number[]; }

@Injectable({ providedIn: 'root' })
export class ReportingService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);

  catalogue(): Observable<ReportingDefinition[]> { return this.api.get<ReportingDefinition[]>('/reporting/catalogue'); }

  execute(reportCode: string, query: ReportingQuery): Observable<ReportingResult> {
    return this.api.get<ReportingResult>(`/reporting/reports/${encodeURIComponent(reportCode)}?${this.queryString(query)}`);
  }

  async exportReport(reportCode: string, query: ReportingQuery): Promise<ReportingJob> {
    if (!await this.auth.bootstrapAntiforgery()) throw new Error('antiforgery_failed');
    const headers = this.auth.requestHeaders().set('Idempotency-Key', crypto.randomUUID());
    return firstValueFrom(this.api.post<ReportingJob>('/reporting/exports', { reportCode, query }, { headers }));
  }

  job(jobId: string): Observable<ReportingJob> { return this.api.get<ReportingJob>(`/reporting/jobs/${encodeURIComponent(jobId)}`); }
  artifactUrl(artifactId: string): string { return `/api/v1/reporting/artifacts/${encodeURIComponent(artifactId)}`; }
  schedules(): Observable<ReportingSchedule[]> { return this.api.get<ReportingSchedule[]>('/reporting/schedules'); }

  async createSchedule(request: Omit<ReportingSchedule, 'scheduleId' | 'status' | 'createdAt' | 'updatedAt' | 'version'>): Promise<ReportingSchedule> {
    if (!await this.auth.bootstrapAntiforgery()) throw new Error('antiforgery_failed');
    const headers = this.auth.requestHeaders().set('Idempotency-Key', crypto.randomUUID());
    return firstValueFrom(this.api.post<ReportingSchedule>('/reporting/schedules', request, { headers }));
  }

  async setScheduleStatus(schedule: ReportingSchedule, status: 'Enabled' | 'Disabled'): Promise<ReportingSchedule> {
    if (!await this.auth.bootstrapAntiforgery()) throw new Error('antiforgery_failed');
    const headers = this.auth.requestHeaders()
      .set('Idempotency-Key', crypto.randomUUID())
      .set('If-Match', btoa(String.fromCharCode(...schedule.version)));
    return firstValueFrom(this.api.post<ReportingSchedule>(`/reporting/schedules/${schedule.scheduleId}/status`, { status }, { headers }));
  }

  private queryString(query: ReportingQuery): string {
    const params = new URLSearchParams();
    Object.entries(query).forEach(([key, value]) => { if (value !== undefined && value !== null && value !== '') params.set(key, String(value)); });
    return params.toString();
  }
}
