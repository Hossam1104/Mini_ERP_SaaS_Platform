import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export interface AppTab {
  id: string;
  label: string;
  routerLink?: string | readonly unknown[];
  exact?: boolean;
  tabId?: string;
  panelId?: string;
}

@Component({
  selector: 'app-tabs',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav #tablist class="app-tabs__list" role="tablist" aria-orientation="horizontal" [attr.aria-label]="ariaLabel" (keydown)="onKeydown($event)">
      @for (tab of tabs; track tab.id) {
        @if (tab.routerLink !== undefined) {
          <a #tabControl [attr.id]="tab.tabId ?? null" [routerLink]="tab.routerLink" routerLinkActive="is-active" ariaCurrentWhenActive="page" #active="routerLinkActive" [routerLinkActiveOptions]="{ exact: tab.exact ?? true }" role="tab" [class.is-active]="active.isActive" [attr.aria-selected]="active.isActive" [attr.aria-controls]="tab.panelId ?? null" [attr.tabindex]="active.isActive ? 0 : -1">{{ tab.label }}</a>
        } @else {
          <button #tabControl [attr.id]="tab.tabId ?? null" type="button" role="tab" [class.is-active]="selected === tab.id" [attr.aria-selected]="selected === tab.id" [attr.aria-controls]="tab.panelId ?? null" [attr.tabindex]="selected === tab.id ? 0 : -1" (click)="selectedChange.emit(tab.id)">{{ tab.label }}</button>
        }
      }
    </nav>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .app-tabs__list { display: flex; min-width: 0; align-items: center; gap: var(--tabs-gap); overflow-x: auto; padding: var(--tabs-padding); border: 1px solid var(--tabs-border); border-radius: var(--tabs-radius); background: var(--tabs-background); backdrop-filter: blur(16px); scrollbar-width: none; scroll-snap-type: x proximity; }
    .app-tabs__list::-webkit-scrollbar { display: none; }
    .app-tabs__list > :is(a, button) { position: relative; display: inline-flex; min-height: var(--tabs-height); flex: 0 0 auto; align-items: center; justify-content: center; gap: var(--button-gap); scroll-snap-align: start; border: 0; border-radius: var(--tabs-item-radius); padding: 0 var(--tabs-item-padding); color: var(--tabs-ink); background: transparent; font: var(--tabs-font); text-decoration: none; white-space: nowrap; cursor: pointer; }
    .app-tabs__list > :is(a, button)::after { position: absolute; inset-inline: var(--tabs-item-padding); inset-block-end: var(--tabs-indicator-bottom); height: var(--tabs-indicator-height); border-radius: var(--tabs-indicator-height); background: var(--tabs-active-ink); content: ''; transform: scaleX(0); transform-origin: left; transition: transform var(--motion-fast) ease; }
    :host-context([dir=rtl]) .app-tabs__list > :is(a, button)::after { transform-origin: right; }
    .app-tabs__list > :is(a, button).is-active { color: var(--tabs-active-ink); background: var(--tabs-active-background); box-shadow: var(--tabs-active-shadow); font-weight: var(--tabs-active-weight); }
    .app-tabs__list > :is(a, button).is-active::after { transform: scaleX(1); }
    .app-tabs__list > :is(a, button):focus-visible { outline: 3px solid var(--focus); outline-offset: 2px; }
  `],
})
export class TabsComponent implements AfterViewChecked {
  @Input() tabs: readonly AppTab[] = [];
  @Input() selected = '';
  @Input() ariaLabel = '';
  @Output() readonly selectedChange = new EventEmitter<string>();
  @ViewChild('tablist', { static: true }) private tablist!: ElementRef<HTMLElement>;
  private activeTab: HTMLElement | null = null;

  ngAfterViewChecked(): void {
    const active = this.tablist.nativeElement.querySelector<HTMLElement>('[aria-selected="true"]');
    if (active && active !== this.activeTab) {
      this.activeTab = active;
      active.scrollIntoView?.({ block: 'nearest', inline: 'nearest' });
    }
  }

  onKeydown(event: KeyboardEvent): void {
    const controls = [...this.tablist.nativeElement.querySelectorAll<HTMLElement>('[role="tab"]')];
    const focused = event.target instanceof HTMLElement && controls.includes(event.target)
      ? event.target
      : controls.find((control) => control.getAttribute('aria-selected') === 'true');
    const current = focused ? controls.indexOf(focused) : -1;
    if (current < 0 || controls.length < 2) return;
    const rtl = this.tablist.nativeElement.closest('[dir="rtl"]') !== null || document.documentElement.dir === 'rtl';
    const direction = event.key === 'ArrowRight' ? (rtl ? -1 : 1) : event.key === 'ArrowLeft' ? (rtl ? 1 : -1) : 0;
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? controls.length - 1 : (current + direction + controls.length) % controls.length;
    if (!direction && event.key !== 'Home' && event.key !== 'End') return;
    event.preventDefault();
    controls[next].focus();
    controls[next].click();
  }
}
