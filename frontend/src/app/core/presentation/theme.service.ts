import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export const THEME_OPTIONS = [
  { id: 'sapphire', label: 'Sapphire', color: '#1d4ed8' },
  { id: 'luxury', label: 'Luxury', color: '#111827' },
  { id: 'forest', label: 'Forest', color: '#1e8449' },
  { id: 'ruby', label: 'Ruby', color: '#e60000' },
  { id: 'purple', label: 'Purple', color: '#7b2fbe' },
  { id: 'amber', label: 'Amber', color: '#e85d04' },
  { id: 'teal', label: 'Teal', color: '#0f766e' },
  { id: 'noir', label: 'Noir', color: '#303640' },
] as const;

export type ThemeName = (typeof THEME_OPTIONS)[number]['id'];

const THEME_STORAGE_KEY = 'mesp.ui.theme';
const DARK_STORAGE_KEY = 'mesp.ui.dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  readonly options = THEME_OPTIONS;
  readonly selectedTheme = signal<ThemeName>(this.readTheme());
  readonly darkMode = signal(this.readDarkMode());

  constructor() {
    this.apply();
  }

  select(theme: ThemeName): void {
    if (!this.options.some((option) => option.id === theme)) return;
    this.selectedTheme.set(theme);
    this.persist(THEME_STORAGE_KEY, theme);
    this.apply();
  }

  toggleDarkMode(): void {
    const dark = !this.darkMode();
    this.darkMode.set(dark);
    this.persist(DARK_STORAGE_KEY, dark ? 'true' : 'false');
    this.apply();
  }

  private readTheme(): ThemeName {
    const saved = this.read(THEME_STORAGE_KEY);
    return this.options.find((option) => option.id === saved)?.id ?? 'sapphire';
  }

  private readDarkMode(): boolean {
    return this.read(DARK_STORAGE_KEY) === 'true';
  }

  private read(key: string): string | null {
    try {
      return this.document.defaultView?.localStorage.getItem(key) ?? null;
    } catch {
      return null;
    }
  }

  private persist(key: string, value: string): void {
    try {
      this.document.defaultView?.localStorage.setItem(key, value);
    } catch {
      // The current page still applies its selection when browser storage is unavailable.
    }
  }

  private apply(): void {
    const root = this.document.documentElement;
    root.dataset['theme'] = this.selectedTheme();
    root.dataset['colorScheme'] = this.darkMode() ? 'dark' : 'light';
  }
}
