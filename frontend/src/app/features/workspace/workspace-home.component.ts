import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/presentation/theme.service';
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
          <div class="overview-hero__art" aria-hidden="true"><span class="hero-orbit hero-orbit--one"></span><span class="hero-orbit hero-orbit--two"></span><span class="hero-orbit hero-orbit--three"></span>@if (!tenantLogoUrl()) { <span class="hero-mark">M</span> }</div>
          @if (tenantLogoUrl()) { <span class="overview-hero__tenant-logo"><svg class="tenant-logo-filter" aria-hidden="true" focusable="false" width="0" height="0"><filter id="tenant-logo-tint" x="0" y="0" width="1" height="1" filterUnits="objectBoundingBox" color-interpolation-filters="sRGB"><feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  -0.53 -1.79 -0.18 0 2.4" result="mask"/><feComposite in="mask" in2="SourceAlpha" operator="in" result="maskA"/><feFlood class="tenant-logo-flood" result="tint"/><feComposite in="tint" in2="maskA" operator="in"/></filter></svg><img [class.tenant-logo-tinted]="tenantLogoUsesTint()" [src]="tenantLogoUrl()" [alt]="context.entry()?.branding?.logoAltText" /></span> } <label class="overview-search"><svg class="icon" aria-hidden="true"><use href="#icon-search" /></svg><input type="search" [value]="searchQuery()" (input)="searchQuery.set($any($event.target).value)" [placeholder]="label('Search modules and destinations', 'ابحث في الوحدات والصفحات')" [attr.aria-label]="label('Search modules and destinations', 'ابحث في الوحدات والصفحات')" /></label>
        </header>

        <section class="overview-modules" aria-labelledby="capability-title">
          <div class="overview-section-heading">
            <div><p class="eyebrow">{{ language.text('tenantWorkspace') }}</p><h2 id="capability-title">{{ language.text('continueWith') }}</h2></div>
            <span class="overview-section-heading__hint">{{ label('Your workspace', 'مساحة عملك') }}</span>
          </div>
          <div class="module-chips" role="group" [attr.aria-label]="label('Filter modules', 'تصفية الوحدات')">
            <button type="button" class="button module-chip" [class.is-active]="selectedGroup() === 'all'" [class.button--primary]="selectedGroup() === 'all'" [attr.aria-pressed]="selectedGroup() === 'all'" (click)="selectedGroup.set('all')">{{ label('All modules', 'كل الوحدات') }}</button>
            @for (group of navigationGroups; track group.id) {
              <button type="button" class="button module-chip" [class.is-active]="selectedGroup() === group.id" [class.button--primary]="selectedGroup() === group.id" [attr.aria-pressed]="selectedGroup() === group.id" (click)="selectedGroup.set(group.id)">{{ navigationLabel(group) }}</button>
            }
          </div>
          <div class="capability-grid">
            @for (module of visibleModules(); track module.path; let index = $index) {
              <a class="module-card" [class]="'module-card module-card--' + module.groupId" [routerLink]="module.path" [style.--card-order]="index" [attr.aria-label]="label('View links for ' + module.labelEn, 'عرض روابط ' + module.labelAr)">
                <span class="module-card__banner" aria-hidden="true"><svg class="icon"><use [attr.href]="'#icon-' + module.icon" /></svg><span class="module-card__art-orbit"></span></span>
                <span class="module-card__tag">{{ label(module.groupLabelEn, module.groupLabelAr) }}</span>
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
  styles: `:host { display: block; }
    .tenant-overview { display: grid; gap: clamp(1.25rem, 2.8vw, 2rem); }
    .overview-hero { position: relative; display: grid; min-height: 260px; grid-template-columns: minmax(0, 1fr) minmax(0, clamp(220px, 34vw, 500px)); align-items: center; gap: .5rem 2rem; overflow: hidden; border: 1px solid color-mix(in srgb, var(--accent) 22%, var(--line)); border-radius: 24px; padding: clamp(1.35rem, 3vw, 2.6rem); background: radial-gradient(ellipse at 12% 8%, color-mix(in srgb, var(--accent) 13%, transparent), transparent 46%), linear-gradient(115deg, color-mix(in srgb, var(--accent-soft) 78%, var(--surface-glass)), var(--surface-glass) 68%, var(--surface-raised)); box-shadow: var(--shadow-glass); backdrop-filter: blur(14px) saturate(145%); animation: card-enter 480ms ease both; }
    .overview-hero__content { position: relative; z-index: 1; max-width: 52rem; grid-column: 1; grid-row: 1; }
    .eyebrow { margin: 0 0 .55rem; color: var(--accent); font-size: 13px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
    h1 { max-width: 48rem; margin: 0; color: var(--ink-strong); font: 800 clamp(2rem, 4vw, 3.35rem)/1.08 var(--font-display); letter-spacing: -.02em; }
    .overview-hero__lead { max-width: 43rem; margin: .75rem 0 1rem; color: var(--ink-muted); font-size: 1rem; line-height: 1.6; }
    .overview-manage { display: inline-flex; min-height: 42px; align-items: center; gap: .5rem; border: 1px solid color-mix(in srgb, var(--accent) 28%, var(--line)); border-radius: 999px; padding: .45rem .9rem; color: var(--accent-strong); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: 14px; font-weight: 700; text-decoration: none; transition: transform var(--motion-fast) ease, box-shadow var(--motion-fast) ease; }
    .overview-manage .icon { width: 18px; height: 18px; }
    .overview-manage:hover { box-shadow: var(--shadow-card); transform: translateY(-2px); }
    .overview-hero__art { position: relative; width: 100%; max-width: 500px; aspect-ratio: 500 / 280; height: auto; grid-column: 2; grid-row: 1 / span 2; }
    .overview-hero__tenant-logo { position: relative; z-index: 2; display: flex; grid-column: 2; grid-row: 1 / span 2; align-items: center; justify-content: center; max-width: 100%; justify-self: center; }
    .overview-hero__tenant-logo img { display: block; width: auto; max-width: 92%; max-height: 210px; height: auto; object-fit: contain; }
    .overview-hero__tenant-logo img.tenant-logo-tinted { filter: url(#tenant-logo-tint); }
    .tenant-logo-filter { position: absolute; width: 0; height: 0; }
    .tenant-logo-flood { flood-color: var(--accent); }
    .hero-orbit { position: absolute; inset-block-start: 50%; inset-inline-start: 50%; display: block; border: 1px solid color-mix(in srgb, var(--accent) 27%, transparent); border-radius: 50%; transform: translate(-50%, -50%); }
    .hero-orbit--one { width: 50%; aspect-ratio: 1; }
    .hero-orbit--two { width: 76%; height: 55%; transform: translate(-50%, -50%) rotate(-35deg); }
    .hero-orbit--three { width: 76%; height: 55%; transform: translate(-50%, -50%) rotate(35deg); }
    :host-context(html[dir='rtl']) .hero-orbit { transform: translate(50%, -50%); }
    :host-context(html[dir='rtl']) .hero-orbit--two { transform: translate(50%, -50%) rotate(35deg); }
    :host-context(html[dir='rtl']) .hero-orbit--three { transform: translate(50%, -50%) rotate(-35deg); }
    .hero-mark { position: absolute; inset: 50% auto auto 50%; display: grid; width: 74px; height: 74px; place-items: center; border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--line)); border-radius: 24px; color: var(--action-text); background: linear-gradient(140deg, var(--accent-action), var(--accent-strong)); box-shadow: 0 14px 32px color-mix(in srgb, var(--accent) 28%, transparent); font: 800 2.1rem/1 var(--font-display); transform: translate(-50%, -50%) rotate(-5deg); }
    .overview-modules { display: grid; gap: .9rem; }
    .overview-section-heading { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding-inline: .2rem; }
    .overview-section-heading h2 { margin: 0; color: var(--ink-strong); font: 800 clamp(1.3rem, 2.3vw, 1.8rem)/1.15 var(--font-display); }
    .overview-section-heading .eyebrow { margin-block-end: .25rem; }
    .overview-section-heading__hint { color: var(--ink-muted); font-size: 14px; }
    .module-chips { display: flex; gap: .5rem; overflow-x: auto; padding: .15rem .15rem .45rem; }
    .module-chip { flex: none; }
    .capability-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 225px), 1fr)); gap: .9rem; }
    .module-card { display: grid; min-width: 0; gap: .7rem; border: 1px solid color-mix(in srgb, var(--accent) 16%, var(--line)); border-radius: 18px; padding: .8rem; color: var(--ink); background: linear-gradient(145deg, color-mix(in srgb, var(--surface-glass) 88%, transparent), color-mix(in srgb, var(--surface-raised) 84%, transparent) 76%); box-shadow: var(--shadow-card), inset 0 1px 0 var(--glass-highlight), inset 0 0 0 1px color-mix(in srgb, var(--glass-highlight) 36%, transparent); backdrop-filter: blur(16px) saturate(155%); text-decoration: none; animation: card-enter 420ms ease both; animation-delay: calc(var(--card-order, 0) * 35ms); transition: border-color var(--motion-fast) ease, box-shadow var(--motion-fast) ease, transform var(--motion-fast) ease; }
    .module-card:hover { border-color: color-mix(in srgb, var(--accent) 42%, var(--line)); box-shadow: var(--shadow-overlay), inset 0 1px 0 var(--glass-highlight); transform: translateY(-5px); }
    .module-card:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .module-card__banner { position: relative; display: grid; min-height: 112px; place-items: center; overflow: hidden; border-radius: 12px; color: var(--accent-strong); background: radial-gradient(circle at 18% 15%, color-mix(in srgb, var(--accent) 24%, white), transparent 40%), linear-gradient(135deg, var(--accent-soft), color-mix(in srgb, var(--surface-raised) 72%, var(--accent-soft))); }
    .module-card__banner::before { position: absolute; inset: 0; background: linear-gradient(145deg, transparent 40%, color-mix(in srgb, var(--accent) 9%, transparent) 41% 53%, transparent 54%), radial-gradient(ellipse at 50% 120%, color-mix(in srgb, var(--accent) 14%, transparent), transparent 65%); content: ''; }
    .module-card__banner .icon { position: relative; z-index: 1; width: 42px; height: 42px; filter: drop-shadow(0 5px 10px color-mix(in srgb, var(--accent) 30%, transparent)); stroke-width: 1.6; }
    .module-card__art-orbit { position: absolute; inset: auto -16px -46px auto; width: 116px; height: 116px; border: 1px solid color-mix(in srgb, var(--accent) 22%, transparent); border-radius: 50%; }
    .module-card--procurement .module-card__banner { color: #14613d; background: radial-gradient(circle at 18% 18%, #d8f4e5, transparent 42%), linear-gradient(135deg, #e7f6ed, color-mix(in srgb, var(--surface-raised) 70%, #a5e1bb)); }
    .module-card--operations .module-card__banner { color: #0b655e; background: radial-gradient(circle at 78% 15%, #c9f2e8, transparent 40%), linear-gradient(135deg, #e4f7f3, color-mix(in srgb, var(--surface-raised) 70%, #90d8ca)); }
    .module-card--finance-sales .module-card__banner { color: #7044a4; background: radial-gradient(circle at 20% 14%, #e9dcff, transparent 42%), linear-gradient(135deg, #f2ecff, color-mix(in srgb, var(--surface-raised) 70%, #c8b0eb)); }
    :host-context(html[data-color-scheme='dark']) .module-card__banner { color: var(--accent-strong); background-color: var(--surface-raised); background-image: radial-gradient(circle at 18% 15%, color-mix(in srgb, var(--accent) 24%, transparent), transparent 40%), linear-gradient(135deg, color-mix(in srgb, var(--accent) 12%, var(--surface-raised)), color-mix(in srgb, var(--accent) 24%, var(--surface-raised))); }
    :host-context(html[data-color-scheme='dark']) .module-card__banner::before { background: linear-gradient(145deg, transparent 40%, color-mix(in srgb, var(--accent) 10%, transparent) 41% 53%, transparent 54%), radial-gradient(ellipse at 50% 120%, color-mix(in srgb, var(--accent) 14%, transparent), transparent 65%); }
    .module-card__tag { justify-self: start; border: 1px solid color-mix(in srgb, var(--accent) 22%, var(--line)); border-radius: 999px; padding: .25rem .6rem; color: var(--accent-strong); background: var(--accent-soft); font-size: 14px; font-weight: 700; }
    .module-card strong { color: var(--ink-strong); font: 700 1.05rem/1.25 var(--font-display); }
    .module-card__view { display: inline-flex; min-height: 40px; align-items: center; justify-content: space-between; gap: .45rem; justify-self: start; border: 1px solid color-mix(in srgb, var(--accent) 25%, var(--line)); border-radius: 999px; padding: .35rem .7rem; color: var(--accent-strong); background: var(--surface-glass); box-shadow: var(--shadow-soft); font-size: 14px; font-weight: 700; }
    .module-card__view .icon { width: 16px; height: 16px; }
    .module-empty { grid-column: 1 / -1; margin: 0; border: 1px dashed var(--line-strong); border-radius: 14px; padding: 1.5rem; color: var(--ink-muted); text-align: center; }
    @media (max-width: 1100px) { .overview-hero { min-height: 0; grid-template-columns: minmax(0, 1fr); } .overview-hero__content { grid-column: 1; grid-row: 1; } .overview-hero__art { grid-column: 1; grid-row: 2; justify-self: center; } .overview-hero__tenant-logo { grid-column: 1; grid-row: 2; justify-self: center; } }
    @media (max-width: 760px) { .overview-hero { padding: 1.25rem; } .overview-hero__tenant-logo img { max-height: 130px; } .hero-mark { width: 58px; height: 58px; border-radius: 19px; font-size: 1.65rem; } .overview-section-heading__hint { display: none; } }
    @media (max-width: 500px) { .overview-hero__tenant-logo { max-width: 220px; } .overview-hero__tenant-logo img { max-height: 96px; } .module-chips { margin-inline: -.2rem; } .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .65rem; } .module-card { padding: .65rem; } .module-card__banner { min-height: 88px; } .module-card__banner .icon { width: 34px; height: 34px; } .module-card strong { font-size: 14px; } .module-card__tag, .module-card__view { font-size: 14px; } }
    @media (max-width: 350px) { .capability-grid { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { .overview-hero, .module-card { animation: none; } }
  `,
})
export class WorkspaceHomeComponent implements OnInit {
  readonly context = inject(ContextService);
  readonly language = inject(LanguageService);
  private readonly theme = inject(ThemeService);
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

  tenantLogoUrl(): string | null {
    const branding = this.context.entry()?.branding;
    return this.theme.darkMode()
      ? branding?.logoDarkUrl ?? branding?.logoLightUrl ?? null
      : branding?.logoLightUrl ?? branding?.logoDarkUrl ?? null;
  }

  tenantLogoUsesTint(): boolean {
    const branding = this.context.entry()?.branding;
    return !!branding?.logoLightUrl && (!this.theme.darkMode() || !branding.logoDarkUrl);
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
