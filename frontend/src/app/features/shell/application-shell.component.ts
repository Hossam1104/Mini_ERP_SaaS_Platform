import { Component, ElementRef, HostListener, OnDestroy, OnInit, QueryList, ViewChild, ViewChildren, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeName, ThemeService } from '../../core/presentation/theme.service';
import { BrandMarkComponent } from '../../shared/ui/brand-mark.component';
import { OperationalContextSwitcherComponent } from '../../shared/ui/operational-context-switcher.component';
import { NAVIGATION_GROUPS, NavigationGroup, NavigationItem } from './navigation.config';

@Component({
  selector: 'app-application-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, BrandMarkComponent, OperationalContextSwitcherComponent],
  template: `
    <a class="skip-link" href="#main-content">{{ language.text('skipToContent') }}</a>
    <div class="shell" [class.shell--sidebar-expanded]="sidebarExpanded()">
      <aside #appSidebar id="app-sidebar" class="sidebar app-rail" [class.sidebar--expanded]="sidebarExpanded()" [class.sidebar--mobile-open]="mobileMenuOpen()" [attr.aria-label]="language.text('menu')">
        <nav class="sidebar__nav" [attr.aria-label]="language.text('menu')">
          @if (canShowModuleNavigation()) {
            @if (sidebarExpanded() || mobileMenuOpen()) {
              <section class="nav-group" [attr.aria-label]="language.text('overview')">
                <a class="nav-link nav-link--expanded" routerLink="/app" routerLinkActive="is-active" ariaCurrentWhenActive="page" [routerLinkActiveOptions]="{ exact: true }" [attr.aria-label]="language.text('overview')" (click)="closeMobileMenu()">
                  <span class="nav-icon" aria-hidden="true"><svg class="icon"><use href="#icon-home" /></svg></span><span class="nav-label">{{ language.text('overview') }}</span>
                </a>
              </section>
              @for (group of navigationGroups; track group.id) {
                <section class="nav-group" [attr.aria-label]="navigationLabel(group)">
                  <span class="nav-group__title">{{ navigationLabel(group) }}</span>
                  @for (item of group.items; track item.path) {
                    <a class="nav-link nav-link--expanded" [routerLink]="item.path" [class.is-active]="isNavigationItemCurrent(item)" [attr.aria-current]="isNavigationItemCurrent(item) ? 'page' : null" [attr.aria-label]="navigationLabel(item)" (click)="closeMobileMenu()">
                      <span class="nav-icon" aria-hidden="true"><svg class="icon"><use [attr.href]="'#icon-' + item.icon" /></svg></span><span class="nav-label">{{ navigationLabel(item) }}</span>
                    </a>
                  }
                </section>
              }
            } @else {
              <div class="rail-tiles">
                <button #railTrigger class="rail-tile" type="button" routerLink="/app" [class.is-active]="isOverviewCurrent()" [attr.aria-current]="isOverviewCurrent() ? 'page' : null" [attr.aria-label]="language.text('overview')" [title]="language.text('overview')" (keydown)="onRailKeydown($event)" (click)="closeModuleFlyout()">
                  <svg class="icon" aria-hidden="true"><use href="#icon-home" /></svg>
                </button>
                @for (group of navigationGroups; track group.id) {
                  <button #railTrigger class="rail-tile" type="button" [class.is-active]="isNavigationGroupCurrent(group)" [attr.aria-label]="navigationLabel(group)" [title]="navigationLabel(group)" [attr.aria-expanded]="openNavGroup() === group.id" aria-controls="module-flyout" (mouseenter)="onModuleTileEnter(group.id, $event.currentTarget)" (mouseleave)="scheduleFlyoutClose()" (focus)="onModuleTileFocus(group.id, $event)" (click)="toggleModuleFlyout(group.id, $event.currentTarget, $event)" (keydown)="onRailKeydown($event, group.id)">
                    <svg class="icon" aria-hidden="true"><use [attr.href]="'#icon-' + group.icon" /></svg>
                  </button>
                }
              </div>
              <nav #moduleFlyout id="module-flyout" class="nav-flyout app-flyout" [class.is-open]="!!openNavGroup()" [style.top.px]="flyoutTop()" [attr.aria-label]="currentFlyoutGroup() ? navigationLabel(currentFlyoutGroup()!) : null" [attr.aria-hidden]="openNavGroup() ? null : 'true'" [attr.inert]="openNavGroup() ? null : ''" (mouseenter)="cancelFlyoutClose()" (mouseleave)="scheduleFlyoutClose()" (keydown)="onModuleFlyoutKeydown($event)">
                @if (currentFlyoutGroup(); as group) {
                  <div class="nav-flyout__header">
                    <span class="nav-flyout__tile" aria-hidden="true"><svg class="icon"><use [attr.href]="'#icon-' + group.icon" /></svg></span>
                    <h2>{{ navigationLabel(group) }}</h2>
                  </div>
                  <div class="nav-flyout__items">
                    @for (item of group.items; track item.path) {
                      <a class="nav-flyout__link" [routerLink]="item.path" [class.is-active]="isNavigationItemCurrent(item)" [attr.aria-current]="isNavigationItemCurrent(item) ? 'page' : null" (click)="closeModuleFlyout()">
                        <svg class="icon" aria-hidden="true"><use [attr.href]="'#icon-' + item.icon" /></svg><span>{{ navigationLabel(item) }}</span>
                      </a>
                    }
                  </div>
                }
              </nav>
            }
          }
        </nav>

        <div class="sidebar__footer">
          <span class="sidebar__help">{{ language.text('helpText') }}</span>
          <span class="release-chip">MESP ? ERP ? 01</span>
        </div>
      </aside>

      @if (mobileMenuOpen()) { <button class="mobile-nav-backdrop" type="button" [attr.aria-label]="label('Close navigation', 'إغلاق القائمة')" (click)="closeMobileMenu()"></button> }

      <div class="shell__body">
        <header class="topbar">
          <div class="topbar__start">
            <button class="icon-button sidebar-toggle desktop-toggle" type="button" (click)="toggleSidebar()" [attr.aria-label]="sidebarExpanded() ? label('Collapse navigation', 'طي القائمة') : label('Expand navigation', 'توسيع القائمة')" [attr.aria-expanded]="sidebarExpanded()" aria-controls="app-sidebar"><svg class="icon" aria-hidden="true"><use href="#icon-menu" /></svg></button>
            <button class="icon-button sidebar-toggle mobile-toggle" type="button" (click)="toggleMobileMenu()" [attr.aria-label]="mobileMenuOpen() ? label('Close navigation', 'إغلاق القائمة') : label('Open navigation', 'فتح القائمة')" [attr.aria-expanded]="mobileMenuOpen()" aria-controls="app-sidebar"><svg class="icon" aria-hidden="true"><use href="#icon-menu" /></svg></button>
            <a class="topbar__brand" [class.topbar__brand--light-backplate]="theme.darkMode() && !!context.entry()?.branding?.logoLightUrl && !context.entry()?.branding?.logoDarkUrl" routerLink="/app" [attr.aria-label]="brandName()">
              @if (tenantLogoUrl()) {
                <img class="topbar__tenant-logo" [src]="tenantLogoUrl()" [alt]="context.entry()?.branding?.logoAltText || brandName()" />
              } @else {
                <app-brand-mark class="topbar__mesp-logo" variant="logo" [theme]="theme.darkMode() ? 'dark' : 'light'" alt="MESP" />
              }
            </a>
            <nav class="breadcrumbs" [attr.aria-label]="label('Breadcrumb', 'مسار التنقل')">
              @if (currentPage() === language.text('overview')) {
                <span aria-current="page">{{ currentPage() }}</span>
              } @else {
                <a routerLink="/app">{{ language.text('overview') }}</a><svg class="icon icon--chevron-right" aria-hidden="true"><use href="#icon-chevron-right" /></svg><span aria-current="page">{{ currentPage() }}</span>
              }
            </nav>
          </div>

          <div class="topbar__actions">
            <div class="tenant-context"><span class="eyebrow">{{ language.text('currentTenant') }}</span><strong>{{ contextLabel() }}</strong></div>
            <app-operational-context-switcher />
            <a class="context-management-link" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
            <div class="theme-control">
              <button #themeTrigger id="theme-trigger" class="theme-trigger" type="button" (click)="toggleThemeMenu()" [attr.aria-label]="label('Themes', 'المظاهر')" aria-haspopup="menu" [attr.aria-controls]="themeMenuOpen() ? 'theme-menu' : null" [attr.aria-expanded]="themeMenuOpen()">
                <svg class="icon" aria-hidden="true"><use href="#icon-palette" /></svg>
                <span>{{ label('Themes', 'المظاهر') }}</span><svg class="icon chevron" aria-hidden="true"><use href="#icon-chevron-down" /></svg>
              </button>
              @if (themeMenuOpen()) {
                <div #themeMenu id="theme-menu" class="theme-menu" role="menu" [attr.aria-label]="label('Choose a theme', 'اختر مظهراً')" (keydown)="onThemeMenuKeydown($event)">
                  @for (option of theme.options; track option.id) {
                    <button class="theme-option" type="button" role="menuitemradio" [attr.aria-checked]="theme.selectedTheme() === option.id" [attr.tabindex]="themeMenuFocus() === option.id ? 0 : -1" (click)="selectTheme(option.id)">
                      <span class="theme-swatch" [style.backgroundColor]="option.color" aria-hidden="true"></span><span class="theme-option__label">{{ option.label }}</span>
                      @if (theme.selectedTheme() === option.id) { <svg class="icon theme-check" aria-hidden="true"><use href="#icon-check" /></svg> }
                    </button>
                  }
                </div>
              }
            </div>
            <button class="icon-button scheme-toggle" type="button" (click)="theme.toggleDarkMode()" [attr.aria-label]="theme.darkMode() ? label('Switch to light mode', 'التبديل إلى الوضع الفاتح') : label('Switch to dark mode', 'التبديل إلى الوضع الداكن')" [attr.aria-pressed]="theme.darkMode()" [title]="theme.darkMode() ? label('Light mode', 'الوضع الفاتح') : label('Dark mode', 'الوضع الداكن')">
              <svg class="icon" aria-hidden="true"><use [attr.href]="theme.darkMode() ? '#icon-sun' : '#icon-moon'" /></svg>
            </button>
            <button class="icon-button notification-button" type="button" [attr.aria-label]="label('Notifications', 'الإشعارات')" [title]="label('Notifications', 'الإشعارات')"><svg class="icon" aria-hidden="true"><use href="#icon-bell" /></svg></button>
            <button class="language-button" type="button" (click)="language.toggle()" [attr.aria-label]="language.language() === 'en' ? 'Language: switch to Arabic' : 'اللغة: التبديل إلى الإنجليزية'">
              <svg class="icon language-button__globe" aria-hidden="true"><use href="#icon-globe" /></svg><span>{{ language.language() === 'en' ? 'EN' : 'عربي' }}</span>
            </button>
            <a class="context-management-link context-management-link--mobile" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
            <div class="user-pill">
              <span class="user-avatar" aria-hidden="true"><svg class="icon"><use href="#icon-user" /></svg></span>
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
    .shell { display: grid; grid-template-columns: var(--sidebar-collapsed) minmax(0, 1fr); min-height: 100dvh; background: transparent; transition: grid-template-columns var(--motion-slow) ease; }
    .shell--sidebar-expanded { grid-template-columns: var(--sidebar-expanded) minmax(0, 1fr); }
    .sidebar { position: sticky; z-index: 38; grid-column: 1; grid-row: 1; inset-block-start: 0; inset-inline-start: 0; display: flex; width: auto; height: 100dvh; min-height: 100dvh; max-height: 100dvh; flex-direction: column; gap: .8rem; overflow-x: hidden; overflow-y: auto; padding: calc(var(--header-height) + 1rem) .55rem 1rem; border-inline-end: 1px solid color-mix(in srgb, var(--accent) 12%, var(--line)); background: var(--surface-glass); box-shadow: var(--shadow-glass); backdrop-filter: blur(18px) saturate(145%); transition: padding var(--motion-slow) ease; }
    .sidebar--expanded { padding-inline: .85rem; }
    .sidebar__nav { position: relative; display: grid; align-content: start; justify-items: center; gap: .55rem; }
    .nav-group { display: grid; justify-items: center; gap: .22rem; }
    .nav-group__title { display: none; padding: .6rem .75rem .2rem; color: var(--ink-muted); font-size: 13px; font-weight: 800; letter-spacing: .05em; text-transform: uppercase; }
    .sidebar--expanded .nav-group__title, .sidebar--mobile-open .nav-group__title { display: block; }
    .nav-link { position: relative; display: flex; width: 48px; min-height: 48px; align-items: center; justify-content: center; gap: .75rem; border: 1px solid transparent; border-radius: 15px; padding: 2px; color: var(--ink-muted); background: transparent; font: 700 .92rem/1.2 var(--font-sans); text-decoration: none; transition: color var(--motion-fast) ease, background var(--motion-fast) ease, border-color var(--motion-fast) ease, transform var(--motion-fast) ease, box-shadow var(--motion-fast) ease; }
    .nav-link:hover { border-color: color-mix(in srgb, var(--accent) 18%, var(--line)); color: var(--accent); background: var(--accent-soft); transform: translateY(-1px); }
    .nav-link.is-active { border-color: transparent; color: var(--action-text, #fff); background: var(--accent-action); box-shadow: 0 7px 17px color-mix(in srgb, var(--accent) 26%, transparent); }
    .nav-link.is-active::before { position: absolute; inset-block: .65rem; inset-inline-start: -7px; width: 3px; border-radius: 3px; background: var(--accent-action); content: ''; }
    .nav-icon { display: inline-grid; width: 42px; height: 42px; place-items: center; flex: none; border: 1px solid color-mix(in srgb, var(--accent) 13%, var(--line)); border-radius: 13px; color: var(--accent); background: color-mix(in srgb, var(--surface-glass) 82%, var(--accent-soft)); box-shadow: 0 3px 9px rgb(14 27 45 / 9%), inset 0 1px 0 var(--glass-highlight); transition: color var(--motion-fast) ease, background var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .nav-icon .icon { width: 18px; height: 18px; }
    .nav-label { display: none; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .nav-link:hover .nav-icon, .nav-link:focus-visible .nav-icon { box-shadow: 0 7px 16px color-mix(in srgb, var(--accent) 17%, transparent); transform: translateY(-2px) scale(1.04); }
    .nav-link.is-active .nav-icon { border-color: transparent; color: var(--action-text, #fff); background: transparent; box-shadow: none; }
    .sidebar--expanded .sidebar__nav, .sidebar--mobile-open .sidebar__nav { justify-items: stretch; }
    .sidebar--expanded .nav-group, .sidebar--mobile-open .nav-group { justify-items: stretch; }
    .sidebar--expanded .nav-link, .sidebar--mobile-open .nav-link { width: auto; justify-content: flex-start; padding-inline: .55rem .75rem; }
    .sidebar--expanded .nav-icon, .sidebar--mobile-open .nav-icon { width: 38px; height: 38px; }
    .sidebar--expanded .nav-label, .sidebar--mobile-open .nav-label { display: block; }
    .sidebar__footer { display: grid; gap: .55rem; margin-block-start: auto; border-block-start: 1px solid var(--line); padding: .8rem .25rem .1rem; text-align: center; }
    .sidebar__help { display: none; color: var(--ink-muted); font-size: 14px; }
    .sidebar--expanded .sidebar__help, .sidebar--mobile-open .sidebar__help { display: block; }
    .release-chip { display: none; justify-content: center; border: 1px solid var(--line); border-radius: 99px; padding: .25rem .4rem; color: var(--ink-muted); font-size: 14px; font-weight: 700; }
    .sidebar--expanded .release-chip { display: inline-flex; padding: .3rem .5rem; }
    .sidebar__rail-trigger { flex: none; }
    .shell__body { grid-column: 2; grid-row: 1; min-width: 0; min-height: 100dvh; padding-block-start: var(--header-height); }
    .topbar { position: fixed; z-index: 50; inset-block-start: 0; inset-inline: 0; display: flex; min-height: var(--header-height); align-items: center; justify-content: space-between; gap: .8rem; border-block-end: 1px solid color-mix(in srgb, var(--accent) 14%, var(--line)); padding-inline: clamp(.75rem, 1.7vw, 1.5rem); color: var(--ink); background: var(--surface-glass); box-shadow: var(--shadow-glass); backdrop-filter: blur(20px) saturate(155%); }
    .topbar__start, .topbar__actions { display: flex; min-width: 0; align-items: center; gap: .65rem; }
    .topbar__brand { display: flex; width: 5.8rem; min-height: 2.75rem; align-items: center; justify-content: center; flex: none; border-radius: 10px; }
    .topbar__brand--light-backplate { border: 1px solid #fff; padding: .18rem .35rem; background: #fff; box-shadow: 0 2px 8px rgb(0 0 0 / 12%); }
    .topbar__mesp-logo { display: block; width: 5.1rem; }
    .topbar__tenant-logo { display: block; width: auto; max-width: 5.1rem; max-height: 2.45rem; object-fit: contain; }
    .breadcrumbs { display: flex; min-width: 0; align-items: center; gap: .5rem; color: var(--ink-muted); font-size: 14px; white-space: nowrap; }
    .breadcrumbs .icon { width: 14px; height: 14px; }
    .breadcrumbs a { color: var(--ink-muted); text-decoration: none; }
    .breadcrumbs a:hover, .breadcrumbs [aria-current='page'] { color: var(--accent); }
    .topbar__actions { justify-content: flex-end; gap: .5rem; }
    .tenant-context { display: grid; max-width: 14rem; gap: .05rem; }
    .tenant-context .eyebrow { color: var(--ink-muted); font-size: 13px; font-weight: 700; }
    .tenant-context strong { overflow: hidden; color: var(--ink); font-size: 14px; font-weight: 800; text-overflow: ellipsis; white-space: nowrap; }
    .context-management-link { color: var(--ink-muted); font-size: 14px; font-weight: 700; text-decoration: none; white-space: nowrap; }
    .context-management-link:hover { color: var(--accent); }
    .context-management-link--mobile { display: none; }
    .icon-button, .language-button, .theme-trigger { display: inline-flex; min-height: 42px; align-items: center; justify-content: center; gap: .5rem; border: 1px solid var(--line); border-radius: 14px; padding: .45rem .7rem; color: var(--accent); background: var(--surface-raised); box-shadow: 0 3px 9px rgb(15 26 48 / 6%); font: 700 .9rem/1.1 var(--font-sans); transition: border-color var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease, background var(--motion-fast) ease; }
    .icon-button.mobile-toggle { display: none; }
    .icon-button { width: 42px; padding: 0; font-size: 1.15rem; }
    .icon-button:hover, .language-button:hover, .theme-trigger:hover { border-color: var(--accent); box-shadow: 0 6px 14px color-mix(in srgb, var(--accent) 14%, transparent); transform: translateY(-1px); }
    .theme-control { position: relative; }
    .theme-trigger { border-radius: 999px; padding-inline: .85rem; }
    .theme-trigger .icon { width: 18px; height: 18px; }
    .chevron { width: 15px !important; height: 15px !important; color: var(--ink-muted); }
    .theme-menu { position: absolute; z-index: 80; inset-block-start: calc(100% + .65rem); inset-inline-end: 0; display: grid; width: 264px; gap: .25rem; border: 1px solid color-mix(in srgb, var(--accent) 20%, var(--line)); border-radius: 18px; padding: .55rem; background: var(--surface-glass); box-shadow: var(--shadow-overlay), inset 0 1px 0 var(--glass-highlight); backdrop-filter: blur(22px) saturate(160%); animation: menu-enter 150ms ease both; }
    .theme-option { display: flex; min-height: 42px; align-items: center; gap: .75rem; border: 1px solid transparent; border-radius: 12px; padding: .45rem .6rem; color: var(--ink); background: transparent; text-align: start; font: 600 14px/1.2 var(--font-sans); }
    .theme-option:hover, .theme-option:focus-visible { border-color: color-mix(in srgb, var(--accent) 20%, var(--line)); background: var(--accent-soft); }
    .theme-swatch { width: 15px; height: 15px; flex: none; border: 1px solid rgb(0 0 0 / 12%); border-radius: 50%; box-shadow: 0 2px 6px rgb(0 0 0 / 16%); }
    .theme-option__label { flex: 1; }
    .theme-check { width: 18px; height: 18px; color: var(--accent); }
    .language-button { border-radius: 999px; color: var(--ink); }
    .language-button__globe { width: 18px; height: 18px; color: var(--accent); }
    .user-pill { display: inline-flex; min-height: 42px; align-items: center; gap: .45rem; border: 1px solid var(--line); border-radius: 999px; padding: .22rem .45rem; background: var(--surface-raised); box-shadow: 0 3px 10px rgb(15 26 48 / 5%); }
    .user-avatar { display: grid; width: 30px; height: 30px; place-items: center; border-radius: 50%; color: var(--action-text); background: var(--accent-action); font-size: .8rem; font-weight: 800; }
    .user-avatar svg { width: 16px; height: 16px; fill: currentColor; }
    .user-pill__label { color: var(--ink); font-size: 14px; font-weight: 700; }
    .sign-out { border: 0; border-inline-start: 1px solid var(--line); padding-inline-start: .55rem; color: var(--ink-muted); background: transparent; font: 600 14px/1 var(--font-sans); }
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
      .shell { display: block; }
      .topbar { min-height: var(--header-height); align-items: stretch; flex-direction: column; justify-content: center; gap: .25rem; padding-block: .45rem; }
      .topbar__start { min-height: 42px; justify-content: space-between; }
      .topbar__actions { min-height: 42px; justify-content: space-between; gap: .35rem; }
      .topbar__brand { width: 4.8rem; }
      .topbar__mesp-logo { width: 4.3rem; }
      .breadcrumbs { margin-inline-start: auto; font-size: .8rem; }
      .desktop-toggle { display: none; }
      .icon-button.mobile-toggle { display: inline-flex; }
      .sidebar { display: none; }
      .sidebar--mobile-open { position: fixed; z-index: 70; inset-block-start: var(--header-height); inset-inline-start: 0; inset-block-end: 0; display: flex; width: min(var(--sidebar-expanded), 88vw); height: auto; min-height: 0; max-height: none; padding: 1rem .85rem; }
      .sidebar--mobile-open .nav-group__title, .sidebar--mobile-open .nav-label, .sidebar--mobile-open .sidebar__help { display: block; }
      .sidebar--mobile-open .nav-link { justify-content: flex-start; padding-inline: .55rem .75rem; }
      .sidebar--mobile-open .nav-link { width: auto; }
      .sidebar--mobile-open .nav-icon { width: 38px; height: 38px; }
      .nav-flyout { display: none; }
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
    @media (max-width: 520px) {
      :host { --header-height: 176px; }
      .topbar__actions { width: 100%; min-height: 108px; flex-wrap: wrap; align-content: center; }
    }
    @media (max-width: 420px) {
      .topbar__actions { gap: .25rem; }
      .icon-button, .language-button, .theme-trigger { min-height: 40px; }
      .icon-button { width: 40px; }
      .language-button { padding-inline: .45rem; }
      .theme-trigger { width: 40px; min-width: 40px; gap: .3rem; padding-inline: 0; }
      .theme-trigger span, .theme-trigger .chevron { display: none; }
      .context-management-link--mobile { display: none; }
      .theme-menu { width: min(264px, calc(100vw - 24px)); }
      .breadcrumbs { gap: .3rem; font-size: .74rem; }
    }
    @supports not (backdrop-filter: blur(4px)) { .sidebar, .topbar, .theme-menu { background: var(--surface-raised); } .mobile-nav-backdrop { background: rgb(9 15 26 / 58%); } }
    @media (prefers-reduced-motion: reduce) { .sidebar, .nav-link, .nav-icon { transition: none; } .nav-link:hover .nav-icon, .nav-link:focus-visible .nav-icon { transform: none; } }
    .shell { grid-template-columns: 76px minmax(0, 1fr); }
    .shell--sidebar-expanded { grid-template-columns: 260px minmax(0, 1fr); }
    @media (max-width: 767px) {
      .shell, .shell--sidebar-expanded { display: block; }
      .sidebar { display: none; }
      .sidebar--mobile-open { position: fixed; z-index: 70; inset-block-start: var(--header-height); inset-inline-start: 0; inset-block-end: 0; display: flex; width: min(260px, 88vw); height: auto; min-height: 0; max-height: none; padding: 16px; }
      .sidebar--mobile-open .nav-group__title, .sidebar--mobile-open .nav-label, .sidebar--mobile-open .sidebar__help { display: block; }
      .sidebar--mobile-open .nav-link--expanded { width: 100%; }
      .desktop-toggle { display: none; }
      .icon-button.mobile-toggle { display: inline-flex; }
    }
    @media (prefers-reduced-motion: reduce) {
      .rail-tile { transition: none; }
      .rail-tile:hover, .rail-tile:focus-visible { transform: none; }
      .nav-flyout { transform: none; transition: opacity 160ms ease-out, visibility 160ms; }
      :host-context([dir=rtl]) .nav-flyout { transform: none; }
      .nav-flyout.is-open, :host-context([dir=rtl]) .nav-flyout.is-open { transform: none; }
    }
  `,
  styleUrls: ['./application-shell-rail.scss'],
})
export class ApplicationShellComponent implements OnInit, OnDestroy {
  readonly auth = inject(AuthService);
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  readonly navigationGroups = NAVIGATION_GROUPS;

