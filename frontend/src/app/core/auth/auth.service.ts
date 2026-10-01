import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  ApiClientService,
} from '../api/api-client.service';
import { FoundationSessionResponse } from '../api/foundation.models';
import { SafeUiError, toSafeUiError } from '../api/safe-error';

export type AuthenticationStatus = 'unknown' | 'loading' | 'authenticated' | 'anonymous' | 'expired' | 'error';

export type SignOutResult =
  | { outcome: 'signed-out' }
  | { outcome: 'session-already-invalid' }
  | { outcome: 'not-confirmed'; error: SafeUiError };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClientService);
  private readonly router = inject(Router);
  private sessionRequest: Promise<boolean> | null = null;
  private developmentBypassRequest: Promise<boolean> | null = null;
  private antiforgeryRequest: Promise<boolean> | null = null;
  private signOutRequest: Promise<SignOutResult> | null = null;
  private signOutGeneration = 0;
  private antiforgeryToken: string | null = null;

  readonly session = signal<FoundationSessionResponse | null>(null);
  readonly developmentBypassActive = signal(false);
  readonly status = signal<AuthenticationStatus>('unknown');
  readonly lastError = signal<SafeUiError | null>(null);
  readonly signingOut = signal(false);
  readonly signOutFailed = signal(false);
  readonly signOutError = signal<SafeUiError | null>(null);

  clearError(): void {
    this.lastError.set(null);
  }

  async ensureSession(): Promise<boolean> {
    if (this.status() === 'authenticated' && this.session()?.authenticated) {
      return true;
    }
    if (this.sessionRequest) {
      return this.sessionRequest;
    }

    this.status.set('loading');
    this.sessionRequest = (async () => {
      if (await this.tryDevelopmentBypass()) {
        return true;
      }

      try {
        const response = await firstValueFrom(this.api.get<FoundationSessionResponse>('/auth/session'));
        this.acceptServerSession(response);
        return response.authenticated;
      } catch (error: unknown) {
        this.markSessionUnavailable(error);
        return false;
      }
    })().finally(() => {
      this.sessionRequest = null;
    });

    return this.sessionRequest;
  }

  async refreshSession(): Promise<boolean> {
    try {
      const response = await firstValueFrom(this.api.get<FoundationSessionResponse>('/auth/session'));
      this.acceptServerSession(response);
      return response.authenticated;
    } catch (error: unknown) {
      this.markSessionUnavailable(error);
      return false;
    }
  }

  async signIn(login: string, password: string): Promise<boolean> {
    this.signOutGeneration += 1;
    this.status.set('loading');
    this.lastError.set(null);
    this.session.set(null);
    this.developmentBypassActive.set(false);
    this.antiforgeryToken = null;
    this.clearSignOutFailure();

    try {
      const response = await firstValueFrom(
        this.api.post<FoundationSessionResponse>('/auth/sign-in', { login, password }),
      );
      this.acceptServerSession(response);
      return true;
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      this.lastError.set(safeError);
      this.status.set('anonymous');
      return false;
    }
  }

  async bootstrapAntiforgery(): Promise<boolean> {
    if (this.antiforgeryToken) {
      return true;
    }
    if (this.antiforgeryRequest) {
      return this.antiforgeryRequest;
    }

    this.antiforgeryRequest = firstValueFrom(this.api.getResponse<{ status: string }>('/auth/antiforgery'))
      .then((response) => {
        const token = response.headers.get('X-CSRF-TOKEN');
        if (!token) {
          this.antiforgeryToken = null;
          this.lastError.set({ code: 'antiforgery_failed', status: 500, correlationId: null });
          return false;
        }
        this.antiforgeryToken = token;
        return true;
      })
      .catch((error: unknown) => {
        this.lastError.set(toSafeUiError(error));
        return false;
      })
      .finally(() => {
        this.antiforgeryRequest = null;
      });

    return this.antiforgeryRequest;
  }

  requestHeaders(): HttpHeaders {
    return this.antiforgeryToken
      ? new HttpHeaders({ 'X-CSRF-TOKEN': this.antiforgeryToken })
      : new HttpHeaders();
  }

  acceptServerSession(response: FoundationSessionResponse): void {
    if (this.signOutRequest) {
      this.signOutGeneration += 1;
    }
    if (!response.authenticated) {
      this.session.set(null);
      this.status.set('anonymous');
      return;
    }
    this.session.set(response);
    this.status.set('authenticated');
    this.lastError.set(null);
  }

  markSessionExpired(): void {
    this.session.set(null);
    this.developmentBypassActive.set(false);
    this.antiforgeryToken = null;
    this.lastError.set({ code: 'authentication_failed', status: 401, correlationId: null });
    this.status.set('expired');
    void this.router.navigate(['/login']);
  }

  signOut(): Promise<SignOutResult> {
    if (this.signOutRequest) {
      return this.signOutRequest;
    }

    if (!this.session()) {
      this.antiforgeryToken = null;
      this.status.set('anonymous');
      const result = Promise.resolve<SignOutResult>({ outcome: 'session-already-invalid' });
      void result.then(() => this.router.navigate(['/login']));
      return result;
    }

    const generation = ++this.signOutGeneration;
    this.signingOut.set(true);
    this.clearSignOutFailure();
    const request = this.performSignOut(generation);
    let trackedRequest!: Promise<SignOutResult>;
    trackedRequest = request.finally(() => {
      if (this.signOutRequest === trackedRequest) {
        this.signingOut.set(false);
        this.signOutRequest = null;
      }
    });
    this.signOutRequest = trackedRequest;
    return trackedRequest;
  }

  private async performSignOut(generation: number): Promise<SignOutResult> {
    let antiforgeryReady = false;
    try {
      antiforgeryReady = await this.bootstrapAntiforgery();
    } catch (error: unknown) {
      return this.markSignOutUnconfirmed(generation, toSafeUiError(error));
    }

    if (!this.isCurrentSignOut(generation)) {
      return this.notConfirmedResult({ code: 'request_failed', status: 409, correlationId: null });
    }

    if (!antiforgeryReady || !this.requestHeaders().has('X-CSRF-TOKEN')) {
      return this.markSignOutUnconfirmed(
        generation,
        this.lastError() ?? { code: 'antiforgery_failed', status: 400, correlationId: null },
      );
    }

    let response: HttpResponse<void>;
    try {
      response = await firstValueFrom(this.api.postResponse<void>('/auth/sign-out', {}, { headers: this.requestHeaders() }));
    } catch (error: unknown) {
      const safeError = toSafeUiError(error);
      if (!this.isCurrentSignOut(generation)) {
        return this.notConfirmedResult(safeError);
      }
      if (safeError.code === 'authentication_failed' || safeError.status === 401) {
        this.markSessionExpired();
        this.clearSignOutFailure();
        return { outcome: 'session-already-invalid' };
      }
      if (safeError.code === 'antiforgery_failed' || safeError.status === 403) {
        this.antiforgeryToken = null;
      }
      return this.markSignOutUnconfirmed(generation, safeError);
    }

    if (response.status !== 204) {
      return this.markSignOutUnconfirmed(generation, {
        code: 'request_failed',
        status: response.status,
        correlationId: null,
      });
    }
    if (!this.isCurrentSignOut(generation)) {
      return this.notConfirmedResult({ code: 'request_failed', status: 409, correlationId: null });
    }
    this.clearConfirmedSession();
    await this.router.navigate(['/login']);
    return { outcome: 'signed-out' };
  }

  private markSignOutUnconfirmed(generation: number, error: SafeUiError): SignOutResult {
    if (!this.isCurrentSignOut(generation)) {
      return this.notConfirmedResult(error);
    }
    this.lastError.set(error);
    this.signOutError.set(error);
    this.signOutFailed.set(true);
    return this.notConfirmedResult(error);
  }

  private notConfirmedResult(error: SafeUiError): SignOutResult {
    return { outcome: 'not-confirmed', error };
  }

  private clearConfirmedSession(): void {
    this.session.set(null);
    this.developmentBypassActive.set(false);
    this.antiforgeryToken = null;
    this.status.set('anonymous');
    this.lastError.set(null);
    this.clearSignOutFailure();
  }

  private clearSignOutFailure(): void {
    this.signOutFailed.set(false);
    this.signOutError.set(null);
  }

  private isCurrentSignOut(generation: number): boolean {
    return this.signOutGeneration === generation;
  }

  private markSessionUnavailable(error: unknown): void {
    const safeError = toSafeUiError(error);
    this.lastError.set(safeError.code === 'authentication_failed' ? null : safeError);
    this.session.set(null);
    this.developmentBypassActive.set(false);
    this.antiforgeryToken = null;
    this.status.set(safeError.code === 'authentication_failed' ? 'anonymous' : 'error');
  }

  private async tryDevelopmentBypass(): Promise<boolean> {
    if (this.developmentBypassRequest) {
      return this.developmentBypassRequest;
    }

    this.developmentBypassRequest = firstValueFrom(
      this.api.post<FoundationSessionResponse>('/auth/development-bypass', {}),
    )
      .then((response) => {
        if (!response.authenticated) {
          return false;
        }

        this.developmentBypassActive.set(true);
        this.acceptServerSession(response);
        return true;
      })
      .catch(() => false)
      .finally(() => {
        this.developmentBypassRequest = null;
      });

    return this.developmentBypassRequest;
  }
}
