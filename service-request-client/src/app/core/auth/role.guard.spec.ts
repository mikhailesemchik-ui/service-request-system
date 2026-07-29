import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH, LoginResponse } from './auth.models';
import { AuthService } from './auth.service';
import { roleGuard } from './role.guard';

type GuardDecision = Observable<boolean | UrlTree>;

const loginUrl = `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;

function buildLoginResponse(role: 'Employee' | 'Admin'): LoginResponse {
  return {
    accessToken: 'jwt-token',
    tokenType: 'Bearer',
    expiresAt: '2026-01-01T00:00:00Z',
    user: { id: 1, username: 'jane.doe', displayName: 'Jane Doe', email: 'jane.doe@example.test', role },
  };
}

describe('roleGuard', () => {
  let authService: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    authService = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function runGuard(roles: unknown, url = '/admin'): GuardDecision {
    const route = { data: { roles } } as unknown as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => roleGuard(route, state)) as GuardDecision;
  }

  function completeInitialization(): void {
    authService.restoreSession().subscribe();
  }

  function loginAs(role: 'Employee' | 'Admin'): void {
    authService.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush(buildLoginResponse(role));
  }

  it('waits for initialization before deciding', () => {
    let resolved = false;

    runGuard(['Admin']).subscribe(() => (resolved = true));

    expect(resolved).toBeFalse();

    completeInitialization();

    expect(resolved).toBeTrue();
  });

  it('allows a user whose role matches the required roles', () => {
    completeInitialization();
    loginAs('Admin');

    let result: boolean | UrlTree | undefined;
    runGuard(['Admin', 'SupportAgent']).subscribe((value) => (result = value));

    expect(result).toBeTrue();
  });

  it('denies a user whose role does not match, without logging them out', () => {
    completeInitialization();
    loginAs('Employee');

    let result: boolean | UrlTree | undefined;
    runGuard(['Admin']).subscribe((value) => (result = value));

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
    expect(authService.isAuthenticated()).toBeTrue();
  });

  it('redirects an anonymous user to /login', () => {
    completeInitialization();

    let result: boolean | UrlTree | undefined;
    runGuard(['Admin']).subscribe((value) => (result = value));

    expect(router.serializeUrl(result as UrlTree)).toContain('/login');
  });

  it('safely denies access when route data.roles is missing', () => {
    completeInitialization();
    loginAs('Admin');

    let result: boolean | UrlTree | undefined;
    runGuard(undefined).subscribe((value) => (result = value));

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
  });

  it('safely denies access when route data.roles is malformed', () => {
    completeInitialization();
    loginAs('Admin');

    let result: boolean | UrlTree | undefined;
    runGuard('Admin').subscribe((value) => (result = value));

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
  });
});
