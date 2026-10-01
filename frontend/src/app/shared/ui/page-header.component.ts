import { Component } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  template: `
    <header class="page-header__surface">
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
    .page-header__body { display: flex; min-width: 0; align-items: center; justify-content: space-between; gap: 20px; }
    .page-header__actions { display: flex; min-width: 0; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 10px; }
    :host ::ng-deep [page-header-copy] { display: block; min-width: 0; flex: 1 1 auto; }
    :host ::ng-deep [page-header-actions] { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 10px; }
    :host ::ng-deep .eyebrow, :host ::ng-deep .return-kicker { margin: 0 0 5px; color: var(--primary); font-size: 12px; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
    :host ::ng-deep [page-header-copy] h1 { margin: 0; color: var(--ink-strong); font: 700 28px/1.2 var(--font-sans); letter-spacing: 0; }
    :host ::ng-deep [page-header-copy] > p:not(.eyebrow):not(.return-kicker), :host ::ng-deep [page-header-copy] .lede, :host ::ng-deep [page-header-copy] .lead { max-width: 52rem; margin: 6px 0 0; color: var(--ink-muted); font-size: 15px; line-height: 1.5; }
    @media (max-width: 620px) { .page-header__surface { padding: 18px 20px; } .page-header__body { align-items: stretch; flex-direction: column; } .page-header__actions, :host ::ng-deep [page-header-actions] { justify-content: flex-start; } }
  `],
})
export class PageHeaderComponent {
}
