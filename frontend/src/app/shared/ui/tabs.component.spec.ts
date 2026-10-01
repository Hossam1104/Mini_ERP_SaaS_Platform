import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, RouterOutlet, provideRouter } from '@angular/router';
import { AppTab, TabsComponent } from './tabs.component';

@Component({ standalone: true, selector: 'test-finance-home', template: 'Finance overview' })
class FinanceHomeComponent {}

@Component({ standalone: true, selector: 'test-finance-payables', template: 'Finance payables' })
class FinancePayablesComponent {}

@Component({
  standalone: true,
  imports: [TabsComponent, RouterOutlet],
  template: '<app-tabs #viewTabs [tabs]="tabs" [selected]="selected" ariaLabel="Views" (selectedChange)="selected = $event" /><section role="tabpanel" [id]="viewTabs.panelId(selected)" [attr.aria-labelledby]="viewTabs.tabId(selected)" tabindex="0">{{ selected }} view</section><router-outlet />',
})
class TabsHostComponent {
  selected = 'first';
  tabs: readonly AppTab[] = [
    { id: 'first', label: 'First' },
    { id: 'second', label: 'Second' },
    { id: 'third', label: 'Third' },
  ];
}

describe('TabsComponent', () => {
  let fixture: ComponentFixture<TabsHostComponent>;
  let host: TabsHostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TabsHostComponent],
      providers: [provideRouter([
        { path: 'finance', component: FinanceHomeComponent },
        { path: 'finance/ap', component: FinancePayablesComponent },
      ])],
    }).compileComponents();
    fixture = TestBed.createComponent(TabsHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('selects an in-page tab and keeps aria-selected in sync', () => {
    const second = fixture.nativeElement.querySelectorAll('[role="tab"]')[1] as HTMLButtonElement;
    second.click();
    fixture.detectChanges();
    expect(host.selected).toBe('second');
    expect(second.getAttribute('aria-selected')).toBe('true');
    expect(second.getAttribute('aria-controls')).toBe(fixture.nativeElement.querySelector('[role="tabpanel"]')?.id);
    expect(fixture.nativeElement.querySelector('[role="tabpanel"]')?.getAttribute('aria-labelledby')).toBe(second.id);
    expect(fixture.nativeElement.querySelector('[aria-selected="false"]')?.textContent.trim()).toBe('First');
  });

  it('moves focus and selection with LTR and RTL arrow keys', () => {
    const [first, second, third] = fixture.nativeElement.querySelectorAll('[role="tab"]') as NodeListOf<HTMLButtonElement>;
    first.focus();
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(second);
    expect(host.selected).toBe('second');

    document.documentElement.dir = 'rtl';
    second.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(document.activeElement).toBe(first);
    expect(host.selected).toBe('first');
    document.documentElement.dir = 'ltr';
    expect(third.getAttribute('role')).toBe('tab');
  });

  it('scrolls only the tab list horizontally when the active tab changes', () => {
    const list = fixture.nativeElement.querySelector('[role="tablist"]') as HTMLElement;
    const third = fixture.nativeElement.querySelectorAll('[role="tab"]')[2] as HTMLElement;
    Object.defineProperty(list, 'getBoundingClientRect', { configurable: true, value: () => new DOMRect(0, 0, 100, 40) });
    Object.defineProperty(third, 'getBoundingClientRect', { configurable: true, value: () => new DOMRect(110, 0, 35, 40) });
    let scrollIntoViewCalls = 0;
    Object.defineProperty(third, 'scrollIntoView', { configurable: true, value: () => scrollIntoViewCalls++ });
    document.documentElement.scrollTop = 17;
    document.body.scrollTop = 23;

    third.click();
    fixture.detectChanges();

    expect(list.scrollLeft).toBe(45);
    expect(document.documentElement.scrollTop).toBe(17);
    expect(document.body.scrollTop).toBe(23);
    expect(scrollIntoViewCalls).toBe(0);

    const first = fixture.nativeElement.querySelector('[role="tab"]') as HTMLElement;
    list.dir = 'rtl';
    list.scrollLeft = -10;
    Object.defineProperty(first, 'getBoundingClientRect', { configurable: true, value: () => new DOMRect(-15, 0, 30, 40) });
    first.click();
    fixture.detectChanges();

    expect(list.scrollLeft).toBe(-25);
    expect(document.documentElement.scrollTop).toBe(17);
    expect(document.body.scrollTop).toBe(23);
  });

  it('renders router-backed entries as normal navigation links with the active page marked', async () => {
    host.tabs = [
      { id: 'home', label: 'Overview', routerLink: '/finance' },
      { id: 'ap', label: 'Payables', routerLink: '/finance/ap' },
    ];
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/finance/ap');
    await fixture.whenStable();
    fixture.detectChanges();
    const links = fixture.nativeElement.querySelectorAll('nav[aria-label="Views"] a') as NodeListOf<HTMLAnchorElement>;
    expect(links).toHaveLength(2);
    expect(links[1].getAttribute('href')).toBe('/finance/ap');
    expect(links[1].getAttribute('aria-current')).toBe('page');
    expect(links[0].hasAttribute('aria-current')).toBe(false);
    expect([...links].every(link => link.getAttribute('role') !== 'tab' && link.tabIndex === 0 && !link.hasAttribute('tabindex'))).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="tab"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="tablist"]')).toBeNull();
  });
});
