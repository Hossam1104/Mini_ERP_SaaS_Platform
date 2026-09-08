import { Component, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { BrandMarkComponent } from '../../shared/ui/brand-mark.component';
import { OperationalContextSwitcherComponent } from '../../shared/ui/operational-context-switcher.component';

@Component({
  selector: 'app-application-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandMarkComponent, OperationalContextSwitcherComponent],
  template: `
    <a class="skip-link" href="#main-content">{{ language.text('skipToContent') }}</a>
    <div class="shell">
      <aside class="sidebar" [attr.aria-label]="language.text('menu')">
        <div class="sidebar__brand">
          @if (context.entry()?.branding?.logoLightUrl || context.entry()?.branding?.logoDarkUrl) {
            <picture class="brand-mark brand-mark--tenant">
              <source [srcset]="context.entry()?.branding?.logoDarkUrl ?? context.entry()?.branding?.logoLightUrl" media="(prefers-color-scheme: dark)" />
              <img [src]="context.entry()?.branding?.logoLightUrl ?? context.entry()?.branding?.logoDarkUrl" [alt]="context.entry()?.branding?.logoAltText ?? ''" />
            </picture>
          } @else {
            <app-brand-mark class="brand-mark" variant="icon" theme="dark" />
          }
          <div>
            <strong>{{ brandName() }}</strong>
            <small>{{ language.text('appKicker') }}</small>
          </div>
        </div>
        <nav class="sidebar__nav">
          @if (!context.entry() || (context.entry()?.entryMode !== 'PlatformAdminHost' && context.entry()?.entryMode !== 'NoAccess')) {
          <a routerLink="/app" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }">
            <span class="nav-icon" aria-hidden="true">⌂</span>{{ language.text('overview') }}
          </a>
          <a routerLink="/app/master-data/categories" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">◆</span>{{ language.text('masterData') }}
          </a>
          <a routerLink="/app/price-lists" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">$</span>{{ language.text('priceLists') }}
          </a>
          <a routerLink="/app/inventory" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">▦</span>{{ language.text('inventoryNavLabel') }}
          </a>
          <a routerLink="/app/finance" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">ƒ</span>{{ language.language() === 'ar' ? 'المالية' : 'Finance' }}
          </a>
          <a routerLink="/app/reporting" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">⌁</span>{{ language.language() === 'ar' ? '\\u0627\\u0644\\u062a\\u0642\\u0627\\u0631\\u064a\\u0631' : 'Reporting' }}
          </a>
          <a routerLink="/app/sales/quotations" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">↗</span>{{ language.text('salesNavLabel') }}
          </a>
          <a routerLink="/app/procurement/purchase-requests" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">↗</span>{{ language.text('purchaseRequestsNavLabel') }}
          </a>
          <a routerLink="/app/procurement/supplier-quotations" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">◇</span>{{ language.text('supplierQuotationsNavLabel') }}
          </a>
          <a routerLink="/app/procurement/purchase-orders" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">▣</span>{{ language.text('purchaseOrdersNavLabel') }}
          </a>
          <a routerLink="/app/procurement/goods-receipts" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">◫</span>{{ language.text('goodsReceiptsNavLabel') }}
          </a>
          <a routerLink="/app/procurement/supplier-returns" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">↶</span>{{ language.text('supplierReturnsNavLabel') }}
          </a>
          <a routerLink="/app/procurement/invoice-handoffs" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">▤</span>{{ language.text('invoiceHandoffsNavLabel') }}
          </a>
          <a routerLink="/app/procurement/invoice-matching" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">≋</span>{{ language.text('invoiceMatchingNavLabel') }}
          </a>
          <a routerLink="/app/master-data/imports" routerLinkActive="is-active">
            <span class="nav-icon" aria-hidden="true">↪</span>{{ language.text('importNavLabel') }}
          </a>
          }
        </nav>
        <div class="sidebar__footer">
          <p>{{ language.text('helpText') }}</p>
          <span class="release-chip">Foundation / 01</span>
        </div>
      </aside>

      <div class="shell__body">
        <header class="topbar">
          <div>
            <p class="eyebrow">{{ language.text('currentTenant') }}</p>
            <p class="topbar__context">{{ contextLabel() }}</p>
          </div>
          <div class="topbar__actions">
            <app-operational-context-switcher />
            <a class="context-management-link" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
            <button class="language-button" type="button" (click)="language.toggle()" [attr.aria-label]="language.text('language')">
              <span aria-hidden="true">◐</span>{{ language.language() === 'en' ? language.text('switchToArabic') : language.text('switchToEnglish') }}
            </button>
            <button
              class="sign-out"
              type="button"
              (click)="signOut()"
              [disabled]="auth.signingOut()"
              [attr.aria-describedby]="auth.signOutFailed() ? 'sign-out-feedback' : null"
            >
              {{ auth.signingOut() ? language.text('signingOut') : language.text('signOut') }}
            </button>
          </div>
        </header>

        @if (auth.signOutFailed()) {
          <div id="sign-out-feedback" class="sign-out-feedback" role="alert" aria-live="assertive">
            {{ language.text('signOutFailed') }}
          </div>
        }

        <main id="main-content" class="shell__content">
          <section class="content-grid__main">
            <router-outlet />
          </section>
        </main>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; min-height: 100dvh; }
    .skip-link { position: fixed; z-index: 10; inset-block-start: 0.75rem; inset-inline-start: 0.75rem; transform: translateY(-200%); padding: 0.65rem 0.8rem; border-radius: 0.5rem; color: #fff; background: var(--ink); }
    .skip-link:focus { transform: translateY(0); }
    .shell { display: grid; grid-template-columns: 16rem minmax(0, 1fr); min-height: 100dvh; background: var(--canvas); }
    .sidebar { display: flex; flex-direction: column; padding: 1.35rem 1rem; color: #eff5f2; background: var(--ink); }
    .sidebar__brand { display: flex; align-items: center; gap: 0.7rem; padding: 0.25rem 0.35rem 2.4rem; }
    .brand-mark { width: 2.25rem; flex: none; }
    .brand-mark--tenant { display: block; height: 2.25rem; }
    .brand-mark--tenant img { display: block; width: 100%; height: 100%; object-fit: contain; }
    .sidebar__brand strong, .sidebar__brand small { display: block; }
    .sidebar__brand strong { color: #fff; font: 700 0.9rem/1.2 var(--font-display); }
    .sidebar__brand small { margin-top: 0.2rem; color: #9eb2ab; font-size: 0.67rem; }
    .sidebar__nav { display: grid; gap: 0.35rem; }
    .sidebar__nav a { display: flex; align-items: center; gap: 0.65rem; border-radius: 0.65rem; padding: 0.72rem 0.65rem; color: #a7bab3; font-size: 0.82rem; font-weight: 700; text-decoration: none; }
    .sidebar__nav a:hover, .sidebar__nav a.is-active { color: #fff; background: #253c3a; }
    .sidebar__nav a.is-active { box-shadow: inset 0.18rem 0 0 var(--accent); }
    .nav-icon { display: inline-grid; place-items: center; width: 1.25rem; color: var(--accent); font-size: 1rem; }
    .sidebar__footer { margin-top: auto; padding: 1rem 0.35rem 0; border-top: 1px solid #29403d; }
    .sidebar__footer p { margin: 0 0 0.75rem; color: #8da49c; font-size: 0.72rem; line-height: 1.5; }
    .release-chip { display: inline-flex; border: 1px solid #3e5952; border-radius: 99px; padding: 0.28rem 0.52rem; color: #a9c6bc; font-size: 0.65rem; font-weight: 800; letter-spacing: 0.06em; text-transform: uppercase; }
    .shell__body { min-width: 0; }
    .topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; min-height: 4.6rem; padding: 0 2.5rem; border-bottom: 1px solid var(--line); background: var(--surface-raised); }
    .eyebrow { margin: 0 0 0.18rem; color: var(--ink-muted); font-size: 0.68rem; font-weight: 800; letter-spacing: 0.12em; text-transform: uppercase; }
    .topbar__context { margin: 0; color: var(--ink); font: 700 1rem/1.2 var(--font-display); }
    .topbar__actions { display: flex; align-items: center; gap: 0.85rem; }
    .context-management-link { color: var(--ink-muted); font-size: 0.75rem; font-weight: 750; text-decoration: none; }
    .context-management-link:hover { color: var(--accent-strong); }
    .language-button, .sign-out { border: 0; color: var(--ink-muted); background: transparent; font: 700 0.78rem/1 var(--font-sans); cursor: pointer; }
    .sign-out { border-inline-start: 1px solid var(--line); padding-inline-start: 1rem; }
    .language-button:hover, .sign-out:hover { color: var(--accent-strong); }
    .sign-out:disabled { color: var(--ink-muted); cursor: wait; opacity: 0.65; }
    .sign-out-feedback { margin: 1rem 2.5rem 0; border: 1px solid color-mix(in srgb, var(--danger) 38%, var(--line)); border-radius: 0.7rem; padding: 0.8rem 1rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: 0.85rem; line-height: 1.5; }
    .shell__content { padding: clamp(1.25rem, 3vw, 2.5rem); }
    .content-grid__main { width: min(100%, 78rem); margin: 0 auto; }
    @media (max-width: 860px) { .shell { grid-template-columns: 4.6rem minmax(0, 1fr); } .sidebar__brand div, .sidebar__nav a:not(.is-active)::after, .sidebar__nav a { justify-content: center; } .sidebar__brand div { display: none; } .sidebar__nav a { padding-inline: 0; font-size: 0; } .nav-icon { font-size: 1.05rem; } .sidebar__footer { display: none; } .topbar { padding-inline: 1.25rem; } }
    @media (max-width: 700px) { .topbar { align-items: flex-start; flex-direction: column; padding-block: 1rem; } .topbar__actions { width: 100%; justify-content: space-between; flex-wrap: wrap; } .shell__content { padding: 1rem; } }
  `,
})
export class ApplicationShellComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  ngOnInit(): void {
    if (this.context.contexts().length === 0 && !this.context.entry()) {
      void this.context.loadEntry();
    }
  }

  contextLabel(): string {
    return this.context.entry()?.candidateTenantDisplayName
      ?? this.context.currentContext()?.displayName
      ?? (this.auth.session()?.selectedPath === 'PlatformGovernanceContext'
        ? this.language.text('platformGovernance')
        : this.language.text('noTenantContext'));
  }

  brandName(): string {
    return this.context.entry()?.branding.displayName ?? this.language.text('appName');
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
  }
}
