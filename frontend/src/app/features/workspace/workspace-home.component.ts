import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/presentation/theme.service';
import { StatusCardComponent } from '../../shared/ui/status-card.component';
import { NAVIGATION_GROUPS, type NavigationItem } from '../shell/navigation.config';

const MODULE_CARD_CONTENT: Record<NavigationItem['path'], {
  imageId: string;
  code: string;
  descriptionEn: string;
  descriptionAr: string;
}> = {
  '/app/master-data/categories': {
    imageId: 'master-data', code: 'MD',
    descriptionEn: 'Maintain product categories and shared reference data used across the ERP.',
    descriptionAr: 'إدارة فئات المنتجات والبيانات المرجعية المشتركة بين وحدات النظام.',
  },
  '/app/price-lists': {
    imageId: 'price-lists', code: 'PL',
    descriptionEn: 'Create and manage product price lists and their effective dates.',
    descriptionAr: 'إنشاء قوائم أسعار المنتجات وإدارة فترات سريانها.',
  },
  '/app/master-data/imports': {
    imageId: 'imports', code: 'IMP',
    descriptionEn: 'Import master data files, validate rows, and review import outcomes.',
    descriptionAr: 'استيراد ملفات البيانات الرئيسية والتحقق من الصفوف ومراجعة النتائج.',
  },
  '/app/procurement/purchase-requests': {
    imageId: 'purchase-requests', code: 'PR',
    descriptionEn: 'Create and review internal purchase requests before orders are issued.',
    descriptionAr: 'إنشاء طلبات الشراء الداخلية ومراجعتها قبل إصدار الأوامر.',
  },
  '/app/procurement/supplier-quotations': {
    imageId: 'supplier-quotations', code: 'SQ',
    descriptionEn: 'Record supplier offers against requests and review their quoted terms.',
    descriptionAr: 'تسجيل عروض الموردين للطلبات ومراجعة الشروط والأسعار المقدمة.',
  },
  '/app/procurement/purchase-orders': {
    imageId: 'purchase-orders', code: 'PO',
    descriptionEn: 'Prepare purchase orders from eligible requests or supplier offers and manage their status.',
    descriptionAr: 'إعداد أوامر الشراء من الطلبات أو عروض الموردين ومتابعة حالاتها.',
  },
  '/app/procurement/goods-receipts': {
    imageId: 'goods-receipts', code: 'GR',
    descriptionEn: 'Record quantities received against confirmed purchase orders.',
    descriptionAr: 'تسجيل الكميات المستلمة مقابل أوامر الشراء المؤكدة.',
  },
  '/app/procurement/supplier-returns': {
    imageId: 'supplier-returns', code: 'SR',
    descriptionEn: 'Record supplier returns against their original received order lines.',
    descriptionAr: 'تسجيل مرتجعات الموردين وربطها ببنود الاستلام الأصلية.',
  },
  '/app/procurement/invoice-handoffs': {
    imageId: 'invoice-handoffs', code: 'IH',
    descriptionEn: 'Register supplier invoice evidence and hand it off to Finance for review.',
    descriptionAr: 'تسجيل مستندات فواتير الموردين وإحالتها إلى المالية للمراجعة.',
  },
  '/app/procurement/invoice-matching': {
    imageId: 'invoice-matching', code: 'IM',
    descriptionEn: 'Compare supplier invoices with purchase orders and receipts, then review matching results.',
    descriptionAr: 'مطابقة فواتير الموردين مع أوامر الشراء والاستلام ومراجعة النتائج.',
  },
  '/app/inventory': {
    imageId: 'inventory', code: 'INV',
    descriptionEn: 'Review stock availability, balances, and movements within the authorized scope.',
    descriptionAr: 'مراجعة توفر المخزون وأرصدته وحركاته ضمن النطاق المصرح به.',
  },
  '/app/inventory/valuation': {
    imageId: 'inventory-valuation', code: 'VAL',
    descriptionEn: 'Review inventory valuation summaries and their supporting details.',
    descriptionAr: 'مراجعة ملخصات تقييم المخزون والتفاصيل الداعمة لها.',
  },
  '/app/finance': {
    imageId: 'finance', code: 'FIN',
    descriptionEn: 'Manage Company books, journals, settlements, and finance period workflows.',
    descriptionAr: 'إدارة دفاتر الشركة والقيود والتسويات وسير عمل الفترات المالية.',
  },
  '/app/sales/quotations': {
    imageId: 'sales', code: 'SAL',
    descriptionEn: 'Manage customer quotations and sales orders in their current workflows.',
    descriptionAr: 'إدارة عروض أسعار العملاء وأوامر البيع ضمن مسارات العمل الحالية.',
  },
  '/app/reporting': {
    imageId: 'reporting', code: 'RPT',
    descriptionEn: 'Run available source-linked reports and review results within your authorized scope.',
    descriptionAr: 'تشغيل التقارير المتاحة المرتبطة بالمصادر ومراجعة نتائجها ضمن نطاق الصلاحيات.',
  },
};

