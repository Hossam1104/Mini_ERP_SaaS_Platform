import { Component, ElementRef, HostListener, OnInit, ViewChild, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeName, ThemeService } from '../../core/presentation/theme.service';
import { BrandMarkComponent } from '../../shared/ui/brand-mark.component';
import { OperationalContextSwitcherComponent } from '../../shared/ui/operational-context-switcher.component';

@Component({
  selector: 'app-application-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandMarkComponent, OperationalContextSwitcherComponent],
  template: `
    <a class="skip-link" href="#main-content">{{ language.text('skipToContent') }}</a>
    <div class="shell" [class.shell--sidebar-expanded]="sidebarExpanded()">
      <aside id="app-sidebar" class="sidebar" [class.sidebar--expanded]="sidebarExpanded()" [class.sidebar--mobile-open]="mobileMenuOpen()" [attr.aria-label]="language.text('menu')">
        <a class="sidebar__brand" routerLink="/app" [attr.aria-label]="brandName()" (click)="closeMobileMenu()">
          @if (tenantLogoUrl()) {
            <img class="brand-mark brand-mark--tenant" [src]="tenantLogoUrl()" [alt]="context.entry()?.branding?.logoAltText || brandName()" />
          } @else {
            <app-brand-mark class="brand-mark" variant="icon" [theme]="theme.darkMode() ? 'dark' : 'light'" />
          }
          <span class="sidebar__brand-copy"><strong>{{ brandName() }}</strong><small>{{ language.text('appKicker') }}</small></span>
        </a>

        <nav class="sidebar__nav" [attr.aria-label]="language.text('menu')">
          @if (!context.entry() || (context.entry()?.entryMode !== 'PlatformAdminHost' && context.entry()?.entryMode !== 'NoAccess')) {
            <section class="nav-group" [attr.aria-label]="label('Overview', 'نظرة عامة')">
              <a class="nav-link" routerLink="/app" routerLinkActive="is-active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }" [attr.aria-label]="language.text('overview')" [title]="language.text('overview')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">⌂</span><span class="nav-label">{{ language.text('overview') }}</span>
              </a>
            </section>
            <section class="nav-group" [attr.aria-label]="label('Master data', 'البيانات الرئيسية')">
              <span class="nav-group__title">{{ language.text('masterData') }}</span>
              <a class="nav-link" routerLink="/app/master-data/categories" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('masterData')" [title]="language.text('masterData')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">◇</span><span class="nav-label">{{ language.text('masterData') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/price-lists" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('priceLists')" [title]="language.text('priceLists')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">＄</span><span class="nav-label">{{ language.text('priceLists') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/master-data/imports" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('importNavLabel')" [title]="language.text('importNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">⇧</span><span class="nav-label">{{ language.text('importNavLabel') }}</span>
              </a>
            </section>
            <section class="nav-group" [attr.aria-label]="label('Procurement', 'المشتريات')">
              <span class="nav-group__title">{{ label('Procurement', 'المشتريات') }}</span>
              <a class="nav-link" routerLink="/app/procurement/purchase-requests" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('purchaseRequestsNavLabel')" [title]="language.text('purchaseRequestsNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">↗</span><span class="nav-label">{{ language.text('purchaseRequestsNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/supplier-quotations" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('supplierQuotationsNavLabel')" [title]="language.text('supplierQuotationsNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">◈</span><span class="nav-label">{{ language.text('supplierQuotationsNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/purchase-orders" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('purchaseOrdersNavLabel')" [title]="language.text('purchaseOrdersNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">▣</span><span class="nav-label">{{ language.text('purchaseOrdersNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/goods-receipts" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('goodsReceiptsNavLabel')" [title]="language.text('goodsReceiptsNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">▤</span><span class="nav-label">{{ language.text('goodsReceiptsNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/supplier-returns" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('supplierReturnsNavLabel')" [title]="language.text('supplierReturnsNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">↶</span><span class="nav-label">{{ language.text('supplierReturnsNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/invoice-handoffs" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('invoiceHandoffsNavLabel')" [title]="language.text('invoiceHandoffsNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">⇥</span><span class="nav-label">{{ language.text('invoiceHandoffsNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/procurement/invoice-matching" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('invoiceMatchingNavLabel')" [title]="language.text('invoiceMatchingNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">⇄</span><span class="nav-label">{{ language.text('invoiceMatchingNavLabel') }}</span>
              </a>
            </section>
            <section class="nav-group" [attr.aria-label]="label('Operations', 'العمليات')">
              <span class="nav-group__title">{{ label('Operations', 'العمليات') }}</span>
              <a class="nav-link" routerLink="/app/inventory" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('inventoryNavLabel')" [title]="language.text('inventoryNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">▦</span><span class="nav-label">{{ language.text('inventoryNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/inventory/valuation" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="label('Inventory valuation', 'تقييم المخزون')" [title]="label('Inventory valuation', 'تقييم المخزون')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">◷</span><span class="nav-label">{{ label('Inventory valuation', 'تقييم المخزون') }}</span>
              </a>
            </section>
            <section class="nav-group" [attr.aria-label]="label('Finance and sales', 'المالية والمبيعات')">
              <span class="nav-group__title">{{ label('Finance and sales', 'المالية والمبيعات') }}</span>
              <a class="nav-link" routerLink="/app/finance" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="label('Finance', 'المالية')" [title]="label('Finance', 'المالية')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">ƒ</span><span class="nav-label">{{ label('Finance', 'المالية') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/sales/quotations" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="language.text('salesNavLabel')" [title]="language.text('salesNavLabel')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">↗</span><span class="nav-label">{{ language.text('salesNavLabel') }}</span>
              </a>
              <a class="nav-link" routerLink="/app/reporting" routerLinkActive="is-active" ariaCurrentWhenActive="page" [attr.aria-label]="label('Reporting', 'التقارير')" [title]="label('Reporting', 'التقارير')" (click)="closeMobileMenu()">
                <span class="nav-icon" aria-hidden="true">☷</span><span class="nav-label">{{ label('Reporting', 'التقارير') }}</span>
              </a>
            </section>
          }
        </nav>

        <div class="sidebar__footer">
          <span class="sidebar__help">{{ language.text('helpText') }}</span>
          <span class="release-chip">MESP · ERP · 01</span>
        </div>
      </aside>

      @if (mobileMenuOpen()) { <button class="mobile-nav-backdrop" type="button" [attr.aria-label]="label('Close navigation', 'إغلاق القائمة')" (click)="closeMobileMenu()"></button> }

      <div class="shell__body">
        <header class="topbar">
          <div class="topbar__start">
            <button class="icon-button sidebar-toggle desktop-toggle" type="button" (click)="toggleSidebar()" [attr.aria-label]="sidebarExpanded() ? label('Collapse navigation', 'طي القائمة') : label('Expand navigation', 'توسيع القائمة')" [attr.aria-expanded]="sidebarExpanded()" aria-controls="app-sidebar">☰</button>
            <button class="icon-button sidebar-toggle mobile-toggle" type="button" (click)="toggleMobileMenu()" [attr.aria-label]="mobileMenuOpen() ? label('Close navigation', 'إغلاق القائمة') : label('Open navigation', 'فتح القائمة')" [attr.aria-expanded]="mobileMenuOpen()" aria-controls="app-sidebar">☰</button>
            <a class="topbar__brand" routerLink="/app" [attr.aria-label]="brandName()">
              @if (tenantLogoUrl()) {
                <img class="topbar__tenant-logo" [src]="tenantLogoUrl()" [alt]="context.entry()?.branding?.logoAltText || brandName()" />
              } @else {
                <app-brand-mark class="topbar__mesp-logo" variant="logo" [theme]="theme.darkMode() ? 'dark' : 'light'" alt="MESP" />
              }
            </a>
            <nav class="breadcrumbs" [attr.aria-label]="label('Breadcrumb', 'مسار التنقل')">
              <a routerLink="/app">{{ language.text('overview') }}</a><span aria-hidden="true">›</span><span aria-current="page">{{ currentPage() }}</span>
            </nav>
          </div>

          <div class="topbar__actions">
            <div class="tenant-context"><span class="eyebrow">{{ language.text('currentTenant') }}</span><strong>{{ contextLabel() }}</strong></div>
            <app-operational-context-switcher />
            <a class="context-management-link" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
            <div class="theme-control">
              <button #themeTrigger id="theme-trigger" class="theme-trigger" type="button" (click)="toggleThemeMenu()" aria-haspopup="menu" [attr.aria-controls]="themeMenuOpen() ? 'theme-menu' : null" [attr.aria-expanded]="themeMenuOpen()">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3.5a8.5 8.5 0 1 0 0 17h1.1a2.2 2.2 0 0 0 1.4-3.9 1.2 1.2 0 0 1 .8-2.1h1.2A4 4 0 0 0 20.5 10c0-3.6-3.8-6.5-8.5-6.5Z"/><circle cx="7.8" cy="10" r="1"/><circle cx="11" cy="7.2" r="1"/><circle cx="15" cy="8" r="1"/></svg>
                <span>{{ label('Themes', 'المظاهر') }}</span><span class="chevron" aria-hidden="true">⌄</span>
              </button>
              @if (themeMenuOpen()) {
                <div #themeMenu id="theme-menu" class="theme-menu" role="menu" [attr.aria-label]="label('Choose a theme', 'اختر مظهراً')" (keydown)="onThemeMenuKeydown($event)">
                  @for (option of theme.options; track option.id) {
                    <button class="theme-option" type="button" role="menuitemradio" [attr.aria-checked]="theme.selectedTheme() === option.id" [attr.tabindex]="themeMenuFocus() === option.id ? 0 : -1" (click)="selectTheme(option.id)">
                      <span class="theme-swatch" [style.backgroundColor]="option.color" aria-hidden="true"></span><span class="theme-option__label">{{ option.label }}</span>
                      @if (theme.selectedTheme() === option.id) { <span class="theme-check" aria-hidden="true">✓</span> }
                    </button>
                  }
                </div>
              }
            </div>
            <button class="icon-button scheme-toggle" type="button" (click)="theme.toggleDarkMode()" [attr.aria-label]="theme.darkMode() ? label('Switch to light mode', 'التبديل إلى الوضع الفاتح') : label('Switch to dark mode', 'التبديل إلى الوضع الداكن')" [attr.aria-pressed]="theme.darkMode()" [title]="theme.darkMode() ? label('Light mode', 'الوضع الفاتح') : label('Dark mode', 'الوضع الداكن')">
              <span aria-hidden="true">{{ theme.darkMode() ? '☼' : '◐' }}</span>
            </button>
            <button class="icon-button notification-button" type="button" [attr.aria-label]="label('Notifications', 'الإشعارات')" [title]="label('Notifications', 'الإشعارات')"><span aria-hidden="true">♧</span></button>
            <button class="language-button" type="button" (click)="language.toggle()" [attr.aria-label]="language.language() === 'en' ? 'Language: switch to Arabic' : 'اللغة: التبديل إلى الإنجليزية'">
              <span class="language-button__globe" aria-hidden="true">文</span><span>{{ language.language() === 'en' ? 'EN' : 'عربي' }}</span>
            </button>
            <a class="context-management-link context-management-link--mobile" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
            <div class="user-pill">
              <span class="user-avatar" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="M12 12a4.3 4.3 0 1 0 0-8.6 4.3 4.3 0 0 0 0 8.6Zm0 2c-4.5 0-7.8 2.3-7.8 5.4v1.2h15.6v-1.2c0-3.1-3.3-5.4-7.8-5.4Z"/></svg></span>
              <span class="user-pill__label">{{ label('Account', 'الحساب') }}</span>
              <button class="sign-out" type="button" (click)="signOut()" [disabled]="auth.signingOut()" [attr.aria-describedby]="auth.signOutFailed() ? 'sign-out-feedback' : null">{{ auth.signingOut() ? language.text('signingOut') : language.text('signOut') }}</button>
            </div>
          </div>
        </header>

        @if (auth.signOutFailed()) {
          <div id="sign-out-feedback" class="sign-out-feedback" role="alert" aria-live="assertive">{{ language.text('signOutFailed') }}</div>
        }

        <main id="main-content" class="shell__content">
          <section class="content-grid__main"><router-outlet /></section>
        </main>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; min-height: 100dvh; }
    .skip-link { position: fixed; z-index: 90; inset-block-start: .75rem; inset-inline-start: .75rem; transform: translateY(-200%); border-radius: var(--radius-control); padding: .7rem .9rem; color: var(--action-text); background: var(--accent-action); font-weight: 700; }
    .skip-link:focus { transform: translateY(0); }
    .shell { min-height: 100dvh; background: var(--canvas); }
    .sidebar { position: fixed; z-index: 38; inset-block-start: var(--header-height); inset-inline-start: 0; inset-block-end: 0; display: flex; width: var(--sidebar-collapsed); flex-direction: column; gap: .8rem; overflow-x: hidden; overflow-y: auto; padding: 1rem .55rem; border-inline-end: 1px solid color-mix(in srgb, var(--accent) 12%, var(--line)); background: var(--surface-glass); box-shadow: var(--shadow-glass); backdrop-filter: blur(18px) saturate(145%); transition: width var(--motion-slow) ease, padding var(--motion-slow) ease; }
    .sidebar--expanded { width: var(--sidebar-expanded); padding-inline: .85rem; }
    .sidebar__brand { display: flex; min-height: 3.5rem; align-items: center; justify-content: flex-start; gap: .7rem; padding: .2rem .25rem .6rem; color: var(--ink); text-decoration: none; }
    .brand-mark { display: block; width: 2.5rem; height: 2.5rem; flex: none; }
    .brand-mark--tenant { width: 2.7rem; height: 2.7rem; object-fit: contain; }
    .sidebar__brand-copy { display: none; min-width: 0; }
    .sidebar__brand-copy strong, .sidebar__brand-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sidebar__brand-copy strong { color: var(--ink); font: 800 1rem/1.15 var(--font-display); }
    .sidebar__brand-copy small { margin-block-start: .2rem; color: var(--ink-muted); font-size: .78rem; }
    .sidebar--expanded .sidebar__brand-copy, .sidebar--mobile-open .sidebar__brand-copy { display: block; }
    .sidebar__nav { display: grid; align-content: start; gap: .5rem; }
    .nav-group { display: grid; gap: .22rem; }
    .nav-group__title { display: none; padding: .6rem .75rem .2rem; color: var(--ink-muted); font-size: .76rem; font-weight: 800; letter-spacing: .05em; }
    .sidebar--expanded .nav-group__title, .sidebar--mobile-open .nav-group__title { display: block; }
    .nav-link { position: relative; display: flex; min-height: 2.9rem; align-items: center; justify-content: center; gap: .75rem; border: 1px solid transparent; border-radius: 14px; padding: .5rem; color: var(--ink-muted); font-size: .92rem; font-weight: 700; text-decoration: none; transition: color var(--motion-fast) ease, background var(--motion-fast) ease, border-color var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .nav-link:hover { border-color: color-mix(in srgb, var(--accent) 18%, var(--line)); color: var(--accent); background: var(--accent-soft); transform: translateY(-1px); }
    .nav-link.is-active { border-color: color-mix(in srgb, var(--accent) 20%, var(--line)); color: var(--accent-strong); background: var(--accent-soft); box-shadow: 0 4px 12px color-mix(in srgb, var(--accent) 10%, transparent); }
    .nav-link.is-active::before { position: absolute; inset-block: .45rem; inset-inline-start: 0; width: 3px; border-radius: 3px; background: var(--accent-action); content: ''; }
    .nav-icon { display: inline-grid; width: 2rem; height: 2rem; place-items: center; flex: none; border-radius: 11px; color: var(--accent); background: color-mix(in srgb, var(--accent-soft) 80%, transparent); font-size: 1.15rem; }
    .nav-label { display: none; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sidebar--expanded .nav-link, .sidebar--mobile-open .nav-link { justify-content: flex-start; padding-inline: .55rem .75rem; }
    .sidebar--expanded .nav-label, .sidebar--mobile-open .nav-label { display: block; }
    .sidebar__footer { display: grid; gap: .55rem; margin-block-start: auto; border-block-start: 1px solid var(--line); padding: .8rem .25rem .1rem; text-align: center; }
    .sidebar__help { display: none; color: var(--ink-muted); font-size: .78rem; }
    .sidebar--expanded .sidebar__help, .sidebar--mobile-open .sidebar__help { display: block; }
    .release-chip { display: inline-flex; justify-content: center; border: 1px solid var(--line); border-radius: 99px; padding: .25rem .4rem; color: var(--ink-muted); font-size: .66rem; font-weight: 700; }
    .sidebar--expanded .release-chip { padding: .3rem .5rem; }
    .shell__body { min-width: 0; min-height: 100dvh; margin-inline-start: var(--sidebar-collapsed); padding-block-start: var(--header-height); transition: margin-inline-start var(--motion-slow) ease; }
    .shell--sidebar-expanded .shell__body { margin-inline-start: var(--sidebar-expanded); }
    .topbar { position: fixed; z-index: 50; inset-block-start: 0; inset-inline: 0; display: flex; min-height: var(--header-height); align-items: center; justify-content: space-between; gap: .8rem; border-block-end: 1px solid color-mix(in srgb, var(--accent) 14%, var(--line)); padding-inline: clamp(.75rem, 1.7vw, 1.5rem); color: var(--ink); background: var(--surface-glass); box-shadow: var(--shadow-glass); backdrop-filter: blur(20px) saturate(155%); }
    .topbar__start, .topbar__actions { display: flex; min-width: 0; align-items: center; gap: .65rem; }
    .topbar__brand { display: flex; width: 5.8rem; align-items: center; justify-content: center; flex: none; }
    .topbar__mesp-logo { display: block; width: 5.4rem; max-height: 2.8rem; }
    .topbar__tenant-logo { display: block; max-width: 6rem; max-height: 2.8rem; object-fit: contain; }
    .breadcrumbs { display: flex; min-width: 0; align-items: center; gap: .5rem; color: var(--ink-muted); font-size: .86rem; white-space: nowrap; }
    .breadcrumbs a { color: var(--ink-muted); text-decoration: none; }
    .breadcrumbs a:hover, .breadcrumbs [aria-current='page'] { color: var(--accent); }
    .topbar__actions { justify-content: flex-end; gap: .5rem; }
    .tenant-context { display: grid; max-width: 14rem; gap: .05rem; }
    .tenant-context .eyebrow { color: var(--ink-muted); font-size: .68rem; font-weight: 700; }
    .tenant-context strong { overflow: hidden; color: var(--ink); font-size: .87rem; font-weight: 800; text-overflow: ellipsis; white-space: nowrap; }
    .context-management-link { color: var(--ink-muted); font-size: .82rem; font-weight: 700; text-decoration: none; white-space: nowrap; }
    .context-management-link:hover { color: var(--accent); }
    .context-management-link--mobile { display: none; }
    .icon-button, .language-button, .theme-trigger { display: inline-flex; min-height: 42px; align-items: center; justify-content: center; gap: .5rem; border: 1px solid var(--line); border-radius: 14px; padding: .45rem .7rem; color: var(--accent); background: var(--surface-raised); box-shadow: 0 3px 9px rgb(15 26 48 / 6%); font: 700 .9rem/1.1 var(--font-sans); transition: border-color var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease, background var(--motion-fast) ease; }
    .icon-button.mobile-toggle { display: none; }
    .icon-button { width: 42px; padding: 0; font-size: 1.15rem; }
    .icon-button:hover, .language-button:hover, .theme-trigger:hover { border-color: var(--accent); box-shadow: 0 6px 14px color-mix(in srgb, var(--accent) 14%, transparent); transform: translateY(-1px); }
    .theme-control { position: relative; }
    .theme-trigger { border-radius: 999px; padding-inline: .85rem; }
    .theme-trigger svg { width: 18px; height: 18px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
    .theme-trigger svg circle { fill: currentColor; stroke: none; }
    .chevron { color: var(--ink-muted); font-size: 1rem; }
    .theme-menu { position: absolute; z-index: 80; inset-block-start: calc(100% + .65rem); inset-inline-end: 0; display: grid; width: 264px; gap: .25rem; border: 1px solid color-mix(in srgb, var(--accent) 20%, var(--line)); border-radius: 18px; padding: .55rem; background: var(--surface-glass); box-shadow: var(--shadow-overlay); backdrop-filter: blur(22px) saturate(160%); animation: menu-enter 150ms ease both; }
    .theme-option { display: flex; min-height: 42px; align-items: center; gap: .75rem; border: 1px solid transparent; border-radius: 12px; padding: .45rem .6rem; color: var(--ink); background: transparent; text-align: start; font: 700 .9rem/1.2 var(--font-sans); }
    .theme-option:hover, .theme-option:focus-visible { border-color: color-mix(in srgb, var(--accent) 20%, var(--line)); background: var(--accent-soft); }
    .theme-swatch { width: 15px; height: 15px; flex: none; border: 1px solid rgb(0 0 0 / 12%); border-radius: 50%; box-shadow: 0 2px 6px rgb(0 0 0 / 16%); }
    .theme-option__label { flex: 1; }
    .theme-check { color: var(--accent); font-size: 1.1rem; }
    .language-button { border-radius: 999px; color: var(--ink); }
    .language-button__globe { color: var(--accent); font-size: 1rem; }
    .user-pill { display: inline-flex; min-height: 42px; align-items: center; gap: .45rem; border: 1px solid var(--line); border-radius: 999px; padding: .22rem .45rem; background: var(--surface-raised); box-shadow: 0 3px 10px rgb(15 26 48 / 5%); }
    .user-avatar { display: grid; width: 30px; height: 30px; place-items: center; border-radius: 50%; color: var(--action-text); background: var(--accent-action); font-size: .8rem; font-weight: 800; }
    .user-avatar svg { width: 16px; height: 16px; fill: currentColor; }
    .user-pill__label { color: var(--ink); font-size: .78rem; font-weight: 700; }
    .sign-out { border: 0; border-inline-start: 1px solid var(--line); padding-inline-start: .55rem; color: var(--ink-muted); background: transparent; font: 700 .8rem/1 var(--font-sans); }
    .sign-out:hover { color: var(--accent); }
    .sign-out:disabled { color: var(--ink-muted); cursor: wait; opacity: .65; }
    .mobile-nav-backdrop { display: none; }
    .sign-out-feedback { margin: 1rem 1.5rem 0; border: 1px solid color-mix(in srgb, var(--danger) 38%, var(--line)); border-radius: var(--radius-control); padding: .8rem 1rem; color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--surface-raised)); font-size: .9rem; line-height: 1.5; }
    .shell__content { min-width: 0; padding: clamp(1rem, 2.8vw, 2.25rem); }
    .content-grid__main { width: min(100%, 94rem); min-width: 0; margin-inline: auto; }
    @keyframes menu-enter { from { opacity: 0; transform: translateY(-4px) scale(.98); } to { opacity: 1; transform: translateY(0) scale(1); } }
    @media (max-width: 1100px) { .tenant-context { display: none; } .topbar { gap: .5rem; } .context-management-link { display: none; } .context-management-link--mobile { display: inline-flex; } }
    @media (max-width: 760px) {
      :host { --header-height: 112px; }
      .topbar { min-height: var(--header-height); align-items: stretch; flex-direction: column; justify-content: center; gap: .25rem; padding-block: .45rem; }
      .topbar__start { min-height: 42px; justify-content: space-between; }
      .topbar__actions { min-height: 42px; justify-content: space-between; gap: .35rem; }
      .topbar__brand { width: 4.4rem; }
      .topbar__mesp-logo { width: 4rem; }
      .breadcrumbs { margin-inline-start: auto; font-size: .8rem; }
      .desktop-toggle { display: none; }
      .icon-button.mobile-toggle { display: inline-flex; }
      .sidebar { display: none; }
      .sidebar--mobile-open { position: fixed; z-index: 70; inset-block-start: var(--header-height); inset-inline-start: 0; inset-block-end: 0; display: flex; width: min(var(--sidebar-expanded), 88vw); padding-inline: .85rem; }
      .sidebar--mobile-open .sidebar__brand { position: relative; isolation: isolate; overflow: hidden; min-height: 4.1rem; border: 1px solid color-mix(in srgb, var(--accent) 18%, var(--line)); border-radius: var(--radius-md); padding: .4rem .5rem; background: linear-gradient(115deg, color-mix(in srgb, var(--accent-soft) 72%, var(--surface-glass)), var(--surface-glass)); box-shadow: var(--shadow-soft); }
      .sidebar--mobile-open .sidebar__brand::before { position: absolute; z-index: 0; inset-block-start: 0; inset-inline: 0; height: 100%; background: linear-gradient(110deg, color-mix(in srgb, var(--accent) 7%, transparent), transparent 76%); content: ''; pointer-events: none; }
      .sidebar--mobile-open .sidebar__brand::after { position: absolute; z-index: 1; inset-block-start: 2.8rem; inset-inline: -5%; height: 1.5rem; border-radius: 50% 50% 0 0 / 58% 58% 0 0; background: color-mix(in srgb, var(--accent) 9%, transparent); content: ''; pointer-events: none; }
      .sidebar--mobile-open .sidebar__brand > * { position: relative; z-index: 2; }
      .sidebar--mobile-open .sidebar__brand-copy, .sidebar--mobile-open .nav-group__title, .sidebar--mobile-open .nav-label, .sidebar--mobile-open .sidebar__help { display: block; }
      .sidebar--mobile-open .nav-link { justify-content: flex-start; padding-inline: .55rem .75rem; }
      .mobile-nav-backdrop { position: fixed; z-index: 60; inset-block-start: var(--header-height); inset-inline: 0; inset-block-end: 0; display: block; border: 0; background: rgb(9 15 26 / 38%); backdrop-filter: blur(4px); }
      .shell__body, .shell--sidebar-expanded .shell__body { margin-inline: 0; }
      .shell__content { padding: 1rem .75rem; }
      .theme-trigger { min-width: 42px; padding-inline: .55rem; }
      .theme-trigger span:not(.chevron) { font-size: .82rem; }
      .user-pill__label { display: none; }
      .user-pill { gap: .25rem; padding-inline: .25rem; }
      .sign-out { padding-inline-start: .35rem; font-size: .75rem; }
      .notification-button { display: none; }
    }
    @media (max-width: 420px) {
      .topbar__actions { gap: .25rem; }
      .icon-button, .language-button, .theme-trigger { min-height: 38px; }
      .icon-button { width: 38px; }
      .language-button { padding-inline: .45rem; }
      .theme-trigger { gap: .3rem; }
      .context-management-link--mobile { display: none; }
      .theme-menu { width: min(264px, calc(100vw - 24px)); }
      .breadcrumbs { gap: .3rem; font-size: .74rem; }
    }
    @supports not (backdrop-filter: blur(4px)) { .sidebar, .topbar, .theme-menu { background: var(--surface-raised); } .mobile-nav-backdrop { background: rgb(9 15 26 / 58%); } }
  `,
})
export class ApplicationShellComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  readonly sidebarExpanded = signal(false);
  readonly mobileMenuOpen = signal(false);
  readonly themeMenuOpen = signal(false);
  readonly themeMenuFocus = signal<ThemeName>('sapphire');
  private themeMenuElement?: HTMLElement;

  @ViewChild('themeTrigger') private themeTrigger?: ElementRef<HTMLButtonElement>;
  @ViewChild('themeMenu') set themeMenu(value: ElementRef<HTMLElement> | undefined) {
    this.themeMenuElement = value?.nativeElement;
    if (this.themeMenuElement) {
      queueMicrotask(() => this.themeMenuElement?.querySelector<HTMLElement>('[tabindex="0"]')?.focus());
    }
  }

  ngOnInit(): void {
    if (this.context.contexts().length === 0 && !this.context.entry()) {
      void this.context.loadEntry();
    }
  }

  @HostListener('document:click', ['$event'])
  closeThemeMenuOnOutsideClick(event: MouseEvent): void {
    const target = event.target;
    if (!this.themeMenuOpen() || !(target instanceof Node)) return;
    if (this.themeMenuElement?.contains(target) || this.themeTrigger?.nativeElement.contains(target)) return;
    this.themeMenuOpen.set(false);
  }

  @HostListener('document:keydown', ['$event'])
  closeOverlaysOnEscape(event: KeyboardEvent): void {
    if (event.key !== 'Escape') return;
    if (this.themeMenuOpen()) {
      event.preventDefault();
      this.closeThemeMenu(true);
    }
    if (this.mobileMenuOpen()) this.closeMobileMenu();
  }

  toggleThemeMenu(): void {
    if (this.themeMenuOpen()) {
      this.closeThemeMenu();
      return;
    }
    this.themeMenuFocus.set(this.theme.selectedTheme());
    this.themeMenuOpen.set(true);
  }

  onThemeMenuKeydown(event: KeyboardEvent): void {
    const menu = event.currentTarget as HTMLElement;
    const items = Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitemradio"]'));
    const index = items.indexOf(event.target as HTMLElement);
    let nextIndex: number | null = null;
    if (event.key === 'ArrowDown' || event.key === 'ArrowRight') nextIndex = (index + 1 + items.length) % items.length;
    if (event.key === 'ArrowUp' || event.key === 'ArrowLeft') nextIndex = (index - 1 + items.length) % items.length;
    if (event.key === 'Home') nextIndex = 0;
    if (event.key === 'End') nextIndex = items.length - 1;
    if (nextIndex !== null) {
      event.preventDefault();
      const option = this.theme.options[nextIndex];
      if (option) this.themeMenuFocus.set(option.id);
      items[nextIndex]?.focus();
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeThemeMenu(true);
    }
  }

  selectTheme(theme: ThemeName): void {
    this.theme.select(theme);
    this.closeThemeMenu(true);
  }

  closeThemeMenu(restoreFocus = false): void {
    this.themeMenuOpen.set(false);
    if (restoreFocus) queueMicrotask(() => this.themeTrigger?.nativeElement.focus());
  }

  closeMobileMenu(): void { this.mobileMenuOpen.set(false); }

  toggleSidebar(): void { this.sidebarExpanded.update((expanded) => !expanded); }
  toggleMobileMenu(): void { this.mobileMenuOpen.update((open) => !open); }

  label(english: string, arabic: string): string {
    return this.language.language() === 'ar' ? arabic : english;
  }

  currentPage(): string {
    const url = this.router.url.split(/[?#]/)[0] ?? '/app';
    if (url === '/app' || url === '/app/') return this.language.text('overview');
    if (url.includes('/master-data')) return this.language.text('masterData');
    if (url.includes('/price-lists')) return this.language.text('priceLists');
    if (url.includes('/procurement/purchase-requests')) return this.language.text('purchaseRequestsNavLabel');
    if (url.includes('/procurement/supplier-quotations')) return this.language.text('supplierQuotationsNavLabel');
    if (url.includes('/procurement/purchase-orders')) return this.language.text('purchaseOrdersNavLabel');
    if (url.includes('/procurement/goods-receipts')) return this.language.text('goodsReceiptsNavLabel');
    if (url.includes('/procurement/supplier-returns')) return this.language.text('supplierReturnsNavLabel');
    if (url.includes('/procurement/invoice-handoffs')) return this.language.text('invoiceHandoffsNavLabel');
    if (url.includes('/procurement/invoice-matching')) return this.language.text('invoiceMatchingNavLabel');
    if (url.includes('/inventory/valuation')) return this.label('Inventory valuation', 'تقييم المخزون');
    if (url.includes('/inventory')) return this.language.text('inventoryNavLabel');
    if (url.includes('/finance')) return this.label('Finance', 'المالية');
    if (url.includes('/sales')) return this.language.text('salesNavLabel');
    if (url.includes('/reporting')) return this.label('Reporting', 'التقارير');
    if (url.includes('/workspaces')) return this.language.text('manageContexts');
    return this.language.text('overview');
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

  tenantLogoUrl(): string | null {
    const branding = this.context.entry()?.branding;
    return this.theme.darkMode()
      ? branding?.logoDarkUrl ?? branding?.logoLightUrl ?? null
      : branding?.logoLightUrl ?? branding?.logoDarkUrl ?? null;
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
  }
}
