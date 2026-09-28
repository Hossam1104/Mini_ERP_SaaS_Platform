import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';

@Component({
  selector: 'app-workspace-home',
  standalone: true,
  imports: [RouterLink, StatusCardComponent],
  template: `
    <section class="tenant-overview ui-page" aria-labelledby="tenant-overview-title">
      @if (isNoAccess()) {
        <header class="overview-hero"><p class="eyebrow">{{ language.text('tenantOverview') }}</p><h1 id="tenant-overview-title">{{ contextHeading() }}</h1></header>
        <app-status-card [title]="language.text('noAccessTitle')" [message]="language.text('noAccessMessage')" state="denied" tone="danger" />
      } @else if (isPlatformControlPlane()) {
        <header class="overview-hero"><p class="eyebrow">{{ language.text('tenantOverview') }}</p><h1 id="tenant-overview-title">{{ contextHeading() }}</h1></header>
        <app-status-card [title]="language.text('platformControlPlaneTitle')" [message]="language.text('platformControlPlaneMessage')" state="unknown" tone="neutral" />
      } @else {
        <header class="overview-hero" aria-labelledby="tenant-overview-title">
          <div class="overview-hero__content">
            <p class="eyebrow">{{ language.text('tenantOverview') }}</p>
            <h1 id="tenant-overview-title">{{ contextHeading() }}</h1>
            <p class="overview-hero__lead">{{ language.text('tenantOverviewLead') }}</p>
            <a class="overview-manage" routerLink="/app/workspaces"><span aria-hidden="true">⌘</span>{{ language.text('manageContexts') }}</a>
          </div>
          <div class="overview-hero__art" aria-hidden="true"><span class="hero-orbit hero-orbit--one"></span><span class="hero-orbit hero-orbit--two"></span><span class="hero-orbit hero-orbit--three"></span><span class="hero-mark">M</span></div>
        </header>

        <section class="overview-modules" aria-labelledby="capability-title">
          <div class="overview-section-heading">
            <div><p class="eyebrow">{{ language.text('tenantWorkspace') }}</p><h2 id="capability-title">{{ language.text('continueWith') }}</h2></div>
            <span class="overview-section-heading__hint">{{ label('Your workspace', 'مساحة عملك') }}</span>
          </div>
          <div class="capability-grid">
            <a class="capability-card capability-card--master" routerLink="/app/master-data/categories">
              <span class="capability-card__top"><span class="capability-card__icon" aria-hidden="true">◇</span><span class="capability-card__index">01</span></span>
              <strong>{{ language.text('masterData') }}</strong><span>{{ language.text('masterDataOverview') }}</span><span class="capability-card__arrow" aria-hidden="true">↗</span>
            </a>
            <a class="capability-card capability-card--price" routerLink="/app/price-lists">
              <span class="capability-card__top"><span class="capability-card__icon" aria-hidden="true">＄</span><span class="capability-card__index">02</span></span>
              <strong>{{ language.text('priceLists') }}</strong><span>{{ language.text('priceListsOverview') }}</span><span class="capability-card__arrow" aria-hidden="true">↗</span>
            </a>
            <a class="capability-card capability-card--procurement" routerLink="/app/procurement/purchase-requests">
              <span class="capability-card__top"><span class="capability-card__icon" aria-hidden="true">↗</span><span class="capability-card__index">03</span></span>
              <strong>{{ language.text('purchaseRequestsNavLabel') }}</strong><span>{{ language.text('purchaseRequestsOverview') }}</span><span class="capability-card__arrow" aria-hidden="true">↗</span>
            </a>
            <a class="capability-card capability-card--suppliers" routerLink="/app/procurement/supplier-quotations">
              <span class="capability-card__top"><span class="capability-card__icon" aria-hidden="true">◈</span><span class="capability-card__index">04</span></span>
              <strong>{{ language.text('supplierQuotationsNavLabel') }}</strong><span>{{ language.text('supplierQuotationsOverview') }}</span><span class="capability-card__arrow" aria-hidden="true">↗</span>
            </a>
          </div>
        </section>

        @if (!hasOperationalContext() && context.entry()?.entryMode === 'TenantHost') {
          <app-status-card [title]="language.text('operationalContextPending')" [message]="language.text('operationalContextPendingMessage')" state="pending" tone="neutral" />
        }
      }
    </section>
  `,
  styles: `
    :host { display: block; }
    .tenant-overview { display: grid; gap: clamp(1.25rem, 2.8vw, 2rem); }
    .overview-hero { position: relative; display: flex; min-height: 210px; align-items: center; justify-content: space-between; gap: 2rem; overflow: hidden; border: 1px solid color-mix(in srgb, var(--accent) 22%, var(--line)); border-radius: 24px; padding: clamp(1.35rem, 3vw, 2.6rem); background: radial-gradient(ellipse at 12% 8%, color-mix(in srgb, var(--accent) 13%, transparent), transparent 46%), linear-gradient(115deg, color-mix(in srgb, var(--accent-soft) 78%, var(--surface-glass)), var(--surface-glass) 68%, var(--surface-raised)); box-shadow: var(--shadow-glass); backdrop-filter: blur(14px) saturate(145%); animation: card-enter 480ms ease both; }
    .overview-hero__content { position: relative; z-index: 1; max-width: 52rem; }
    .eyebrow { margin: 0 0 .55rem; color: var(--accent); font-size: .85rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
    h1 { max-width: 48rem; margin: 0; color: var(--ink-strong); font: 800 clamp(2rem, 4vw, 3.35rem)/1.08 var(--font-display); letter-spacing: -.02em; }
    .overview-hero__lead { max-width: 43rem; margin: .75rem 0 1rem; color: var(--ink-muted); font-size: 1rem; line-height: 1.6; }
    .overview-manage { display: inline-flex; min-height: 42px; align-items: center; gap: .5rem; border: 1px solid color-mix(in srgb, var(--accent) 28%, var(--line)); border-radius: 999px; padding: .45rem .9rem; color: var(--accent-strong); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: .9rem; font-weight: 800; text-decoration: none; transition: transform var(--motion-fast) ease, box-shadow var(--motion-fast) ease; }
    .overview-manage:hover { box-shadow: var(--shadow-card); transform: translateY(-2px); }
    .overview-hero__art { position: relative; width: 210px; height: 150px; flex: 0 0 210px; }
    .hero-orbit { position: absolute; inset-block-start: 50%; inset-inline-start: 50%; display: block; border: 1px solid color-mix(in srgb, var(--accent) 27%, transparent); border-radius: 50%; transform: translate(-50%, -50%); }
    .hero-orbit--one { width: 145px; height: 145px; }
    .hero-orbit--two { width: 190px; height: 110px; transform: translate(-50%, -50%) rotate(-35deg); }
    .hero-orbit--three { width: 190px; height: 110px; transform: translate(-50%, -50%) rotate(35deg); }
    .hero-mark { position: absolute; inset: 50% auto auto 50%; display: grid; width: 74px; height: 74px; place-items: center; border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--line)); border-radius: 24px; color: var(--action-text); background: linear-gradient(140deg, var(--accent-action), var(--accent-strong)); box-shadow: 0 14px 32px color-mix(in srgb, var(--accent) 28%, transparent); font: 800 2.1rem/1 var(--font-display); transform: translate(-50%, -50%) rotate(-5deg); }
    .overview-modules { display: grid; gap: .9rem; }
    .overview-section-heading { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding-inline: .2rem; }
    .overview-section-heading h2 { margin: 0; color: var(--ink-strong); font: 800 clamp(1.3rem, 2.3vw, 1.8rem)/1.15 var(--font-display); }
    .overview-section-heading .eyebrow { margin-block-end: .25rem; }
    .overview-section-heading__hint { color: var(--ink-muted); font-size: .86rem; }
    .capability-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: .9rem; }
    .capability-card { position: relative; display: grid; min-height: 178px; align-content: start; gap: .55rem; overflow: hidden; border: 1px solid var(--line); border-radius: var(--radius-card); padding: 1.05rem; color: var(--ink); background: linear-gradient(145deg, var(--surface-glass), var(--surface-raised) 70%); box-shadow: var(--shadow-card); text-decoration: none; animation: card-enter 420ms ease both; transition: border-color var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .capability-card:nth-child(2) { animation-delay: 45ms; }
    .capability-card:nth-child(3) { animation-delay: 90ms; }
    .capability-card:nth-child(4) { animation-delay: 135ms; }
    .capability-card::after { position: absolute; inset-block-end: -48px; inset-inline-end: -34px; width: 120px; height: 120px; border-radius: 50%; background: color-mix(in srgb, var(--accent) 8%, transparent); content: ''; transition: transform var(--motion-slow) ease; }
    .capability-card:hover { border-color: color-mix(in srgb, var(--accent) 35%, var(--line)); box-shadow: var(--shadow-overlay); transform: translateY(-4px); }
    .capability-card:hover::after { transform: scale(1.3); }
    .capability-card:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .capability-card__top { display: flex; align-items: center; justify-content: space-between; margin-block-end: .35rem; }
    .capability-card__icon { display: grid; width: 42px; height: 42px; place-items: center; border: 1px solid color-mix(in srgb, var(--accent) 20%, var(--line)); border-radius: 14px; color: var(--accent); background: var(--accent-soft); font-size: 1.25rem; }
    .capability-card__index { color: var(--ink-muted); font-size: .8rem; font-weight: 800; }
    .capability-card strong { color: var(--ink-strong); font: 800 1.08rem/1.25 var(--font-display); }
    .capability-card > span:not(.capability-card__top) { max-width: 25rem; color: var(--ink-muted); font-size: .9rem; line-height: 1.45; }
    .capability-card .capability-card__arrow { position: absolute; inset-block-end: .9rem; inset-inline-end: 1rem; color: var(--accent); font-size: 1rem; }
    @media (max-width: 1000px) { .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 620px) { .overview-hero { min-height: 0; padding: 1.25rem; } .overview-hero__art { position: absolute; inset-inline-end: -3.5rem; inset-block-end: -2rem; width: 150px; height: 120px; opacity: .52; } .hero-orbit--one { width: 116px; height: 116px; } .hero-orbit--two, .hero-orbit--three { width: 150px; height: 90px; } .hero-mark { width: 58px; height: 58px; border-radius: 19px; font-size: 1.65rem; } .overview-section-heading__hint { display: none; } .capability-grid { gap: .65rem; } .capability-card { min-height: 166px; padding: .85rem; } .capability-card strong { font-size: 1rem; } .capability-card > span:not(.capability-card__top) { font-size: .82rem; } }
    @media (max-width: 350px) { .capability-grid { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { .overview-hero, .capability-card { animation: none; } }
  `,
})
export class WorkspaceHomeComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  ngOnInit(): void {
    if (!this.context.entry()) void this.context.loadEntry();
  }

  contextHeading(): string {
    return this.context.entry()?.candidateTenantDisplayName
      ?? this.context.entry()?.branding.displayName
      ?? this.language.text('tenantOverview');
  }

  isNoAccess(): boolean { return this.context.entry()?.entryMode === 'NoAccess'; }
  isPlatformControlPlane(): boolean { return this.context.entry()?.entryMode === 'PlatformAdminHost'; }
  hasOperationalContext(): boolean { return this.context.selectedOperationalContextId() !== null || this.context.operationalContexts().length === 0; }

  label(english: string, arabic: string): string {
    return this.language.language() === 'ar' ? arabic : english;
  }
}