interface OverviewModule {
  path: string;
  labelEn: string;
  labelAr: string;
  icon: string;
  code: string;
  descriptionEn: string;
  descriptionAr: string;
  imageSrc: string;
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
              <a class="module-card ui-surface" [class]="'module-card ui-surface module-card--' + module.groupId" [routerLink]="module.path" [style.--card-order]="index" [attr.aria-label]="label('View links for ' + module.labelEn, 'عرض روابط ' + module.labelAr)">
                <span class="module-card__banner" aria-hidden="true"><img class="module-card__image" [src]="module.imageSrc" width="640" height="360" loading="lazy" decoding="async" alt="" /><span class="module-card__code">{{ module.code }}</span></span>
                <span class="module-card__title">{{ navigationLabel(module) }}</span>
                <span class="module-card__description">{{ label(module.descriptionEn, module.descriptionAr) }}</span>
                <span class="module-card__view">{{ label('View links →', 'عرض الروابط ←') }}</span>
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
    .module-card { display: grid; min-width: 0; gap: .7rem; padding: .8rem; color: var(--ink); text-decoration: none; animation-delay: calc(var(--card-order, 0) * 35ms); }
    .module-card:focus-visible { outline: 3px solid var(--focus); outline-offset: 3px; }
    .module-card__banner { position: relative; display: block; width: 100%; aspect-ratio: 16 / 9; overflow: hidden; border-radius: 12px; background-color: var(--surface-raised); background-image: radial-gradient(circle at 50% 50%, transparent 0 35%, color-mix(in srgb, var(--accent) 12%, transparent) 35.5% 36%, transparent 36.5% 52%, color-mix(in srgb, var(--accent) 8%, transparent) 52.5% 53%, transparent 53.5%), linear-gradient(135deg, color-mix(in srgb, var(--accent-soft) 80%, var(--surface-raised)), var(--surface-raised)); }
    .module-card__image { position: relative; z-index: 1; display: block; width: 100%; height: 100%; border-radius: inherit; padding: .35rem; object-fit: contain; }
    .module-card__code { position: absolute; z-index: 2; top: .6rem; right: .6rem; border: 1px solid color-mix(in srgb, var(--accent) 32%, var(--line)); border-radius: 999px; padding: .3rem .55rem; color: var(--accent-strong); background: color-mix(in srgb, var(--surface-raised) 92%, transparent); font: 700 .72rem/1 var(--font-sans); letter-spacing: .08em; direction: ltr; }
    @media (prefers-reduced-motion: no-preference) { .module-card__image { transition: transform var(--motion-fast) ease; } .module-card:hover .module-card__image { transform: scale(1.03); } }
    .module-card__title { color: var(--ink-strong); font: 500 1.05rem/1.25 var(--font-display); }
    .module-card__description { display: -webkit-box; min-height: 2.8em; overflow: hidden; color: var(--ink-muted); font-size: 14px; line-height: 1.4; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
    .module-card__view { display: inline-flex; min-height: 40px; align-items: center; justify-content: center; justify-self: start; border: 1px solid var(--line-strong); border-radius: 999px; padding: .35rem .8rem; color: var(--ink); background: var(--surface-raised); font-size: 14px; font-weight: 600; }
    .module-empty { grid-column: 1 / -1; margin: 0; border: 1px dashed var(--line-strong); border-radius: 14px; padding: 1.5rem; color: var(--ink-muted); text-align: center; }
    @media (max-width: 1100px) { .overview-hero { min-height: 0; grid-template-columns: minmax(0, 1fr); } .overview-hero__content { grid-column: 1; grid-row: 1; } .overview-hero__art { grid-column: 1; grid-row: 2; justify-self: center; } .overview-hero__tenant-logo { grid-column: 1; grid-row: 2; justify-self: center; } }
    @media (max-width: 760px) { .overview-hero { padding: 1.25rem; } .overview-hero__tenant-logo img { max-height: 130px; } .hero-mark { width: 58px; height: 58px; border-radius: 19px; font-size: 1.65rem; } .overview-section-heading__hint { display: none; } }
    @media (max-width: 500px) { .overview-hero__tenant-logo { max-width: 220px; } .overview-hero__tenant-logo img { max-height: 96px; } .module-chips { margin-inline: -.2rem; } .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .65rem; } .module-card { padding: .65rem; } .module-card__title, .module-card__view { font-size: 14px; } .module-card__code { top: .4rem; right: .4rem; padding: .25rem .45rem; } }
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
  readonly moduleDestinations: OverviewModule[] = NAVIGATION_GROUPS.flatMap((group) => group.items.map((item) => {
    const card = MODULE_CARD_CONTENT[item.path];
    return {
      ...item,
      code: card.code,
      descriptionEn: card.descriptionEn,
      descriptionAr: card.descriptionAr,
      imageSrc: '/images/modules/' + card.imageId + '.webp',
      groupId: group.id,
      groupLabelEn: group.labelEn,
      groupLabelAr: group.labelAr,
    };
  }));
  readonly visibleModules = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();
    const group = this.selectedGroup();
    return this.moduleDestinations.filter((module) =>
      (group === 'all' || module.groupId === group)
      && (!query || `${module.code} ${module.labelEn} ${module.labelAr} ${module.descriptionEn} ${module.descriptionAr} ${module.groupLabelEn} ${module.groupLabelAr} ${module.path}`.toLocaleLowerCase().includes(query)),
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
