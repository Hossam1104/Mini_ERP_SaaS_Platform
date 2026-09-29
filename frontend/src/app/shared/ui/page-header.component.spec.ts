import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderComponent } from './page-header.component';

@Component({ standalone: true, imports: [PageHeaderComponent, RouterLink], template: "<app-page-header [pageLabelOverride]=\"'Purchase Orders'\"><div page-header-copy><p class='eyebrow'>Procurement</p><h1>Purchase Orders</h1><p class='lede'>Review orders</p></div><div page-header-actions><a data-page-action routerLink='/app'>Refresh</a></div></app-page-header>" })
class PageHeaderHostComponent {}

describe('PageHeaderComponent', () => {
  let fixture: ComponentFixture<PageHeaderHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PageHeaderHostComponent],
      providers: [LanguageService, provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(PageHeaderHostComponent);
    fixture.detectChanges();
  });

  it('keeps the page title, subtitle and actions with an accessible route breadcrumb', () => {
    const header = fixture.nativeElement.querySelector('app-page-header header') as HTMLElement;
    expect(header.querySelector('nav[aria-label=\"Breadcrumb\"] a[href=\"/app\"]')?.textContent?.trim()).toBe('Overview');
    expect(header.querySelector('[aria-current=\"page\"]')?.textContent?.trim()).toBe('Purchase Orders');
    expect(header.querySelector('h1')?.textContent?.trim()).toBe('Purchase Orders');
    expect(header.querySelector('.lede')?.textContent?.trim()).toBe('Review orders');
    expect(header.querySelector('.page-header__actions [data-page-action]')?.textContent?.trim()).toBe('Refresh');
  });
});
