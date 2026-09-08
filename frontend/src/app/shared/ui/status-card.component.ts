import { Component, Input, inject } from '@angular/core';
import { LanguageService } from '../../core/i18n/language.service';

export type StatusCardTone = 'neutral' | 'danger' | 'success' | 'accent';
export type UiState = 'loading' | 'empty' | 'denied' | 'failed' | 'unavailable' | 'unknown' | 'pending';

@Component({
  selector: 'app-status-card',
  standalone: true,
  template: `
    <section
      class="status-card"
      [class]="'status-card status-card--' + tone"
      [attr.aria-label]="title"
      [attr.role]="state ? 'status' : null"
      [attr.aria-live]="state ? 'polite' : null"
      [attr.aria-busy]="state === 'loading' ? 'true' : null"
    >
      <div class="status-card__marker" aria-hidden="true"></div>
      <div>
        @if (state) {
          <span class="status-card__state">{{ stateLabel() }}</span>
        }
        <h2>{{ title }}</h2>
        <p>{{ message }}</p>
      </div>
    </section>
  `,
  styles: `
    :host { display: block; }
    .status-card {
      display: flex;
      gap: 0.9rem;
      align-items: flex-start;
      border: 1px solid var(--line);
      border-radius: 1rem;
      padding: 1rem 1.1rem;
      background: var(--surface-raised);
    }
    .status-card__marker { width: 0.55rem; height: 0.55rem; margin-top: 0.35rem; border-radius: 50%; background: var(--ink-muted); flex: 0 0 auto; }
    .status-card--danger { border-color: color-mix(in srgb, var(--danger) 32%, var(--line)); background: color-mix(in srgb, var(--danger) 6%, var(--surface-raised)); }
    .status-card--danger .status-card__marker { background: var(--danger); }
    .status-card--success .status-card__marker { background: var(--success); }
    .status-card--accent .status-card__marker { background: var(--accent); }
    .status-card__state { display: block; margin-bottom: 0.2rem; color: var(--ink-muted); font-size: 0.68rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
    h2 { margin: 0; font: 650 0.86rem/1.35 var(--font-sans); color: var(--ink); }
    p { margin: 0.25rem 0 0; color: var(--ink-muted); font-size: 0.9rem; line-height: 1.55; }
  `,
})
export class StatusCardComponent {
  private readonly language = inject(LanguageService, { optional: true });
  @Input() title = '';
  @Input() message = '';
  @Input() tone: StatusCardTone = 'neutral';
  @Input() state: UiState | null = null;

  stateLabel(): string {
    const key = this.state;
    if (key === 'loading') return this.language?.text('loading') ?? 'Loading';
    if (key === 'empty') return this.language?.text('empty') ?? 'Empty';
    if (key === 'denied') return this.language?.text('accessDenied') ?? 'Access denied';
    if (key === 'failed') return this.language?.text('error') ?? 'Error';
    if (key === 'unavailable') return this.language?.text('unavailableState') ?? 'Unavailable';
    if (key === 'pending') return this.language?.text('pendingState') ?? 'Pending';
    return this.language?.text('unknownState') ?? 'Unknown';
  }
}