  readonly sidebarExpanded = signal(false);
  readonly mobileMenuOpen = signal(false);
  readonly openNavGroup = signal<string | null>(null);
  readonly flyoutTop = signal(84);
  readonly themeMenuOpen = signal(false);
  readonly themeMenuFocus = signal<ThemeName>('sapphire');
  private themeMenuElement?: HTMLElement;
  private sidebarElement?: HTMLElement;
  private moduleFlyoutElement?: HTMLElement;
  private lastModuleTrigger?: HTMLElement;
  private flyoutOpenTimer?: ReturnType<typeof setTimeout>;
  private flyoutCloseTimer?: ReturnType<typeof setTimeout>;

  @ViewChild('appSidebar') set appSidebar(value: ElementRef<HTMLElement> | undefined) { this.sidebarElement = value?.nativeElement; }
  @ViewChild('moduleFlyout') set moduleFlyout(value: ElementRef<HTMLElement> | undefined) { this.moduleFlyoutElement = value?.nativeElement; }
  @ViewChildren('railTrigger') private railTriggers?: QueryList<ElementRef<HTMLElement>>;
  @ViewChild('themeTrigger') private themeTrigger?: ElementRef<HTMLButtonElement>;
  @ViewChild('themeMenu') set themeMenu(value: ElementRef<HTMLElement> | undefined) {
    this.themeMenuElement = value?.nativeElement;
    if (this.themeMenuElement) {
      queueMicrotask(() => this.themeMenuElement?.querySelector<HTMLElement>('[tabindex="0"]')?.focus());
    }
  }

