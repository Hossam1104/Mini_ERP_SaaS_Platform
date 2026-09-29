import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';
import { ContextService } from '../context/context.service';

export const THEME_OPTIONS = [
  { id: 'sapphire', label: 'Sapphire', color: '#1d4ed8' },
  { id: 'luxury', label: 'Luxury', color: '#111827' },
  { id: 'forest', label: 'Forest', color: '#1e8449' },
  { id: 'meadow', label: 'Meadow', color: '#5da234' },
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
  private readonly context = inject(ContextService);
  private readonly document = inject(DOCUMENT);
  readonly options = THEME_OPTIONS;
  private userChoice = this.readStoredTheme();
  readonly selectedTheme = signal<ThemeName>(
    this.userChoice ?? this.resolveTenantTheme(this.context.entry()?.branding.defaultTheme),
  );
  readonly darkMode = signal(this.readDarkMode());

  constructor() {
    this.apply();
    effect(() => {
      const defaultTheme = this.context.entry()?.branding.defaultTheme;
      if (this.userChoice === null) {
        this.selectedTheme.set(this.resolveTenantTheme(defaultTheme));
        this.apply();
      }
    });
  }

  select(theme: ThemeName): void {
    if (!this.options.some((option) => option.id === theme)) return;
    this.userChoice = theme;
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

  private readStoredTheme(): ThemeName | null {
    const saved = this.read(THEME_STORAGE_KEY);
    return this.options.find((option) => option.id === saved)?.id ?? null;
  }

  private resolveTenantTheme(theme: string | null | undefined): ThemeName {
    return this.options.find((option) => option.id === theme)?.id ?? 'sapphire';
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
