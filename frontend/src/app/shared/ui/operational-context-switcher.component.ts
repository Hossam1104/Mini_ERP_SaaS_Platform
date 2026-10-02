import { Component, OnInit, inject } from '@angular/core';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-operational-context-switcher',
  standalone: true,
  template: `
    @if (context.operationalContexts().length > 1) {
      <div class="operational-switcher">
        <label class="operational-switcher__label" for="operational-context-select">
          {{ language.text('operationalContext') }}
        </label>
        <select
          id="operational-context-select"
          class="operational-switcher__select"
          [value]="context.selectedOperationalContextId() ?? ''"
          [disabled]="context.switching()"
          (change)="switchFromEvent($event)"
        >
          @for (candidate of context.operationalContexts(); track candidate.contextId) {
            <option [value]="candidate.contextId">{{ candidate.displayName }} - {{ kindLabel(candidate.kind) }}</option>
          }
        </select>
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .operational-switcher { display: grid; gap: 0.25rem; min-width: 12rem; }
    .operational-switcher__label { color: var(--ink-muted); font-size: 13px; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
    .operational-switcher__select { min-height: 44px; max-width: 17rem; border: 1px solid var(--line-strong); border-radius: 12px; padding: 0.45rem 0.6rem; color: var(--ink); background: var(--surface-raised); box-shadow: 0 3px 9px rgb(15 26 48 / 6%); font: 600 14px/1.2 var(--font-sans); }
    .operational-switcher__select:focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
    @media (max-width: 520px) { .operational-switcher { min-width: 0; width: 100%; } .operational-switcher__select { max-width: none; width: 100%; } }
  `,
})
export class OperationalContextSwitcherComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);

  ngOnInit(): void {
    if (!this.context.entry()) void this.context.loadEntry();
  }

  async switchFromEvent(event: Event): Promise<void> {
    const value = (event.target as HTMLSelectElement).value;
    if (value) {
      await this.context.switchOperationalContext(value);
    }
  }

  kindLabel(kind: string): string {
    return kind === 'Branch'
      ? this.language.text('branchContext')
      : this.language.text('companyContext');
  }
}