  ngOnInit(): void {
    try {
      this.sidebarExpanded.set(typeof window !== 'undefined' && window.localStorage.getItem('mesp.ui.rail') === 'expanded');
    } catch { /* Storage can be unavailable in a restricted browser context. */ }
    if (this.context.contexts().length === 0 && !this.context.entry()) {
      void this.context.loadEntry();
    }
  }

  ngOnDestroy(): void {
    this.cancelFlyoutOpen();
    this.cancelFlyoutClose();
  }

  @HostListener('document:click', ['$event'])
  closeThemeMenuOnOutsideClick(event: MouseEvent): void {
    const target = event.target;
    if (!this.themeMenuOpen() || !(target instanceof Node)) return;
    if (this.themeMenuElement?.contains(target) || this.themeTrigger?.nativeElement.contains(target)) return;
    this.themeMenuOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  closeModuleFlyoutOnOutsideClick(event: MouseEvent): void {
    const target = event.target;
    if (!this.openNavGroup() || !(target instanceof Node) || this.sidebarElement?.contains(target)) return;
    this.closeModuleFlyout();
  }

  @HostListener('document:keydown', ['$event'])
  closeOverlaysOnEscape(event: KeyboardEvent): void {
    if (event.key !== 'Escape') return;
    if (this.themeMenuOpen()) {
      event.preventDefault();
      this.closeThemeMenu(true);
    }
    if (this.mobileMenuOpen()) this.closeMobileMenu();
    if (this.openNavGroup()) {
      event.preventDefault();
      this.closeModuleFlyout(true);
    }
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

  toggleSidebar(): void {
    const expanded = !this.sidebarExpanded();
    this.sidebarExpanded.set(expanded);
    try { window.localStorage.setItem('mesp.ui.rail', expanded ? 'expanded' : 'collapsed'); } catch { /* Storage can be unavailable in a restricted browser context. */ }
    this.closeModuleFlyout();
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
    this.closeModuleFlyout();
  }

  canShowModuleNavigation(): boolean {
    return !this.context.entry() || (this.context.entry()?.entryMode !== 'PlatformAdminHost' && this.context.entry()?.entryMode !== 'NoAccess');
  }

  label(english: string, arabic: string): string {
    return this.language.language() === 'ar' ? arabic : english;
  }

  navigationLabel(item: Pick<NavigationItem, 'labelEn' | 'labelAr'> | { labelEn: string; labelAr: string }): string {
    return this.label(item.labelEn, item.labelAr);
  }

  currentNavigationItem(): NavigationItem | null {
    const url = this.router.url.split(/[?#]/)[0] ?? '/app';
    let match: NavigationItem | null = null;
    for (const group of NAVIGATION_GROUPS) {
      for (const item of group.items as readonly NavigationItem[]) {
        const isMasterDataResource = item.path === '/app/master-data/categories'
          && url.startsWith('/app/master-data/')
          && !url.startsWith('/app/master-data/imports');
        if ((isMasterDataResource || url === item.path || url.startsWith(item.path + '/')) && (!match || item.path.length > match.path.length)) match = item;
      }
    }
    return match;
  }

  isNavigationItemCurrent(item: NavigationItem): boolean {
    return this.currentNavigationItem()?.path === item.path;
  }

  isNavigationGroupCurrent(group: NavigationGroup): boolean {
    return group.items.some((item) => this.isNavigationItemCurrent(item));
  }

  currentFlyoutGroup(): NavigationGroup | null {
    return this.navigationGroups.find((group) => group.id === this.openNavGroup()) ?? null;
  }

  isOverviewCurrent(): boolean {
    const url = this.router.url.split(/[?#]/)[0] ?? '/app';
    return url === '/app' || url === '/app/';
  }

  onModuleTileEnter(groupId: string, trigger: EventTarget | null): void {
    this.cancelFlyoutClose();
    this.cancelFlyoutOpen();
    this.flyoutOpenTimer = setTimeout(() => this.openModuleFlyout(groupId, false, trigger), 80);
  }

  onModuleTileFocus(groupId: string, event: FocusEvent): void {
    const trigger = event.currentTarget;
    if (event.target instanceof HTMLElement && event.target.matches(':focus-visible')) {
      this.cancelFlyoutOpen();
      this.openModuleFlyout(groupId, false, trigger);
    }
  }

  toggleModuleFlyout(groupId: string, trigger: EventTarget | null, event: MouseEvent): void {
    this.cancelFlyoutOpen();
    this.cancelFlyoutClose();
    if (event.detail === 0) {
      this.openModuleFlyout(groupId, true, trigger);
      return;
    }
    if (this.openNavGroup() === groupId) {
      this.closeModuleFlyout();
      return;
    }
    this.openModuleFlyout(groupId, false, trigger);
  }

  openModuleFlyout(groupId: string, focusFirst = false, trigger?: EventTarget | null): void {
    if (this.sidebarExpanded() || this.mobileMenuOpen()) return;
    this.cancelFlyoutOpen();
    this.cancelFlyoutClose();
    if (trigger instanceof HTMLElement) this.lastModuleTrigger = trigger;
    this.openNavGroup.set(groupId);
    requestAnimationFrame(() => {
      this.positionFlyout();
      if (focusFirst) this.moduleFlyoutElement?.querySelector<HTMLElement>('a[href]')?.focus();
    });
  }

  closeModuleFlyout(restoreFocus = false): void {
    this.cancelFlyoutOpen();
    this.cancelFlyoutClose();
    this.openNavGroup.set(null);
    if (restoreFocus) queueMicrotask(() => this.lastModuleTrigger?.focus());
  }

  cancelFlyoutOpen(): void {
    if (this.flyoutOpenTimer !== undefined) clearTimeout(this.flyoutOpenTimer);
    this.flyoutOpenTimer = undefined;
  }

  cancelFlyoutClose(): void {
    if (this.flyoutCloseTimer !== undefined) clearTimeout(this.flyoutCloseTimer);
    this.flyoutCloseTimer = undefined;
  }

  scheduleFlyoutClose(): void {
    this.cancelFlyoutClose();
    this.flyoutCloseTimer = setTimeout(() => this.closeModuleFlyout(), 150);
  }

  positionFlyout(): void {
    if (typeof window === 'undefined' || !this.lastModuleTrigger || !this.moduleFlyoutElement || !this.openNavGroup()) return;
    const railTop = this.sidebarElement?.getBoundingClientRect().top ?? 0;
    const triggerTop = this.lastModuleTrigger.getBoundingClientRect().top - railTop;
    const minTop = 12 - railTop;
    const maxTop = window.innerHeight - this.moduleFlyoutElement.offsetHeight - 12 - railTop;
    this.flyoutTop.set(Math.min(Math.max(triggerTop, minTop), Math.max(minTop, maxTop)));
  }

  @HostListener('window:resize')
  repositionFlyout(): void { this.positionFlyout(); }

  onRailKeydown(event: KeyboardEvent, groupId?: string): void {
    const triggers = this.railTriggers?.toArray().map((item) => item.nativeElement) ?? [];
    const index = triggers.indexOf(event.currentTarget as HTMLElement);
    if (index < 0 || triggers.length === 0) return;
    if (groupId && (event.key === 'Enter' || event.key === ' ' || event.key === (this.language.language() === 'ar' ? 'ArrowLeft' : 'ArrowRight'))) {
      event.preventDefault();
      this.openModuleFlyout(groupId, true, event.currentTarget);
      return;
    }
    const rtl = this.language.language() === 'ar';
    let next: number | null = null;
    if (event.key === 'ArrowDown') next = (index + 1) % triggers.length;
    if (event.key === 'ArrowUp') next = (index - 1 + triggers.length) % triggers.length;
    if (next === null) return;
    event.preventDefault();
    triggers[next]?.focus();
  }

  onModuleFlyoutKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.closeModuleFlyout(true);
      return;
    }
    const items = Array.from(this.moduleFlyoutElement?.querySelectorAll<HTMLElement>('a[href]') ?? []);
    if (items.length === 0) return;
    const first = items[0];
    const last = items[items.length - 1];
    const index = items.indexOf(event.target as HTMLElement);
    let next: number | null = null;
    if (event.key === 'ArrowDown') next = (index + 1 + items.length) % items.length;
    if (event.key === 'ArrowUp') next = (index - 1 + items.length) % items.length;
    if (next !== null) {
      event.preventDefault();
      items[next]?.focus();
      return;
    }
    if (event.key !== 'Tab') return;
    if ((event.shiftKey && event.target === first) || (!event.shiftKey && event.target === last)) this.closeModuleFlyout();
  }

  currentPage(): string {
    const url = this.router.url.split(/[?#]/)[0] ?? '/app';
    if (url === '/app' || url === '/app/') return this.language.text('overview');
    const currentItem = this.currentNavigationItem();
    if (currentItem) return this.navigationLabel(currentItem);
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
