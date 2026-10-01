import { Component, OnInit, inject } from '@angular/core';
import { ContextSwitcherComponent } from '../../shared/ui/context-switcher.component';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { OperationalContextSwitcherComponent } from '../../shared/ui/operational-context-switcher.component';

@Component({
  selector: 'app-tenant-select',
  standalone: true,
  imports: [ContextSwitcherComponent, OperationalContextSwitcherComponent],
  template: `
    <section class="tenant-select ui-page" aria-labelledby="tenant-select-title">
      <header class="ui-page-header ui-page-header--compact">
        <div>
          <p class="eyebrow">{{ language.text(isTenantChooser() ? 'tenantChooserKicker' : 'operationalContext') }}</p>
          <h1 id="tenant-select-title">{{ language.text(isTenantChooser() ? 'tenantChooserTitle' : 'manageContexts') }}</h1>
          <p class="lede">{{ language.text(isTenantChooser() ? 'tenantChooserLead' : 'contextServerReference') }}</p>
        </div>
        <span class="ui-status-chip ui-status-chip--accent">{{ language.text('serverAuthority') }}</span>
      </header>

      @if (isTenantChooser()) {
        <div class="ui-surface ui-surface--glass workspace-selector">
          <app-context-switcher />
        </div>
      }

      @if (tenantDisplayName(); as tenantName) {
        <section class="ui-surface ui-surface--glass context-summary" [attr.aria-label]="language.text('currentTenant')">
          <div class="context-summary__item">
            <p class="context-summary__label">{{ language.text('currentTenant') }}</p>
            <p class="context-summary__value">{{ tenantName }}</p>
          </div>
          @if (context.currentOperationalContext(); as activeContext) {
            <div class="context-summary__item">
              <p class="context-summary__label">{{ language.text('operationalContext') }}</p>
              <p class="context-summary__value">{{ activeContext.displayName }} · {{ language.text(activeContext.kind === 'Branch' ? 'branchContext' : 'companyContext') }}</p>
            </div>
          } @else if (context.operationalContexts().length > 0) {
            <p class="context-summary__pending" role="status">{{ language.text('operationalContextPending') }}</p>
          }
          <app-operational-context-switcher />
        </section>
      }
    </section>
  `,
  styles: `
    :host { display: block; }
    .tenant-select { display: grid; gap: 1rem; }
    .workspace-selector { max-width: 50rem; }
    .context-summary { display: grid; gap: 1rem; max-width: 50rem; }
    .context-summary__item { display: grid; gap: 0.25rem; }
    .context-summary__label { margin: 0; color: var(--ink-muted); font-size: 0.78rem; font-weight: 700; }
    .context-summary__value { margin: 0; color: var(--ink); font: 650 1.1rem/1.3 var(--font-display); }
    .context-summary__pending { margin: 0; color: var(--ink-muted); }
    .eyebrow { margin: 0; color: var(--accent-strong); font-size: 0.72rem; font-weight: 800; letter-spacing: 0.13em; text-transform: uppercase; }
    h1 { margin: 0; color: var(--ink); font: 750 clamp(1.9rem, 4vw, 2.8rem)/1.05 var(--font-display); letter-spacing: -0.045em; }
    .lede { max-width: 42rem; margin: 0.75rem 0 0; color: var(--ink-muted); line-height: 1.65; }
  `,
})
export class TenantSelectComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  ngOnInit(): void {
    if (!this.context.entry() && !this.context.loading()) void this.context.loadEntry();
  }

  isTenantChooser(): boolean {
    const entry = this.context.entry();
    return entry?.entryMode === 'CommonHost' && entry.authorizedTenants.length > 1;
  }

  tenantDisplayName(): string | null {
    const entry = this.context.entry();
    if (!entry?.candidateTenantId) return null;

    const tenant = entry.authorizedTenants.find((candidate) => candidate.tenantId === entry.candidateTenantId);
    const arabicName = tenant?.arabicDisplayName?.trim() || entry.branding.arabicDisplayName?.trim();
    const englishName = entry.candidateTenantDisplayName
      ?? tenant?.displayName
      ?? (entry.entryMode === 'TenantHost' ? entry.branding.displayName : null);
    return this.language.language() === 'ar' && arabicName ? arabicName : englishName ?? null;
  }
}
