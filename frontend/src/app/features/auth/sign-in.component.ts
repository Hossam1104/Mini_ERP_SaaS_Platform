import { Component, OnInit, computed, inject, isDevMode, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { FoundationContextCandidate } from '../../core/api/foundation.models';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { DevelopmentApiIdentityService } from '../../core/dev/development-api-identity.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/presentation/theme.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';

type SignInStep = 'credentials' | 'chooseTenant' | 'empty';

@Component({
  selector: 'app-sign-in',
  standalone: true,
  imports: [ReactiveFormsModule, StatusCardComponent],
  template: `
    <main class="auth-shell">
      <section
        class="auth-surface"
        [class.auth-surface--failed]="auth.status() !== 'expired' && !!errorMessage()"
        aria-labelledby="auth-brand-name"
      >
        <aside class="auth-brand" aria-label="{{ brandName() }}">
          <div class="auth-brand__mark">
            <img class="brand-logo" [src]="brandLogoUrl()" [alt]="brandLogoAlt()" width="1254" height="1254" />
          </div>
          <p class="auth-brand__name" id="auth-brand-name">{{ brandName() }}</p>
          <p class="auth-brand__eyebrow">{{ language.text('productBrandTagline') }}</p>
          <p class="auth-brand__copy">{{ language.text('shellWelcome') }}</p>
        </aside>

        <div class="auth-form" [attr.aria-busy]="busy()">
          @if (step() === 'credentials') {
            <section class="auth-pane auth-pane--credentials" aria-labelledby="sign-in-title">
              <header class="auth-form__heading">
                <h1 id="sign-in-title">{{ language.text('signInTitle') }}</h1>
                <p>{{ language.text('signInLead') }}</p>
              </header>

              @if (errorMessage()) {
                <div class="auth-error" [attr.role]="auth.status() === 'expired' ? 'status' : 'alert'" [attr.aria-live]="auth.status() === 'expired' ? 'polite' : 'assertive'">
                  <app-status-card [title]="language.text('error')" [message]="errorMessage()" state="failed" tone="danger" />
                  @if (showDevPasswordHint()) {
                    <p class="dev-error-hint">{{ language.text('devPasswordHint') }}</p>
                  }
                </div>
              }

              <form class="sign-in-form" [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="busy()">
                <div class="field field--login">
                  <label for="username">{{ language.text('login') }}</label>
                  <input
                    id="username"
                    name="username"
                    type="email"
                    inputmode="email"
                    autocomplete="username"
                    autocapitalize="none"
                    spellcheck="false"
                    required
                    formControlName="login"
                    [attr.aria-invalid]="form.controls.login.touched && form.controls.login.invalid"
                    [attr.aria-describedby]="form.controls.login.touched && form.controls.login.invalid ? 'login-error' : null"
                  />
                  @if (form.controls.login.touched && form.controls.login.invalid) {
                    <span class="field-error" id="login-error" role="alert">{{ language.text('validationError') }}</span>
                  }
                </div>
                <div class="field field--password">
                  <label for="password">{{ language.text('password') }}</label>
                  <div class="password-field">
                    <input
                      id="password"
                      name="password"
                      [type]="passwordVisible() ? 'text' : 'password'"
                      autocomplete="current-password"
                      required
                      formControlName="password"
                      [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid"
                      [attr.aria-describedby]="passwordDescription()"
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
                    <span class="field-error" id="password-error" role="alert">{{ language.text('validationError') }}</span>
                  }
                </div>

                @if (clientDevelopment) {
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

                <button class="button primary-button" type="submit" [disabled]="busy() || blockedByDevPreflight()" [attr.aria-busy]="busy()">
                  @if (busy()) { <span class="button-spinner" aria-hidden="true"></span> }
                  {{ busy() ? language.text('signingIn') : language.text('signIn') }}
                </button>
              </form>

              @if (developmentAccountHint()) {
                <p class="dev-account" role="note">
                  <span>{{ language.text('devAccountHint') }}</span>
                  <code>{{ developmentAccountHint() }}</code>
                </p>
              }
            </section>
          } @else if (step() === 'chooseTenant') {
            <section class="auth-pane auth-pane--next" aria-labelledby="choose-tenant-title">
              <header class="auth-form__heading">
                <p class="auth-kicker">{{ language.text('tenantChooserKicker') }}</p>
                <h1 id="choose-tenant-title">{{ language.text('tenantChooserTitle') }}</h1>
                <p id="choose-tenant-copy">{{ language.text('loginTenantChooserLead') }}</p>
              </header>

              @if (selectionError()) {
                <p class="auth-error-message" role="alert" aria-live="assertive">{{ selectionError() }}</p>
              }

              <form class="tenant-form" (ngSubmit)="chooseTenant()">
                <label for="tenant-context">{{ language.text('availableTenants') }}</label>
                <select
                  id="tenant-context"
                  name="tenant-context"
                  [value]="selectedContextId()"
                  aria-describedby="choose-tenant-copy"
                  (change)="selectedContextId.set($any($event.target).value)"
                  required
                >
                  <option value="">{{ language.text('chooseTenant') }}</option>
                  @for (candidate of tenantContexts(); track candidate.contextId) {
                    <option [value]="candidate.contextId">{{ localizedDisplayName(candidate) }}</option>
                  }
                </select>
                <button class="button primary-button" type="submit" [disabled]="busy() || !selectedContextId()" [attr.aria-busy]="busy()">
                  @if (busy()) { <span class="button-spinner" aria-hidden="true"></span> }
                  {{ busy() ? language.text('openingWorkspace') : language.text('continue') }}
                </button>
              </form>
            </section>
          } @else {
            <section class="auth-pane auth-pane--next" aria-labelledby="no-tenant-title" aria-live="polite">
              <header class="auth-form__heading">
                <p class="auth-kicker">{{ language.text('tenantChooserKicker') }}</p>
                <h1 id="no-tenant-title">{{ language.text('noAccessTitle') }}</h1>
                <p>{{ selectionError() || language.text('noTenantsForAccount') }}</p>
              </header>
              @if (auth.signOutFailed()) {
                <app-status-card [title]="language.text('signOutFailed')" [message]="language.text('signOutFailed')" state="failed" tone="danger" />
              }
              <button class="button primary-button" type="button" [disabled]="busy()" (click)="signOutFromLogin()">
                {{ language.text('signOut') }}
              </button>
            </section>
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
    .auth-shell { position: relative; isolation: isolate; min-height: 100dvh; display: grid; place-items: center; overflow: hidden; padding: clamp(1rem, 4vw, 3rem); background: var(--canvas); }
    .auth-shell::before, .auth-shell::after { position: fixed; z-index: -1; border-radius: 50%; content: ''; pointer-events: none; filter: blur(2px); }
    .auth-shell::before { inset-block-start: -22rem; inset-inline-start: -17rem; width: 46rem; height: 46rem; background: radial-gradient(circle, color-mix(in srgb, var(--accent-action) 18%, transparent), transparent 68%); animation: auth-ambient-drift 24s ease-in-out infinite alternate; }
    .auth-shell::after { inset-block-end: -26rem; inset-inline-end: -16rem; width: 52rem; height: 52rem; background: radial-gradient(circle, color-mix(in srgb, var(--accent) 16%, transparent), transparent 70%); animation: auth-ambient-drift 29s ease-in-out -7s infinite alternate-reverse; }
    .auth-surface { width: min(100%, 58rem); display: grid; grid-template-columns: 1fr; overflow: hidden; border: 1px solid var(--line); border-radius: 1.5rem; background: var(--surface-raised); box-shadow: var(--shadow-card), 0 1.8rem 5rem color-mix(in srgb, var(--accent-action) 10%, transparent); animation: auth-card-enter 540ms cubic-bezier(.2,.75,.25,1) both; }
    @media (min-width: 860px) { .auth-surface { grid-template-columns: minmax(16rem, 20rem) 1fr; } }
    .auth-brand { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.8rem; padding: 2rem 1.5rem; text-align: center; background: linear-gradient(145deg, color-mix(in srgb, var(--accent-soft) 68%, var(--surface-raised)), var(--surface-raised) 74%); border-block-end: 1px solid var(--line); }
    @media (min-width: 860px) { .auth-brand { align-items: flex-start; padding: 2.5rem 2rem; border-block-end: 0; border-inline-end: 1px solid var(--line); text-align: start; } }
    .auth-brand__mark { display: grid; width: min(100%, 13rem); min-height: 6.5rem; place-items: center; border-radius: 1.15rem; background: color-mix(in srgb, var(--surface-raised) 88%, transparent); box-shadow: 0 0.9rem 2.5rem color-mix(in srgb, var(--accent-action) 16%, transparent); animation: auth-brand-float 5.4s ease-in-out 260ms infinite; }
    .brand-logo { display: block; width: min(100%, 12rem); height: 6.2rem; object-fit: contain; filter: drop-shadow(0 0.4rem 0.8rem color-mix(in srgb, var(--accent-action) 16%, transparent)); }
    .auth-brand__name { margin: 0; color: var(--ink); font: 750 clamp(1.2rem, 3vw, 1.55rem)/1.2 var(--font-display); animation: auth-reveal 460ms 110ms both; }
    .auth-brand__eyebrow { margin: 0; color: var(--accent-strong); font-size: 0.7rem; font-weight: 800; letter-spacing: 0.12em; text-transform: uppercase; animation: auth-reveal 460ms 180ms both; }
    .auth-brand__copy { max-width: 20rem; margin: 0; color: var(--ink-muted); font-size: 0.88rem; line-height: 1.6; animation: auth-reveal 460ms 250ms both; }
    .auth-form { display: grid; gap: 1rem; align-content: center; padding: clamp(1.4rem, 4vw, 3rem); }
    .auth-pane { display: grid; gap: 1.15rem; animation: auth-step-enter 380ms cubic-bezier(.2,.75,.25,1) both; }
    .auth-form__heading { display: grid; gap: 0.4rem; }
    .auth-form__heading h1 { margin: 0; color: var(--ink); font: 750 clamp(1.55rem, 4vw, 2rem)/1.18 var(--font-display); letter-spacing: -0.025em; }
    .auth-form__heading p { margin: 0; color: var(--ink-muted); line-height: 1.6; font-size: 0.92rem; }
    .auth-kicker { color: var(--accent-strong) !important; font-size: 0.74rem !important; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
    .sign-in-form, .tenant-form { display: grid; gap: 1rem; }
    .field { display: grid; gap: 0.45rem; }
    .field--login { animation: auth-reveal 440ms 230ms both; }
    .field--password { animation: auth-reveal 440ms 310ms both; }
    .field label, .tenant-form label { color: var(--ink); font-size: 0.84rem; font-weight: 700; transition: color 180ms ease, transform 180ms ease; }
    .field:focus-within label, .tenant-form:focus-within label { color: var(--accent-strong); transform: translateY(-1px); }
    .field input, .tenant-form select { width: 100%; box-sizing: border-box; min-height: 46px; border: 1px solid var(--line-strong); border-radius: var(--radius-control); padding: 0.7rem 0.85rem; color: var(--ink); background: linear-gradient(180deg, var(--surface-raised), var(--surface-tint)); box-shadow: inset 0 1px var(--control-gloss), 0 3px 9px rgb(15 32 58 / 8%); font: inherit; transition: border-color 180ms ease, box-shadow 180ms ease, background-color 180ms ease; }
    .field input:focus-visible, .tenant-form select:focus-visible { border-color: var(--accent-action); outline: 3px solid var(--focus); outline-offset: 2px; box-shadow: 0 0 0 4px color-mix(in srgb, var(--accent-action) 24%, transparent); }
    .password-field { position: relative; display: flex; align-items: center; }
    .password-field input { padding-inline-end: 3rem; }
    .password-toggle { position: absolute; inset-inline-end: 0.4rem; display: inline-grid; width: 2.25rem; height: 2.25rem; place-items: center; border: 0; border-radius: 0.55rem; color: var(--ink-muted); background: transparent; cursor: pointer; transition: color 180ms ease, background-color 180ms ease, transform 180ms ease; }
    .password-toggle:hover { color: var(--accent-strong); background: var(--accent-soft); transform: scale(1.04); }
    .password-toggle:focus-visible, .language-button:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .password-toggle svg { width: 1.15rem; height: 1.15rem; }
    .field-error, .auth-error-message { color: var(--danger); font-size: 0.82rem; line-height: 1.5; }
    .field-hint { color: var(--accent-strong); font-size: 0.78rem; font-weight: 650; }
    .auth-error { display: grid; gap: 0.35rem; }
    .auth-surface--failed .auth-error app-status-card { animation: auth-shake 360ms ease-out; }
    .dev-error-hint { margin: 0; color: var(--ink-muted); font-size: 0.78rem; line-height: 1.5; }
    .dev-api-status { margin: 0; color: var(--ink-muted); font-size: 0.78rem; font-weight: 650; }
    .dev-api-status.is-connected { color: var(--success); }
    .dev-api-status.is-unavailable { color: var(--danger); }
    .primary-button { display: inline-flex; min-height: 46px; align-items: center; justify-content: center; gap: 0.55rem; border: 1px solid color-mix(in srgb, var(--accent-action) 88%, var(--line)); border-radius: var(--radius-control); padding: 0.75rem 1rem; color: var(--action-text); background: var(--accent-action); box-shadow: 0 0.55rem 1.2rem color-mix(in srgb, var(--accent-action) 24%, transparent); font: 750 0.92rem/1 var(--font-sans); cursor: pointer; transition: transform 180ms ease, box-shadow 180ms ease, filter 180ms ease; }
    .sign-in-form .primary-button { animation: auth-reveal 440ms 430ms both; }
    .primary-button:hover:not(:disabled) { transform: translateY(-2px); box-shadow: 0 0.8rem 1.5rem color-mix(in srgb, var(--accent-action) 32%, transparent); filter: saturate(1.08); }
    .primary-button:active:not(:disabled) { transform: translateY(0) scale(0.985); }
    .primary-button:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .primary-button:disabled { cursor: wait; opacity: 0.72; }
    .button-spinner { width: 0.9rem; height: 0.9rem; border: 2px solid color-mix(in srgb, var(--action-text) 42%, transparent); border-block-start-color: var(--action-text); border-radius: 50%; animation: auth-spinner 700ms linear infinite; }
    .dev-account { display: flex; flex-wrap: wrap; align-items: center; gap: 0.35rem 0.55rem; margin: 0; border-inline-start: 3px solid var(--accent-action); border-radius: 0.45rem; padding: 0.6rem 0.75rem; color: var(--ink-muted); background: var(--surface-tint); font-size: 0.78rem; line-height: 1.5; }
    .dev-account code { color: var(--ink); font: 700 0.78rem/1.5 var(--font-mono, monospace); }
    .auth-form__foot { display: flex; justify-content: flex-end; padding-block-start: 0.15rem; }
    .language-button { display: inline-flex; align-items: center; gap: 0.4rem; border: 0; border-radius: 0.4rem; padding: 0.25rem; color: var(--ink-muted); background: transparent; font: 700 0.82rem/1.3 var(--font-sans); cursor: pointer; transition: color 180ms ease, background-color 180ms ease; }
    .language-button:hover { color: var(--accent-strong); background: var(--accent-soft); }
    .auth-surface--failed .auth-brand__mark { animation: auth-shake 360ms ease-out, auth-brand-float 5.4s 360ms ease-in-out infinite; }
    @media (min-width: 860px) { .auth-form { min-height: 31rem; } }
    @media (max-width: 480px) { .auth-shell { padding: 0.8rem; } .auth-surface { border-radius: 1.1rem; } .auth-brand { gap: 0.55rem; padding: 1.2rem 1rem; } .auth-brand__mark { min-height: 4.25rem; width: min(100%, 10rem); border-radius: 0.85rem; } .brand-logo { height: 4rem; width: min(100%, 9rem); } .auth-brand__copy { max-width: 18rem; font-size: 0.82rem; } .auth-form { padding: 1.25rem 1.05rem; } }
    @keyframes auth-ambient-drift { from { transform: translate3d(-1.5rem, -0.5rem, 0) scale(0.96); } to { transform: translate3d(2rem, 1.5rem, 0) scale(1.06); } }
    @keyframes auth-card-enter { from { opacity: 0; transform: translateY(1rem) scale(0.99); } to { opacity: 1; transform: translateY(0) scale(1); } }
    @keyframes auth-step-enter { from { opacity: 0; transform: translateY(0.65rem); } to { opacity: 1; transform: translateY(0); } }
    @keyframes auth-reveal { from { opacity: 0; transform: translateY(0.55rem); } to { opacity: 1; transform: translateY(0); } }
    @keyframes auth-brand-float { 0%, 100% { transform: translateY(0); box-shadow: 0 0.9rem 2.5rem color-mix(in srgb, var(--accent-action) 15%, transparent); } 50% { transform: translateY(-0.35rem); box-shadow: 0 1.2rem 2.8rem color-mix(in srgb, var(--accent-action) 25%, transparent); } }
    @keyframes auth-shake { 20%, 60% { transform: translateX(-0.3rem); } 40%, 80% { transform: translateX(0.3rem); } 100% { transform: translateX(0); } }
    @keyframes auth-spinner { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { .auth-shell::before, .auth-shell::after, .auth-surface, .auth-pane, .auth-brand__mark, .auth-brand__name, .auth-brand__eyebrow, .auth-brand__copy, .auth-error app-status-card, .button-spinner { animation: none !important; } *, *::before, *::after { scroll-behavior: auto !important; transition-duration: 0.01ms !important; } }
  `,
})
export class SignInComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly context = inject(ContextService);
  private readonly devApi = inject(DevelopmentApiIdentityService);
  private readonly router = inject(Router);
  private readonly theme = inject(ThemeService);
  readonly language = inject(LanguageService);
  readonly auth = this.authService;
  readonly clientDevelopment = isDevMode();
  readonly busy = signal(false);
  readonly step = signal<SignInStep>('credentials');
  readonly loginEntryMode = signal('NoAccess');
  readonly passwordVisible = signal(false);
  readonly capsLockOn = signal(false);
  readonly selectedContextId = signal('');
  readonly selectionError = signal('');
  readonly devApiStatus = this.devApi.status;
  readonly tenantContexts = computed(() => this.context.contexts().filter((candidate) => candidate.kind === 'OrdinaryMembership'));

  localizedDisplayName(candidate: FoundationContextCandidate): string {
    const arabicName = candidate.arabicDisplayName?.trim();
    return this.language.language() === 'ar' && arabicName ? arabicName : candidate.displayName;
  }
  readonly form = this.formBuilder.nonNullable.group({
    login: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  constructor() {
    this.form.controls.login.valueChanges.subscribe(() => this.authService.clearError());
    this.form.controls.password.valueChanges.subscribe(() => this.authService.clearError());
  }

  ngOnInit(): void {
    void this.context.loadEntry().then((entry) => this.loginEntryMode.set(entry?.entryMode ?? 'NoAccess'));
    if (this.clientDevelopment) {
      void this.devApi.check();
    }
    void this.theme.selectedTheme();
  }

  errorMessage(): string {
    const error = this.authService.lastError();
    if (!error) return '';
    if (this.authService.status() === 'expired') return this.language.text('sessionExpired');
    if (error.code === 'authentication_failed') return this.language.text('signInError');
    if (error.code === 'network_error') return this.language.text('networkError');
    return this.language.text('requestError');
  }

  showDevPasswordHint(): boolean {
    return this.context.entry()?.isDevelopment === true
      && this.authService.status() !== 'expired'
      && this.authService.lastError()?.code === 'authentication_failed';
  }

  developmentAccountHint(): string | null {
    const entry = this.context.entry();
    return entry?.isDevelopment ? entry.developmentAccountHint : null;
  }

  brandName(): string {
    return this.context.entry()?.branding.displayName || 'MESP';
  }

  brandLogoAlt(): string {
    const branding = this.context.entry()?.branding;
    return branding?.tenantConfigured ? branding.logoAltText : this.language.text('appName');
  }

  brandLogoUrl(): string {
    const branding = this.context.entry()?.branding;
    const configuredLogo = branding?.tenantConfigured
      ? this.theme.darkMode()
        ? branding.logoDarkUrl ?? branding.logoLightUrl
        : branding.logoLightUrl ?? branding.logoDarkUrl
      : null;
    return configuredLogo ?? (this.theme.darkMode()
      ? 'assets/Logo_4_3_BG_Removed_Dark.png'
      : 'assets/Logo_4_3_BG_Removed.png');
  }

  passwordDescription(): string | null {
    if (this.capsLockOn()) return 'caps-lock-warning';
    if (this.form.controls.password.touched && this.form.controls.password.invalid) return 'password-error';
    return null;
  }

  blockedByDevPreflight(): boolean {
    return this.clientDevelopment && this.devApiStatus() === 'unavailable';
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
    try {
      if (await this.authService.signIn(this.form.controls.login.value, this.form.controls.password.value)) {
        await this.navigateAfterSignIn();
      }
    } finally {
      this.busy.set(false);
    }
  }

  async chooseTenant(): Promise<void> {
    const contextId = this.selectedContextId();
    if (!contextId || !this.tenantContexts().some((candidate) => candidate.contextId === contextId)) {
      this.selectionError.set(this.language.text('selectTenantFirst'));
      return;
    }

    this.selectionError.set('');
    this.busy.set(true);
    try {
      if (await this.context.switchContext(contextId)) {
        await this.router.navigate(['/app']);
      } else {
        this.selectionError.set(this.language.text('accessDenied'));
      }
    } finally {
      this.busy.set(false);
    }
  }

  async signOutFromLogin(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await this.authService.signOut();
      if (result.outcome !== 'not-confirmed') {
        this.step.set('credentials');
        this.selectedContextId.set('');
        this.selectionError.set('');
        const entry = await this.context.loadEntry();
        this.loginEntryMode.set(entry?.entryMode ?? this.loginEntryMode());
      }
    } finally {
      this.busy.set(false);
    }
  }

  private async navigateAfterSignIn(): Promise<void> {
    const entry = await this.context.loadEntry();
    if (!entry || !(await this.authService.refreshSession())) {
      this.step.set('empty');
      this.selectionError.set(this.language.text('noTenantsForAccount'));
      return;
    }

    if (entry.entryMode === 'CommonHost') {
      await this.chooseCommonHostContext();
      return;
    }

    if (entry.entryMode === 'TenantHost') {
      const selected = this.authService.session()?.selectedContextId;
      if (selected) {
        await this.router.navigate(['/app']);
        return;
      }
      const contexts = await this.context.load();
      const exactTenantContext = contexts.find((candidate) =>
        candidate.kind === 'OrdinaryMembership' && candidate.tenantId === entry.candidateTenantId);
      if (exactTenantContext && await this.context.switchContext(exactTenantContext.contextId)) {
        await this.router.navigate(['/app']);
        return;
      }
    } else if (entry.entryMode === 'PlatformAdminHost'
      && this.authService.session()?.selectedContextId) {
      await this.router.navigate(['/app']);
      return;
    }

    this.step.set('empty');
    this.selectionError.set(this.language.text('noTenantsForAccount'));
  }

  private async chooseCommonHostContext(): Promise<void> {
    const contexts = await this.context.load();
    const tenants = this.tenantContexts();
    if (tenants.length === 0) {
      this.step.set('empty');
      this.selectionError.set('');
      return;
    }
    if (tenants.length > 1) {
      this.step.set('chooseTenant');
      this.selectedContextId.set('');
      return;
    }

    const onlyContext = tenants[0];
    if (!contexts.some((candidate) => candidate.contextId === onlyContext.contextId)) {
      this.step.set('empty');
      return;
    }
    if (this.authService.session()?.selectedContextId !== onlyContext.contextId
      && !(await this.context.switchContext(onlyContext.contextId))) {
      this.step.set('empty');
      this.selectionError.set(this.language.text('accessDenied'));
      return;
    }
    await this.router.navigate(['/app']);
  }
}
