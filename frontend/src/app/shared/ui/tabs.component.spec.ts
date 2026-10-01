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
  template: '<app-tabs [tabs]="tabs" [selected]="selected" ariaLabel="Views" (selectedChange)="selected = $event" /><router-outlet />',
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

  it('renders router-link tabs and selects the active route', async () => {
    host.tabs = [
      { id: 'home', label: 'Overview', routerLink: '/finance' },
      { id: 'ap', label: 'Payables', routerLink: '/finance/ap' },
    ];
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/finance/ap');
    await fixture.whenStable();
    fixture.detectChanges();
    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]') as NodeListOf<HTMLAnchorElement>;
    expect(tabs[1].getAttribute('href')).toBe('/finance/ap');
    expect(tabs[1].getAttribute('aria-selected')).toBe('true');
    expect(tabs[0].getAttribute('aria-selected')).toBe('false');
  });
});
