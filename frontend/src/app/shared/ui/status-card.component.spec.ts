import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatusCardComponent, UiState } from './status-card.component';

describe('StatusCardComponent', () => {
  let fixture: ComponentFixture<StatusCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StatusCardComponent] }).compileComponents();
  });

  it.each([
    ['loading', 'Loading'],
    ['empty', 'Empty'],
    ['denied', 'This action is not available for your current access.'],
    ['failed', 'Error'],
    ['unavailable', 'Unavailable'],
    ['unknown', 'Unknown state'],
    ['pending', 'Pending'],
  ] as [UiState, string][])('renders an accessible %s state', (state, label) => {
    fixture = TestBed.createComponent(StatusCardComponent);
    fixture.componentRef.setInput('title', 'State title');
    fixture.componentRef.setInput('message', 'State message');
    fixture.componentRef.setInput('state', state);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const section = element.querySelector('section')!;
    expect(section.getAttribute('role')).toBe('status');
    expect(section.getAttribute('aria-live')).toBe('polite');
    expect(element.querySelector('.status-card__state')?.textContent?.trim()).toBe(label);
    expect(section.getAttribute('aria-busy')).toBe(state === 'loading' ? 'true' : null);
  });
});
