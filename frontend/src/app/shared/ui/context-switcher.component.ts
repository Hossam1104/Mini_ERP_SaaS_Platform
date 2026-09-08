import { Component, OnInit, computed, inject } from '@angular/core';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { AuthService } from '../../core/auth/auth.service';
import { StatusCardComponent } from './status-card.component';
import { Router } from '@angular/router';

@Component({
  selector: 'app-context-switcher',
  standalone: true,
  imports: [StatusCardComponent],
  template: `
    <section class="context-switcher" aria-labelledby="context-switcher-title">
      <div class="context-switcher__heading">
        <div>
          <p class="eyebrow">{{ language.text('availableTenants') }}</p>
          <h2 id="context-switcher-title">{{ contextLabel() }}</h2>
        </div>
        <span class="context-switcher__pill" [class.context-switcher__pill--support]="isSupport()">
          {{ pathLabel() }}
        </span>
      </div>

      @if (context.loading()) {
        <app-status-card
          [title]="language.text('loading')"
          [message]="language.text('contextLoading')"
          state="loading"
          tone="neutral"
        />
      } @else if (tenantContexts().length === 0) {
        <app-status-card
          [title]="language.text('empty')"
          [message]="language.text('contextEmpty')"
          state="empty"
          tone="neutral"
        />
      } @else {
        <label class="field-label ui-field__label" for="workspace-select">{{ language.text('chooseTenant') }}</label>
        <select
          id="workspace-select"
          class="context-select ui-field__control"
          [value]="auth.session()?.selectedContextId ?? ''"
          [disabled]="context.switching()"
          (change)="switchFromEvent($event)"
        >
          <option value="" disabled>{{ language.text('chooseTenant') }}</option>
          @for (candidate of tenantContexts(); track candidate.contextId) {
            <option [value]="candidate.contextId">
              {{ candidate.displayName }} · {{ kindLabel(candidate.kind) }}
            </option>
          }
        </select>
        @if (context.switching()) {
          <p class="context-switcher__hint" role="status">{{ language.text('contextSwitching') }}</p>
        }
      }

      @if (context.lastError(); as error) {
        <app-status-card
          [title]="language.text('error')"
          [message]="errorMessage(error.code)"
          state="failed"
          tone="danger"
        />
      }
    </section>
  `,
  styles: `
    :host { display: block; }
    .context-switcher { display: grid; gap: 1rem; }
    .context-switcher__heading { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .eyebrow { margin: 0 0 0.25rem; color: var(--ink-muted); font-size: 0.73rem; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; }
    h2 { margin: 0; color: var(--ink); font: 650 1.2rem/1.2 var(--font-display); }
    .context-switcher__pill { border: 1px solid color-mix(in srgb, var(--accent) 40%, var(--line)); border-radius: 99px; color: var(--accent-strong); background: var(--accent-soft); padding: 0.38rem 0.65rem; font-size: 0.75rem; font-weight: 750; white-space: nowrap; }
    .context-switcher__pill--support { color: var(--support); border-color: color-mix(in srgb, var(--support) 38%, var(--line)); background: var(--support-soft); }
    .context-switcher__hint { margin: 0; color: var(--ink-muted); font-size: 0.88rem; }
    .field-label { color: var(--ink-muted); font-size: 0.78rem; font-weight: 700; }
    .context-select { width: 100%; border: 1px solid var(--line-strong); border-radius: 0.75rem; padding: 0.75rem 0.85rem; color: var(--ink); background: var(--surface); font: inherit; }
    .context-select:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
    @media (max-width: 560px) { .context-switcher__heading { flex-direction: column; } }
  `,
})
export class ContextSwitcherComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  readonly router = inject(Router);
  readonly tenantContexts = computed(() =>
    this.context.contexts().filter((candidate) => candidate.kind === 'OrdinaryMembership'),
  );

  ngOnInit(): void {
    if (this.context.contexts().length === 0) {
      void this.context.load();
    }
  }

  async switchFromEvent(event: Event): Promise<void> {
    const value = (event.target as HTMLSelectElement).value;
    if (value) {
      const switched = await this.context.switchContext(value);
      if (switched && this.context.entry()?.entryMode === 'CommonHost') {
        await this.context.loadEntry();
        await this.router.navigate(['/app']);
      }
    }
  }

  contextLabel(): string {
    const current = this.context.currentContext();
    if (current) {
      return current.displayName;
    }
    return this.auth.session()?.selectedPath === 'PlatformGovernanceContext'
      ? this.language.text('platformGovernance')
      : this.language.text('noTenantContext');
  }

  pathLabel(): string {
    const path = this.auth.session()?.selectedPath;
    return path ? this.kindLabel(path) : this.language.text('chooseTenant');
  }

  isSupport(): boolean {
    return this.auth.session()?.selectedPath === 'SupportGrant';
  }

  kindLabel(kind: string): string {
    if (kind === 'SupportGrant') return this.language.text('supportGrant');
    if (kind === 'PlatformGovernanceContext') return this.language.text('platformGovernance');
    return this.language.text('tenantMembership');
  }

  errorMessage(code: string): string {
    if (code === 'access_denied') return this.language.text('accessDenied');
    if (code === 'context_version_conflict') return this.language.text('staleContext');
    if (code === 'network_error') return this.language.text('networkError');
    return this.language.text('requestError');
  }
}
