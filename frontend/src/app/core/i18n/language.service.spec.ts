import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.removeItem('mesp.ui.language');
    TestBed.configureTestingModule({ providers: [LanguageService] });
  });

  it('changes the document direction with the selected language', () => {
    const service = TestBed.inject(LanguageService);

    service.setLanguage('ar');
    expect(service.language()).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(service.text('signIn')).toBe('تسجيل الدخول');

    service.setLanguage('en');
    expect(document.documentElement.lang).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
    expect(service.text('signIn')).toBe('Sign in');
  });

  it('persists Arabic and restores RTL on the next service instance', () => {
    TestBed.inject(LanguageService).setLanguage('ar');
    expect(localStorage.getItem('mesp.ui.language')).toBe('ar');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [LanguageService] });
    const restored = TestBed.inject(LanguageService);
    expect(restored.language()).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    localStorage.removeItem('mesp.ui.language');
  });
});
