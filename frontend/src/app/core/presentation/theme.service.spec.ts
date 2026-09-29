import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ContextService } from '../context/context.service';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  const entry = signal<{ branding: { defaultTheme?: string | null } } | null>(null);

  beforeEach(() => {
    localStorage.removeItem('mesp.ui.theme');
    localStorage.removeItem('mesp.ui.dark');
    entry.set(null);
    TestBed.configureTestingModule({
      providers: [ThemeService, { provide: ContextService, useValue: { entry } }],
    });
  });

  it('uses Sapphire light mode by default and applies the independent color scheme', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
    expect(service.darkMode()).toBe(false);
    expect(document.documentElement.dataset['theme']).toBe('sapphire');
    expect(document.documentElement.dataset['colorScheme']).toBe('light');
  });

  it('uses the Tenant default when no user choice exists', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
    entry.set({ branding: { defaultTheme: 'meadow' } });
    TestBed.flushEffects();
    expect(service.selectedTheme()).toBe('meadow');
    expect(document.documentElement.dataset['theme']).toBe('meadow');
  });

  it('persists a user choice over the Tenant default across instances', () => {
    entry.set({ branding: { defaultTheme: 'meadow' } });
    const service = TestBed.inject(ThemeService);
    service.select('teal');
    service.toggleDarkMode();
    expect(localStorage.getItem('mesp.ui.theme')).toBe('teal');
    expect(localStorage.getItem('mesp.ui.dark')).toBe('true');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [ThemeService, { provide: ContextService, useValue: { entry } }],
    });
    const restored = TestBed.inject(ThemeService);
    expect(restored.selectedTheme()).toBe('teal');
    expect(restored.darkMode()).toBe(true);
    expect(document.documentElement.dataset['theme']).toBe('teal');
    expect(document.documentElement.dataset['colorScheme']).toBe('dark');
  });

  it('falls back to Sapphire when stored theme data is invalid', () => {
    localStorage.setItem('mesp.ui.theme', 'tenant-specific-name');
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
  });

  it('falls back to Sapphire when the configured Tenant theme is invalid', () => {
    entry.set({ branding: { defaultTheme: 'unknown-theme' } });
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
  });
});
