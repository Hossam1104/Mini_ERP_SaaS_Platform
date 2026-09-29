import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ContextService } from '../../core/context/context.service';
import { CurrencyAmountComponent } from './currency-amount.component';

describe('CurrencyAmountComponent', () => {
  let fixture: ComponentFixture<CurrencyAmountComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CurrencyAmountComponent],
      providers: [
        {
          provide: ContextService,
          useValue: {
            entry: signal({
              currencyPresentation: {
                currencyCode: 'SAR',
                symbolAssetUrl: '/assets/Saudi_Riyal.svg',
                symbolTextFallback: 'SAR',
              },
            }),
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CurrencyAmountComponent);
    fixture.componentRef.setInput('amount', 125.5);
    fixture.componentRef.setInput('currencyCode', 'SAR');
    fixture.componentRef.setInput('locale', 'en-US');
    fixture.detectChanges();
  });

  it('renders the configured SAR asset with an accessible name', () => {
    const symbol = (fixture.nativeElement as HTMLElement).querySelector('[role=\"img\"]');
    expect(symbol?.getAttribute('aria-label')).toBe('SAR');
    expect(symbol?.getAttribute('style')).toContain('Saudi_Riyal.svg');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('125.50');
  });

  it('keeps non-SAR currencies as text', () => {
    fixture.componentRef.setInput('currencyCode', 'USD');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role=\"img\"]')).toBeNull();
    expect(element.textContent).toContain('USD');
    expect(element.textContent).toContain('125.50');
  });
});
