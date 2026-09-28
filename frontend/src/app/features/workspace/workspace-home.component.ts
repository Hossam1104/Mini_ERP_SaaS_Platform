import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';
import { NAVIGATION_GROUPS } from '../shell/navigation.config';

interface OverviewModule {
  path: string;
  labelEn: string;
  labelAr: string;
  icon: string;
  groupId: string;
  groupLabelEn: string;
  groupLabelAr: string;
}

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
            <a class="overview-manage" routerLink="/app/workspaces"><svg class="icon" aria-hidden="true"><use href="#icon-sliders" /></svg>{{ language.text('manageContexts') }}</a>
          </div>
          <div class="overview-hero__art" aria-hidden="true"><span class="hero-orbit hero-orbit--one"></span><span class="hero-orbit hero-orbit--two"></span><span class="hero-orbit hero-orbit--three"></span><span class="hero-mark">M</span></div>
          <label class="overview-search"><svg class="icon" aria-hidden="true"><use href="#icon-search" /></svg><input type="search" [value]="searchQuery()" (input)="searchQuery.set($any($event.target).value)" [placeholder]="label('Search modules and destinations', 'ابحث في الوحدات والصفحات')" [attr.aria-label]="label('Search modules and destinations', 'ابحث في الوحدات والصفحات')" /></label>
        </header>

        <section class="overview-modules" aria-labelledby="capability-title">
          <div class="overview-section-heading">
            <div><p class="eyebrow">{{ language.text('tenantWorkspace') }}</p><h2 id="capability-title">{{ language.text('continueWith') }}</h2></div>
            <span class="overview-section-heading__hint">{{ label('Your workspace', 'مساحة عملك') }}</span>
          </div>
          <div class="module-chips" role="group" [attr.aria-label]="label('Filter modules', 'تصفية الوحدات')">
            <button type="button" class="module-chip" [class.is-active]="selectedGroup() === 'all'" [attr.aria-pressed]="selectedGroup() === 'all'" (click)="selectedGroup.set('all')">{{ label('All modules', 'كل الوحدات') }}</button>
            @for (group of navigationGroups; track group.id) {
              <button type="button" class="module-chip" [class.is-active]="selectedGroup() === group.id" [attr.aria-pressed]="selectedGroup() === group.id" (click)="selectedGroup.set(group.id)">{{ navigationLabel(group) }}</button>
            }
          </div>
          <div class="capability-grid">
            @for (module of visibleModules(); track module.path; let index = $index) {
              <a class="module-card" [class]="'module-card module-card--' + module.groupId" [routerLink]="module.path" [style.--card-order]="index" [attr.aria-label]="label('View links for ' + module.labelEn, 'عرض روابط ' + module.labelAr)">
                <span class="module-card__banner" aria-hidden="true"><svg class="icon"><use [attr.href]="'#icon-' + module.icon" /></svg><span class="module-card__art-orbit"></span></span>
                <span class="module-card__tag">{{ navigationLabel(module) }}</span>
                <strong>{{ navigationLabel(module) }}</strong>
                <span class="module-card__view">{{ label('View links', 'عرض الروابط') }}<svg class="icon icon--arrow-up-right" aria-hidden="true"><use href="#icon-arrow-up-right" /></svg></span>
              </a>
            } @empty {
              <p class="module-empty" role="status">{{ label('No matching destinations', 'لا توجد صفحات مطابقة') }}</p>
            }
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
    .overview-hero { position: relative; display: grid; min-height: 260px; grid-template-columns: minmax(0, 1fr) 190px; align-items: center; gap: .5rem 2rem; overflow: hidden; border: 1px solid color-mix(in srgb, var(--accent) 22%, var(--line)); border-radius: 24px; padding: clamp(1.35rem, 3vw, 2.6rem); background: radial-gradient(ellipse at 12% 8%, color-mix(in srgb, var(--accent) 13%, transparent), transparent 46%), linear-gradient(115deg, color-mix(in srgb, var(--accent-soft) 78%, var(--surface-glass)), var(--surface-glass) 68%, var(--surface-raised)); box-shadow: var(--shadow-glass); backdrop-filter: blur(14px) saturate(145%); animation: card-enter 480ms ease both; }
    .overview-hero__content { position: relative; z-index: 1; max-width: 52rem; grid-column: 1; grid-row: 1; }
    .eyebrow { margin: 0 0 .55rem; color: var(--accent); font-size: 13px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
    h1 { max-width: 48rem; margin: 0; color: var(--ink-strong); font: 800 clamp(2rem, 4vw, 3.35rem)/1.08 var(--font-display); letter-spacing: -.02em; }
    .overview-hero__lead { max-width: 43rem; margin: .75rem 0 1rem; color: var(--ink-muted); font-size: 1rem; line-height: 1.6; }
    .overview-manage { display: inline-flex; min-height: 42px; align-items: center; gap: .5rem; border: 1px solid color-mix(in srgb, var(--accent) 28%, var(--line)); border-radius: 999px; padding: .45rem .9rem; color: var(--accent-strong); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: 14px; font-weight: 700; text-decoration: none; transition: transform var(--motion-fast) ease, box-shadow var(--motion-fast) ease; }
    .overview-manage .icon { width: 18px; height: 18px; }
    .overview-manage:hover { box-shadow: var(--shadow-card); transform: translateY(-2px); }
    .overview-search { position: relative; z-index: 2; display: flex; width: min(100%, 38rem); min-height: 48px; grid-column: 1; grid-row: 2; align-items: center; gap: .7rem; border: 1px solid color-mix(in srgb, var(--accent) 25%, var(--line-strong)); border-radius: 13px; padding-inline: .8rem; color: var(--accent); background: var(--surface-raised); box-shadow: 0 8px 22px color-mix(in srgb, var(--accent) 12%, transparent), inset 0 1px var(--control-gloss); }
    .overview-search .icon { width: 19px; height: 19px; }
    .overview-search input { width: 100%; min-width: 0; min-height: 44px; border: 0 !important; padding: 0; color: var(--ink); background: transparent !important; box-shadow: none !important; outline: 0; }
    .overview-search:focus-within { border-color: var(--accent); box-shadow: 0 0 0 4px color-mix(in srgb, var(--accent) 20%, transparent), 0 8px 22px color-mix(in srgb, var(--accent) 12%, transparent); }
    .overview-hero__art { position: relative; width: 190px; height: 150px; grid-column: 2; grid-row: 1 / span 2; }
    .hero-orbit { position: absolute; inset-block-start: 50%; inset-inline-start: 50%; display: block; border: 1px solid color-mix(in srgb, var(--accent) 27%, transparent); border-radius: 50%; transform: translate(-50%, -50%); }
    .hero-orbit--one { width: 145px; height: 145px; }
    .hero-orbit--two { width: 190px; height: 110px; transform: translate(-50%, -50%) rotate(-35deg); }
    .hero-orbit--three { width: 190px; height: 110px; transform: translate(-50%, -50%) rotate(35deg); }
    .hero-mark { position: absolute; inset: 50% auto auto 50%; display: grid; width: 74px; height: 74px; place-items: center; border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--line)); border-radius: 24px; color: var(--action-text); background: linear-gradient(140deg, var(--accent-action), var(--accent-strong)); box-shadow: 0 14px 32px color-mix(in srgb, var(--accent) 28%, transparent); font: 800 2.1rem/1 var(--font-display); transform: translate(-50%, -50%) rotate(-5deg); }
    .overview-modules { display: grid; gap: .9rem; }
    .overview-section-heading { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding-inline: .2rem; }
    .overview-section-heading h2 { margin: 0; color: var(--ink-strong); font: 800 clamp(1.3rem, 2.3vw, 1.8rem)/1.15 var(--font-display); }
    .overview-section-heading .eyebrow { margin-block-end: .25rem; }
    .overview-section-heading__hint { color: var(--ink-muted); font-size: 14px; }
    .module-chips { display: flex; gap: .5rem; overflow-x: auto; padding: .15rem .15rem .45rem; }
    .module-chip { min-height: 40px; flex: none; border: 1px solid var(--line); border-radius: 999px; padding: .45rem .9rem; color: var(--ink-muted); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: 14px; font-weight: 700; transition: color var(--motion-fast) ease, background var(--motion-fast) ease, border-color var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .module-chip:hover { border-color: var(--accent); color: var(--accent); transform: translateY(-1px); }
    .module-chip.is-active { border-color: var(--accent); color: var(--action-text); background: var(--accent-action); }
    .capability-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 225px), 1fr)); gap: .9rem; }
    .module-card { display: grid; min-width: 0; gap: .7rem; border: 1px solid color-mix(in srgb, var(--accent) 12%, var(--line)); border-radius: 18px; padding: .8rem; color: var(--ink); background: linear-gradient(145deg, var(--surface-glass), var(--surface-raised) 76%); box-shadow: var(--shadow-card); text-decoration: none; animation: card-enter 420ms ease both; animation-delay: calc(var(--card-order, 0) * 35ms); transition: border-color var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .module-card:hover { border-color: color-mix(in srgb, var(--accent) 35%, var(--line)); box-shadow: var(--shadow-overlay); transform: translateY(-4px); }
    .module-card:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .module-card__banner { position: relative; display: grid; min-height: 112px; place-items: center; overflow: hidden; border-radius: 12px; color: var(--accent-strong); background: radial-gradient(circle at 18% 15%, color-mix(in srgb, var(--accent) 24%, white), transparent 40%), linear-gradient(135deg, var(--accent-soft), color-mix(in srgb, var(--surface-raised) 72%, var(--accent-soft))); }
    .module-card__banner::before { position: absolute; inset: 0; background: linear-gradient(145deg, transparent 40%, color-mix(in srgb, var(--accent) 9%, transparent) 41% 53%, transparent 54%), radial-gradient(ellipse at 50% 120%, color-mix(in srgb, var(--accent) 14%, transparent), transparent 65%); content: ''; }
    .module-card__banner .icon { position: relative; z-index: 1; width: 42px; height: 42px; filter: drop-shadow(0 5px 10px color-mix(in srgb, var(--accent) 30%, transparent)); stroke-width: 1.6; }
    .module-card__art-orbit { position: absolute; inset: auto -16px -46px auto; width: 116px; height: 116px; border: 1px solid color-mix(in srgb, var(--accent) 22%, transparent); border-radius: 50%; }
    .module-card--procurement .module-card__banner { color: #14613d; background: radial-gradient(circle at 18% 18%, #d8f4e5, transparent 42%), linear-gradient(135deg, #e7f6ed, color-mix(in srgb, var(--surface-raised) 70%, #a5e1bb)); }
    .module-card--operations .module-card__banner { color: #0b655e; background: radial-gradient(circle at 78% 15%, #c9f2e8, transparent 40%), linear-gradient(135deg, #e4f7f3, color-mix(in srgb, var(--surface-raised) 70%, #90d8ca)); }
    .module-card--finance-sales .module-card__banner { color: #7044a4; background: radial-gradient(circle at 20% 14%, #e9dcff, transparent 42%), linear-gradient(135deg, #f2ecff, color-mix(in srgb, var(--surface-raised) 70%, #c8b0eb)); }
    .module-card__tag { justify-self: start; border: 1px solid color-mix(in srgb, var(--accent) 22%, var(--line)); border-radius: 999px; padding: .25rem .6rem; color: var(--accent-strong); background: var(--accent-soft); font-size: 14px; font-weight: 700; }
    .module-card strong { color: var(--ink-strong); font: 700 1.05rem/1.25 var(--font-display); }
    .module-card__view { display: inline-flex; min-height: 40px; align-items: center; justify-content: space-between; gap: .45rem; justify-self: start; border: 1px solid color-mix(in srgb, var(--accent) 25%, var(--line)); border-radius: 999px; padding: .35rem .7rem; color: var(--accent-strong); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: 14px; font-weight: 700; }
    .module-card__view .icon { width: 16px; height: 16px; }
    .module-empty { grid-column: 1 / -1; margin: 0; border: 1px dashed var(--line-strong); border-radius: 14px; padding: 1.5rem; color: var(--ink-muted); text-align: center; }
    @media (max-width: 760px) { .overview-hero { min-height: 0; grid-template-columns: minmax(0, 1fr) 112px; padding: 1.25rem; } .overview-hero__art { width: 112px; height: 120px; } .overview-search { width: 100%; } .hero-orbit--one { width: 116px; height: 116px; } .hero-orbit--two, .hero-orbit--three { width: 150px; height: 90px; } .hero-mark { width: 58px; height: 58px; border-radius: 19px; font-size: 1.65rem; } .overview-section-heading__hint { display: none; } }
    @media (max-width: 500px) { .overview-hero { grid-template-columns: minmax(0, 1fr); } .overview-hero__art { position: absolute; inset-inline-end: -3rem; inset-block-end: 2rem; width: 112px; height: 112px; opacity: .36; } .overview-search { grid-column: 1; grid-row: 2; } .module-chips { margin-inline: -.2rem; } .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .65rem; } .module-card { padding: .65rem; } .module-card__banner { min-height: 88px; } .module-card__banner .icon { width: 34px; height: 34px; } .module-card strong { font-size: 14px; } .module-card__tag, .module-card__view { font-size: 14px; } }
    @media (max-width: 350px) { .capability-grid { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { .overview-hero, .module-card { animation: none; } }
  `,
})
export class WorkspaceHomeComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);
  readonly navigationGroups = NAVIGATION_GROUPS;
  readonly searchQuery = signal('');
  readonly selectedGroup = signal('all');
  readonly moduleDestinations: OverviewModule[] = NAVIGATION_GROUPS.flatMap((group) => group.items.map((item) => ({
    ...item,
    groupId: group.id,
    groupLabelEn: group.labelEn,
    groupLabelAr: group.labelAr,
  })));
  readonly visibleModules = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();
    const group = this.selectedGroup();
    return this.moduleDestinations.filter((module) =>
      (group === 'all' || module.groupId === group)
      && (!query || `${module.labelEn} ${module.labelAr} ${module.groupLabelEn} ${module.groupLabelAr} ${module.path}`.toLocaleLowerCase().includes(query)),
    );
  });

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

  navigationLabel(item: { labelEn: string; labelAr: string }): string {
    return this.label(item.labelEn, item.labelAr);
  }
}
