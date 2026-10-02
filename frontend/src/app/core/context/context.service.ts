import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClientService } from '../api/api-client.service';
import {
  FoundationEmergencyTenantSwitchRequest,
  FoundationEmergencyTenantsResponse,
  FoundationEntryResponse,
  FoundationOperationalContext,
  FoundationOperationalContextSwitchRequest,
  FoundationOperationalContextSwitchResponse,
  FoundationSessionResponse,
  FoundationTenantCandidate,
} from '../api/foundation.models';
import { SafeUiError, toSafeUiError } from '../api/safe-error';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class ContextService {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private requestSequence = 0;

  readonly emergencyTenants = signal<FoundationTenantCandidate[]>([]);
  readonly entry = signal<FoundationEntryResponse | null>(null);
  readonly operationalContexts = signal<FoundationOperationalContext[]>([]);
  readonly operationalSelectionVersion = signal(0);
  readonly selectedOperationalContextId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly switching = signal(false);
  readonly lastError = signal<SafeUiError | null>(null);
  readonly currentOperationalContext = computed(() => {
    const selectedId = this.selectedOperationalContextId();
    return selectedId
      ? this.operationalContexts().find((candidate) => candidate.contextId === selectedId) ?? null
      : null;
  });

  constructor() {
    effect(() => {
      const state = this.auth.status();
      if (state === 'anonymous' || state === 'expired' || state === 'error') {
        this.emergencyTenants.set([]);
        this.entry.set(null);
        this.operationalContexts.set([]);
        this.selectedOperationalContextId.set(null);
        this.operationalSelectionVersion.set(0);
        this.lastError.set(null);
      }
    });
  }

  async loadEntry(): Promise<FoundationEntryResponse | null> {
    this.loading.set(true);
    this.lastError.set(null);
    try {
      const response = await firstValueFrom(this.api.get<FoundationEntryResponse>('/auth/entry'));
      this.entry.set(response);
      this.operationalContexts.set(response.operationalContexts ?? []);
      this.selectedOperationalContextId.set(response.selectedOperationalContextId ?? null);
      this.operationalSelectionVersion.set(response.operationalSelectionVersion ?? 0);
      return response;
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      this.lastError.set(safeError);
      if (safeError.code === 'authentication_failed') {
        this.auth.markSessionExpired();
      }
      return null;
    } finally {
      this.loading.set(false);
    }
  }

  async loadEmergencyTenants(): Promise<FoundationTenantCandidate[]> {
    this.loading.set(true);
    this.lastError.set(null);
    try {
      const response = await firstValueFrom(this.api.get<FoundationEmergencyTenantsResponse>('/auth/emergency-tenants'));
      this.emergencyTenants.set(response.tenants ?? []);
      return this.emergencyTenants();
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      this.lastError.set(safeError);
      if (safeError.code === 'authentication_failed') this.auth.markSessionExpired();
      return [];
    } finally {
      this.loading.set(false);
    }
  }

  async switchEmergencyTenant(tenantId: string): Promise<boolean> {
    const session = this.auth.session();
    if (!tenantId || !this.emergencyTenants().some((item) => item.tenantId === tenantId) || !session?.isEmergencySuperAdministrator) {
      this.lastError.set({ code: 'access_denied', status: 403, correlationId: null });
      return false;
    }

    const sequence = ++this.requestSequence;
    this.switching.set(true);
    this.lastError.set(null);
    try {
      if (!(await this.auth.bootstrapAntiforgery())) return false;
      const request: FoundationEmergencyTenantSwitchRequest = {
        tenantId,
        expectedSelectionVersion: session.selectionVersion,
      };
      const headers = this.auth.requestHeaders().set('Idempotency-Key', this.idempotencyKey());
      const response = await firstValueFrom(
        this.api.post<FoundationSessionResponse>('/auth/emergency-tenant-switch', request, { headers }),
      );
      if (sequence !== this.requestSequence) return false;
      this.auth.acceptServerSession(response);
      return true;
    } catch (error: unknown) {
      if (sequence === this.requestSequence) {
        const safeError = toSafeUiError(error);
        this.lastError.set(safeError);
        if (safeError.code === 'authentication_failed') this.auth.markSessionExpired();
      }
      return false;
    } finally {
      if (sequence === this.requestSequence) this.switching.set(false);
    }
  }

  async switchOperationalContext(contextId: string): Promise<boolean> {
    const candidate = this.operationalContexts().find((item) => item.contextId === contextId);
    const session = this.auth.session();
    if (!candidate || !session) {
      this.lastError.set({ code: 'access_denied', status: 403, correlationId: null });
      return false;
    }

    const sequence = ++this.requestSequence;
    this.switching.set(true);
    this.lastError.set(null);
    try {
      if (!(await this.auth.bootstrapAntiforgery())) {
        return false;
      }

      const request: FoundationOperationalContextSwitchRequest = {
        contextId: candidate.contextId,
        expectedSelectionVersion: this.operationalSelectionVersion(),
        expectedEligibilityVersion: candidate.eligibilityVersion,
      };
      const headers = this.auth.requestHeaders()
        .set('Idempotency-Key', this.idempotencyKey());
      const response = await firstValueFrom(
        this.api.post<FoundationOperationalContextSwitchResponse>('/auth/operational-context-switch', request, { headers }),
      );
      if (sequence !== this.requestSequence) {
        return false;
      }
      this.selectedOperationalContextId.set(response.selectedContext.contextId);
      this.operationalSelectionVersion.set(response.selectionVersion);
      return true;
    } catch (error: unknown) {
      if (sequence === this.requestSequence) {
        const safeError = toSafeUiError(error);
        this.lastError.set(safeError);
        if (safeError.code === 'authentication_failed') {
          this.auth.markSessionExpired();
        }
      }
      return false;
    } finally {
      if (sequence === this.requestSequence) {
        this.switching.set(false);
      }
    }
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.() ?? `context-${Date.now().toString(36)}`;
  }

}
