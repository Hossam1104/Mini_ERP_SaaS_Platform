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
      <header class="tenant-overview__intro ui-page-header">
        <div>
          <p class="eyebrow">{{ language.text('tenantOverview') }}</p>
          <h1 id="tenant-overview-title">{{ contextHeading() }}</h1>
          <p class="lede">{{ language.text('tenantOverviewLead') }}</p>
        </div>
        <a class="tenant-overview__link" routerLink="/app/workspaces">{{ language.text('manageContexts') }}</a>
      </header>

      @if (isNoAccess()) {
        <app-status-card
          [title]="language.text('noAccessTitle')"
          [message]="language.text('noAccessMessage')"
          state="denied"
          tone="danger"
        />
      } @else if (isPlatformControlPlane()) {
        <app-status-card
          [title]="language.text('platformControlPlaneTitle')"
          [message]="language.text('platformControlPlaneMessage')"
          state="unknown"
          tone="neutral"
        />
      } @else {
        <section class="overview-panel ui-surface ui-surface--glass" aria-labelledby="capability-title">
          <div class="overview-panel__heading">
            <div>
              <p class="eyebrow eyebrow--quiet">{{ language.text('tenantWorkspace') }}</p>
              <h2 id="capability-title">{{ language.text('continueWith') }}</h2>
            </div>
            <span class="overview-panel__marker" aria-hidden="true">01</span>
          </div>

          <div class="capability-grid">
            <a class="capability-card capability-card--primary" routerLink="/app/master-data/categories">
              <span class="capability-card__index">01</span>
              <strong>{{ language.text('masterData') }}</strong>
              <span>{{ language.text('masterDataOverview') }}</span>
            </a>
            <a class="capability-card" routerLink="/app/price-lists">
              <span class="capability-card__index">02</span>
              <strong>{{ language.text('priceLists') }}</strong>
              <span>{{ language.text('priceListsOverview') }}</span>
            </a>
            <a class="capability-card" routerLink="/app/procurement/purchase-requests">
              <span class="capability-card__index">03</span>
              <strong>{{ language.text('purchaseRequestsNavLabel') }}</strong>
              <span>{{ language.text('purchaseRequestsOverview') }}</span>
            </a>
            <a class="capability-card" routerLink="/app/procurement/supplier-quotations">
              <span class="capability-card__index">04</span>
              <strong>{{ language.text('supplierQuotationsNavLabel') }}</strong>
              <span>{{ language.text('supplierQuotationsOverview') }}</span>
            </a>
          </div>
        </section>

        @if (!hasOperationalContext() && context.entry()?.entryMode === 'TenantHost') {
          <app-status-card
            [title]="language.text('operationalContextPending')"
            [message]="language.text('operationalContextPendingMessage')"
            state="pending"
            tone="neutral"
          />
        }
      }
    </section>
  `,
  styles: `
    :host { display: block; }
    .tenant-overview { display: grid; gap: 1.25rem; }
    .tenant-overview__intro { align-items: flex-end; }
    .eyebrow { margin: 0 0 0.55rem; color: var(--accent-strong); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    .eyebrow--quiet { color: var(--ink-muted); margin-bottom: 0.35rem; }
    h1 { max-width: 42rem; margin: 0; color: var(--ink); font: 750 clamp(2rem, 5vw, 3.4rem)/1.02 var(--font-display); letter-spacing: -0.055em; }
    .lede { max-width: 39rem; margin: 1rem 0 0; color: var(--ink-muted); font-size: 1rem; line-height: 1.65; }
    .tenant-overview__link { border: 1px solid var(--line-strong); border-radius: var(--radius-sm); padding: 0.65rem 0.8rem; color: var(--accent-strong); background: var(--surface-raised); font-size: 0.76rem; font-weight: 800; text-decoration: none; }
    .tenant-overview__link:hover { border-color: var(--accent-strong); background: var(--accent-soft); }
    .overview-panel { display: grid; gap: 1.4rem; padding: clamp(1.25rem, 3vw, 2rem); }
    .overview-panel__heading { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
    .overview-panel h2 { margin: 0; color: var(--ink); font: 700 clamp(1.35rem, 3vw, 2rem)/1.1 var(--font-display); letter-spacing: -0.03em; }
    .overview-panel__marker { color: var(--accent); font: 800 2.2rem/1 var(--font-display); opacity: 0.8; }
    .capability-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.85rem; }
    .capability-card { display: grid; gap: 0.55rem; min-height: 8rem; border: 1px solid var(--line); border-radius: 0.85rem; padding: 1rem; color: var(--ink); background: var(--surface-raised); text-decoration: none; transition: transform 150ms ease, border-color 150ms ease, background 150ms ease; }
    .capability-card:hover { transform: translateY(-2px); border-color: var(--accent); background: var(--accent-soft); }
    .capability-card:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
    .capability-card--primary { border-color: color-mix(in srgb, var(--accent) 45%, var(--line)); background: linear-gradient(135deg, var(--accent-soft), var(--surface-raised) 75%); }
    .capability-card__index { color: var(--accent-strong); font-size: 0.7rem; font-weight: 850; letter-spacing: 0.1em; }
    .capability-card strong { font: 700 1.1rem/1.15 var(--font-display); }
    .capability-card span:last-child { color: var(--ink-muted); font-size: 0.8rem; line-height: 1.45; }
    @media (max-width: 620px) { .tenant-overview__intro { align-items: flex-start; } .capability-grid { grid-template-columns: 1fr; } }
  `,
})
export class WorkspaceHomeComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  ngOnInit(): void {
    if (!this.context.entry()) {
      void this.context.loadEntry();
    }
  }

  contextHeading(): string {
    return this.context.entry()?.candidateTenantDisplayName
      ?? this.context.entry()?.branding.displayName
      ?? this.language.text('tenantOverview');
  }

  isNoAccess(): boolean {
    return this.context.entry()?.entryMode === 'NoAccess';
  }

  isPlatformControlPlane(): boolean {
    return this.context.entry()?.entryMode === 'PlatformAdminHost';
  }

  hasOperationalContext(): boolean {
    return this.context.selectedOperationalContextId() !== null
      || this.context.operationalContexts().length === 0;
  }
}
