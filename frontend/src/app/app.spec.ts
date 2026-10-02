import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App foundation navigation', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('creates the root router host', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('keeps only the approved entry routes and removes access-context selection', () => {
    expect(routes.map((route) => route.path)).toEqual(['', 'login', 'app', '**']);
    const appRoute = routes.find((route) => route.path === 'app');
    expect(appRoute?.children?.some((route) => route.path === 'workspaces' || route.path === 'access-contexts')).toBe(false);
  });
});
