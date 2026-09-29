import { Component, Input, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LanguageService } from '../../core/i18n/language.service';
import { NAVIGATION_GROUPS, NavigationItem } from '../../features/shell/navigation.config';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [RouterLink],
  template: `
    <header class="page-header__surface">
      <nav class="page-header__breadcrumb" [attr.aria-label]="breadcrumbLabel()">
        <a routerLink="/app">{{ language.text('overview') }}</a>
        <svg class="icon" aria-hidden="true"><use href="#icon-chevron-right" /></svg>
        <span aria-current="page">{{ pageLabel() }}</span>
      </nav>
      <div class="page-header__body">
        <ng-content select="[page-header-copy]" />
        <div class="page-header__actions"><ng-content select="[page-header-actions]" /></div>
      </div>
    </header>
  `,
  styles: [`
    :host { --primary: var(--accent); display: block; min-width: 0; color: var(--ink); }
    .page-header__surface { display: grid; gap: 12px; min-width: 0; border: 1px solid rgb(255 255 255 / .6); border-radius: 20px; padding: 24px 28px; color: var(--ink); background: rgb(255 255 255 / .84); box-shadow: 0 12px 32px rgb(15 23 42 / .08); backdrop-filter: blur(18px) saturate(1.4); }
    :host-context(html[data-color-scheme='dark']) .page-header__surface { border-color: rgb(255 255 255 / .08); background: rgb(15 23 42 / .84); }
    .page-header__breadcrumb { display: flex; min-width: 0; align-items: center; gap: 7px; color: var(--ink-muted); font-size: 13px; font-weight: 500; }
    .page-header__breadcrumb a { color: var(--ink-muted); text-decoration: none; }
    .page-header__breadcrumb a:hover, .page-header__breadcrumb [aria-current=page] { color: var(--primary); }
    .page-header__breadcrumb .icon { width: 14px; height: 14px; }
    :host-context([dir=rtl]) .page-header__breadcrumb .icon { transform: rotate(180deg); }
    .page-header__body { display: flex; min-width: 0; align-items: center; justify-content: space-between; gap: 20px; }
    .page-header__actions { display: flex; min-width: 0; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 10px; }
    :host ::ng-deep [page-header-copy] { display: block; min-width: 0; flex: 1 1 auto; }
    :host ::ng-deep [page-header-actions] { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 10px; }
    :host ::ng-deep .eyebrow, :host ::ng-deep .return-kicker { margin: 0 0 5px; color: var(--primary); font-size: 12px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    :host ::ng-deep [page-header-copy] h1 { margin: 0; color: var(--ink-strong); font: 700 28px/1.2 var(--font-sans); letter-spacing: 0; }
    :host ::ng-deep [page-header-copy] > p:not(.eyebrow):not(.return-kicker), :host ::ng-deep [page-header-copy] .lede, :host ::ng-deep [page-header-copy] .lead { max-width: 52rem; margin: 6px 0 0; color: var(--ink-muted); font-size: 15px; line-height: 1.5; }
    @media (max-width: 620px) { .page-header__surface { padding: 18px 20px; } .page-header__body { align-items: stretch; flex-direction: column; } .page-header__actions, :host ::ng-deep [page-header-actions] { justify-content: flex-start; } }
    @media (prefers-reduced-motion: reduce) { .page-header__breadcrumb .icon { transform: none; } }
  `],
})
export class PageHeaderComponent {
  readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  @Input() pageLabelOverride: string | null = null;

  breadcrumbLabel(): string { return this.language.language() === 'ar' ? '\u0645\u0633\u0627\u0631 \u0627\u0644\u062a\u0646\u0642\u0644' : 'Breadcrumb'; }

  pageLabel(): string {
    if (this.pageLabelOverride) return this.pageLabelOverride;
    const url = this.router.url.split(/[?#]/)[0] ?? '/app';
    let match: NavigationItem | null = null;
    for (const group of NAVIGATION_GROUPS) {
      for (const item of group.items) {
        const isMasterDataResource = item.path === '/app/master-data/categories'
          && url.startsWith('/app/master-data/')
          && !url.startsWith('/app/master-data/imports');
        if ((isMasterDataResource || url === item.path || url.startsWith(item.path + '/'))
          && (!match || item.path.length > match.path.length)) match = item;
      }
    }
    if (match) return this.language.language() === 'ar' ? match.labelAr : match.labelEn;
    return this.language.text('overview');
  }
}
