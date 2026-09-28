import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.removeItem('mesp.ui.theme');
    localStorage.removeItem('mesp.ui.dark');
    TestBed.configureTestingModule({ providers: [ThemeService] });
  });

  it('uses Sapphire light mode by default and applies the independent color scheme', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
    expect(service.darkMode()).toBe(false);
    expect(document.documentElement.dataset['theme']).toBe('sapphire');
    expect(document.documentElement.dataset['colorScheme']).toBe('light');
  });

  it('persists the selected theme and dark mode separately across instances', () => {
    const service = TestBed.inject(ThemeService);
    service.select('forest');
    service.toggleDarkMode();
    expect(localStorage.getItem('mesp.ui.theme')).toBe('forest');
    expect(localStorage.getItem('mesp.ui.dark')).toBe('true');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [ThemeService] });
    const restored = TestBed.inject(ThemeService);
    expect(restored.selectedTheme()).toBe('forest');
    expect(restored.darkMode()).toBe(true);
    expect(document.documentElement.dataset['theme']).toBe('forest');
    expect(document.documentElement.dataset['colorScheme']).toBe('dark');
  });

  it('falls back to Sapphire when stored theme data is invalid', () => {
    localStorage.setItem('mesp.ui.theme', 'tenant-specific-name');
    const service = TestBed.inject(ThemeService);
    expect(service.selectedTheme()).toBe('sapphire');
  });
});
