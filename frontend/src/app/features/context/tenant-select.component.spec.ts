import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '../../core/api/auth.interceptor';
import { FoundationContextCandidate, FoundationSessionResponse } from '../../core/api/foundation.models';
import { AuthService } from '../../core/auth/auth.service';
import { ContextService } from '../../core/context/context.service';
import { LanguageService } from '../../core/i18n/language.service';
import { TenantSelectComponent } from './tenant-select.component';

const session: FoundationSessionResponse = {
  authenticated: true,
  actorId: 'actor-1',
  sessionId: 'session-1',
  lifecycleState: 'Active',
  absoluteExpiresAt: null,
  selectedPath: null,
  selectedTenantId: null,
  selectedContextId: null,
  selectionVersion: 0,
  displayName: 'Amina Hassan',
  login: 'amina@example.com',
};

const wafraContext: FoundationContextCandidate = {
  contextId: 'context-1',
  kind: 'OrdinaryMembership',
  tenantId: '11111111-1111-1111-1111-111111111111',
  displayName: 'Wafra',
  eligibilityVersion: 1,
};

describe('TenantSelectComponent', () => {
  let fixture: ComponentFixture<TenantSelectComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantSelectComponent],
      providers: [
        AuthService,
        ContextService,
        LanguageService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(AuthService).acceptServerSession(session);
    TestBed.inject(ContextService).contexts.set([wafraContext]);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TenantSelectComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('renders the server-provided human Tenant name and one central selector', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Wafra');
    expect(element.textContent).not.toContain(wafraContext.tenantId);
    expect(element.querySelectorAll('app-context-switcher')).toHaveLength(1);
    expect(element.querySelector('.ui-technical-reference')).not.toBeNull();
  });
});
