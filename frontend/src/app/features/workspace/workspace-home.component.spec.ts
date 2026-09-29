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
});
