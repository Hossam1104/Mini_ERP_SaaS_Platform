import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/presentation/theme.service';
import { WorkspaceHomeComponent } from './workspace-home.component';

describe('WorkspaceHomeComponent', () => {
  const tenantEntry = (logoLightUrl: string | null) => ({
    entryMode: 'Tenant',
    candidateTenantDisplayName: 'Example Tenant',
    branding: {
      displayName: 'Example Tenant',
      logoLightUrl,
      logoDarkUrl: null,
      logoAltText: 'Example Tenant',
    },
  });
  const entry = signal(tenantEntry('/assets/example-tenant-logo.png'));

  beforeEach(async () => {
    entry.set(tenantEntry('/assets/example-tenant-logo.png'));
    await TestBed.configureTestingModule({
      imports: [WorkspaceHomeComponent],
      providers: [
        provideRouter([]),
        {
          provide: ContextService,
          useValue: {
            entry,
            selectedOperationalContextId: signal<string | null>(null),
            operationalContexts: signal([]),
          },
        },
        { provide: LanguageService, useValue: { language: signal('en'), text: (key: string) => key } },
        { provide: ThemeService, useValue: { darkMode: signal(true) } },
      ],
    }).compileComponents();
  });

  it('shows the light Tenant logo in dark mode and hides it when no logo is configured', () => {
    const fixture: ComponentFixture<WorkspaceHomeComponent> = TestBed.createComponent(WorkspaceHomeComponent);
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('.overview-hero__tenant-logo img') as HTMLImageElement;
    expect(image).not.toBeNull();
    expect(image?.getAttribute('src')).toBe('/assets/example-tenant-logo.png');
    expect(image?.alt).toBe('Example Tenant');

    entry.set(tenantEntry(null));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.overview-hero__tenant-logo')).toBeNull();
    expect(fixture.nativeElement.querySelector('.hero-mark')?.textContent.trim()).toBe('M');
  });

  it('shows a decorative local image on every module card', () => {
    const fixture: ComponentFixture<WorkspaceHomeComponent> = TestBed.createComponent(WorkspaceHomeComponent);
    fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('.module-card') as NodeListOf<HTMLElement>;
    expect(cards.length).toBe(fixture.componentInstance.moduleDestinations.length);

    for (const card of Array.from(cards)) {
      const image = card.querySelector('img');
      expect(image?.getAttribute('src')).toMatch(/^\/images\/modules\/[a-z0-9-]+\.webp$/);
      expect(image?.getAttribute('alt')).toBe('');
    }
  });

  it('matches the card code in Overview search without widening name searches', () => {
    const fixture: ComponentFixture<WorkspaceHomeComponent> = TestBed.createComponent(WorkspaceHomeComponent);
    const home = fixture.componentInstance;
    home.searchQuery.set('Purchase Orders');
    expect(home.visibleModules().map((module) => module.code)).toEqual(['PO']);
    home.searchQuery.set('sq');
    expect(home.visibleModules().map((module) => module.code)).toContain('SQ');
  });
});
