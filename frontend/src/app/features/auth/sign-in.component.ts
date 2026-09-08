import { Component, OnInit, inject, isDevMode, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { DevelopmentApiIdentityService } from '../../core/dev/development-api-identity.service';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';

const DEVELOPMENT_USERNAME = 'admin@minierp.local';

@Component({
  selector: 'app-sign-in',
  standalone: true,
  imports: [ReactiveFormsModule, StatusCardComponent],
  template: `
    <main class="auth-shell">
      <section class="auth-surface" aria-labelledby="sign-in-title">
        <div class="auth-brand">
          <picture class="brand-logo-picture">
            <source srcset="assets/Logo_4_3_BG_Removed_Dark.png" media="(prefers-color-scheme: dark)" />
            <img
              class="brand-logo"
              src="assets/Logo_4_3_BG_Removed.png"
              [attr.alt]="language.text('appName')"
              width="1254"
              height="1254"
            />
          </picture>
          <p class="auth-brand__eyebrow">{{ language.text('productBrandTagline') }}</p>
          <p class="auth-brand__copy">{{ language.text('shellWelcome') }}</p>
        </div>

        <div class="auth-form">
          <header class="auth-form__heading">
            <h1 id="sign-in-title">{{ language.text('signInTitle') }}</h1>
            <p>{{ language.text('signInLead') }}</p>
          </header>

          @if (errorMessage()) {
            <app-status-card [title]="language.text('error')" [message]="errorMessage()" state="failed" tone="danger" />
            @if (showDevPasswordHint()) {
              <p class="dev-error-hint">{{ language.text('devPasswordHint') }}</p>
            }
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <div class="field">
              <label for="login">{{ language.text('login') }}</label>
              <input id="login" type="text" autocomplete="username" formControlName="login" />
              @if (form.controls.login.touched && form.controls.login.invalid) {
                <span class="field-error" role="alert">{{ language.text('validationError') }}</span>
              }
            </div>
            <div class="field">
              <label for="password">{{ language.text('password') }}</label>
              <div class="password-field">
                <input
                  id="password"
                  [type]="passwordVisible() ? 'text' : 'password'"
                  autocomplete="current-password"
                  formControlName="password"
                  [attr.aria-describedby]="capsLockOn() ? 'caps-lock-warning' : null"
                  (keyup)="trackCapsLock($event)"
                  (keydown)="trackCapsLock($event)"
                />
                <button
                  type="button"
                  class="password-toggle"
                  [attr.aria-label]="passwordVisible() ? language.text('hidePassword') : language.text('showPassword')"
                  (click)="passwordVisible.set(!passwordVisible())"
                >
                  @if (passwordVisible()) {
                    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M3.7 3.7 20.3 20.3M10.6 10.7a2.5 2.5 0 0 0 3.5 3.5M9.4 5.5A10.6 10.6 0 0 1 12 5.2c5 0 9 3.6 10.5 6.8a11.8 11.8 0 0 1-3 3.9M6.2 7.4C3.9 9 2.2 11.1 1.5 12c1.5 3.2 5.5 6.8 10.5 6.8 1.2 0 2.4-.2 3.5-.6" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  } @else {
                    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="M1.5 12C3 8.8 7 5.2 12 5.2s9 3.6 10.5 6.8C21 15.2 17 18.8 12 18.8S3 15.2 1.5 12Z" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/><circle cx="12" cy="12" r="2.6" fill="none" stroke="currentColor" stroke-width="1.6"/></svg>
                  }
                </button>
              </div>
              @if (capsLockOn()) {
                <span id="caps-lock-warning" class="field-hint" role="status">{{ language.text('capsLockWarning') }}</span>
              }
              @if (form.controls.password.touched && form.controls.password.invalid) {
                <span class="field-error" role="alert">{{ language.text('validationError') }}</span>
              }
            </div>

            @if (isDevelopment) {
              <p class="dev-api-status" [class.is-connected]="devApiStatus() === 'connected'" [class.is-unavailable]="devApiStatus() === 'unavailable'" role="status">
                @if (devApiStatus() === 'checking' || devApiStatus() === 'idle') {
                  {{ language.text('devApiChecking') }}
                } @else if (devApiStatus() === 'connected') {
                  {{ language.text('devApiConnected') }}
                } @else {
                  {{ language.text('devApiUnavailable') }}
                }
              </p>
            }

            <button class="primary-button" type="submit" [disabled]="busy() || blockedByDevPreflight()">
              {{ busy() ? language.text('signingIn') : language.text('signIn') }}
            </button>
          </form>

          @if (isDevelopment) {
            <div class="dev-account">
              <span class="dev-account__label">{{ language.text('devAccountHint') }}</span>
            </div>
          }

          <div class="auth-form__foot">
            <button class="language-button" type="button" (click)="language.toggle()">
              <span aria-hidden="true">◐</span>
              {{ language.language() === 'en' ? language.text('switchToArabic') : language.text('switchToEnglish') }}
            </button>
          </div>
        </div>
      </section>
    </main>
  `,
  styles: `
    :host { display: block; }
    .auth-shell { min-height: 100dvh; display: grid; place-items: center; padding: 1.5rem 1rem; background: var(--canvas); }
    .auth-surface {
      width: min(100%, 58rem);
      display: grid;
      grid-template-columns: 1fr;
      border: 1px solid var(--line);
      border-radius: 1.1rem;
      background: var(--surface-raised);
      box-shadow: var(--shadow-card);
      overflow: hidden;
    }
    @media (min-width: 860px) {
      .auth-surface { grid-template-columns: minmax(15rem, 17rem) 1fr; }
    }
    .auth-brand {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.85rem;
      padding: 2rem 1.75rem;
      text-align: center;
      background: color-mix(in srgb, var(--ink) 4%, var(--surface));
      border-bottom: 1px solid var(--line);
    }
    @media (min-width: 860px) {
      .auth-brand {
        border-bottom: 0;
        border-inline-end: 1px solid var(--line);
        text-align: start;
        align-items: flex-start;
        padding: 2.25rem 1.75rem;
      }
    }
    .brand-logo-picture { display: block; width: min(100%, 13rem); height: auto; }
    .brand-logo { display: block; width: 100%; height: auto; object-fit: contain; }
    .auth-brand__eyebrow { margin: 0; color: var(--ink-muted); font-size: 0.7rem; font-weight: 800; letter-spacing: 0.12em; text-transform: uppercase; }
    .auth-brand__copy { margin: 0; color: var(--ink-muted); font-size: 0.85rem; line-height: 1.55; }
    .auth-form { display: grid; gap: 1.15rem; padding: 2rem 1.75rem; align-content: start; }
    .auth-form__heading { display: grid; gap: 0.35rem; }
    .auth-form__heading h1 { margin: 0; color: var(--ink); font: 700 clamp(1.35rem, 3vw, 1.6rem)/1.2 var(--font-display); letter-spacing: -0.02em; }
    .auth-form__heading p { margin: 0; color: var(--ink-muted); line-height: 1.55; font-size: 0.9rem; }
    form { display: grid; gap: 1rem; }
    .field { display: grid; gap: 0.4rem; }
    label { color: var(--ink); font-size: 0.82rem; font-weight: 700; }
    input { width: 100%; box-sizing: border-box; border: 1px solid var(--line-strong); border-radius: 0.65rem; padding: 0.75rem 0.85rem; color: var(--ink); background: var(--surface); font: inherit; }
    input:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; border-color: var(--accent); }
    .password-field { position: relative; display: flex; align-items: center; }
    .password-field input { padding-inline-end: 2.75rem; }
    .password-toggle {
      position: absolute;
      inset-inline-end: 0.35rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.1rem;
      height: 2.1rem;
      border: 0;
      border-radius: 0.5rem;
      background: transparent;
      color: var(--ink-muted);
      cursor: pointer;
    }
    .password-toggle:hover { color: var(--ink); background: color-mix(in srgb, var(--ink) 6%, transparent); }
    .password-toggle:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
    .password-toggle svg { width: 1.15rem; height: 1.15rem; }
    .field-error { color: var(--danger); font-size: 0.76rem; }
    .field-hint { color: var(--accent-strong); font-size: 0.76rem; font-weight: 650; }
    .dev-error-hint { margin: -0.4rem 0 0; color: var(--ink-muted); font-size: 0.78rem; line-height: 1.5; }
    .dev-api-status { margin: 0; font-size: 0.78rem; font-weight: 650; color: var(--ink-muted); }
    .dev-api-status.is-connected { color: var(--success); }
    .dev-api-status.is-unavailable { color: var(--danger); }
    .primary-button { border: 0; border-radius: 0.65rem; padding: 0.8rem 1rem; color: #fff; background: var(--ink); font: 750 0.9rem/1 var(--font-sans); cursor: pointer; transition: transform 140ms ease, background 140ms ease; }
    .primary-button:hover:not(:disabled) { transform: translateY(-1px); background: var(--ink-strong); }
    .primary-button:disabled { cursor: wait; opacity: 0.56; }
    .dev-account { display: flex; align-items: center; gap: 0.5rem; padding: 0.5rem 0.7rem; border: 1px dashed var(--line-strong); border-radius: 0.55rem; background: var(--surface); }
    .dev-account__label { color: var(--ink-muted); font-size: 0.74rem; }
    .auth-form__foot { display: flex; align-items: center; justify-content: flex-end; }
    .language-button { border: 0; padding: 0; background: transparent; cursor: pointer; color: var(--ink-muted); font: 700 0.8rem/1 var(--font-sans); }
    .language-button:hover { color: var(--accent-strong); }
  `,
})
export class SignInComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly context = inject(ContextService);
  private readonly devApi = inject(DevelopmentApiIdentityService);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly isDevelopment = isDevMode();
  readonly busy = signal(false);
  readonly passwordVisible = signal(false);
  readonly capsLockOn = signal(false);
  readonly devApiStatus = this.devApi.status;
  readonly form = this.formBuilder.nonNullable.group({
    login: [this.isDevelopment ? DEVELOPMENT_USERNAME : '', [Validators.required]],
    password: ['', [Validators.required]],
  });

  constructor() {
    this.form.controls.login.valueChanges.subscribe(() => this.auth.clearError());
    this.form.controls.password.valueChanges.subscribe(() => this.auth.clearError());
  }

  ngOnInit(): void {
    if (this.isDevelopment) {
      void this.devApi.check();
    }
  }

  errorMessage(): string {
    const error = this.auth.lastError();
    if (!error) return '';
    if (this.auth.status() === 'expired') return this.language.text('sessionExpired');
    if (error.code === 'authentication_failed') return this.language.text('signInError');
    if (error.code === 'network_error') return this.language.text('networkError');
    return this.language.text('requestError');
  }

  showDevPasswordHint(): boolean {
    return this.isDevelopment && this.auth.lastError()?.code === 'authentication_failed';
  }

  blockedByDevPreflight(): boolean {
    return this.isDevelopment && this.devApiStatus() === 'unavailable';
  }

  trackCapsLock(event: KeyboardEvent): void {
    if (typeof event.getModifierState === 'function') {
      this.capsLockOn.set(event.getModifierState('CapsLock'));
    }
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.blockedByDevPreflight()) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const signedIn = await this.auth.signIn(this.form.controls.login.value, this.form.controls.password.value);
    if (signedIn) {
      await this.navigateAfterSignIn();
    }
    this.busy.set(false);
  }

  private async navigateAfterSignIn(): Promise<void> {
    if (this.auth.session()?.selectedContextId) {
      await this.router.navigate(['/app']);
      return;
    }

    const entry = await this.context.loadEntry();
    if (!entry) {
      await this.router.navigate(['/app']);
      return;
    }

    if (entry.entryMode === 'CommonHost' && entry.authorizedTenants.length > 1) {
      await this.router.navigate(['/tenant/select']);
      return;
    }

    // Tenant-host and single-Tenant common-host entry both land on Overview.
    // Platform/no-access responses also stay inside the safe shell boundary;
    // the server remains the authority for every later business operation.
    await this.router.navigate(['/app']);
  }
}
