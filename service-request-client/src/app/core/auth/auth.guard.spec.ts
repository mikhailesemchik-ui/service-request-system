import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH, LoginResponse } from './auth.models';
import { authGuard, guestGuard } from './auth.guard';
import { AuthService } from './auth.service';

type Guard = typeof authGuard;
type GuardDecision = Observable<boolean | UrlTree>;

const loginUrl = `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;
const loginResponse: LoginResponse = {
  accessToken: 'jwt-token',
  tokenType: 'Bearer',
  expiresAt: '2026-01-01T00:00:00Z',
  user: { id: 1, username: 'jane.doe', displayName: 'Jane Doe', email: 'jane.doe@example.test', role: 'Employee' },
};

describe('authGuard / guestGuard', () => {
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

  function runGuard(guard: Guard, url = '/dashboard'): GuardDecision {
    const route = { data: {} } as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => guard(route, state)) as GuardDecision;
  }

  function completeInitialization(): void {
    authService.restoreSession().subscribe();
  }

  function loginAsEmployee(): void {
    authService.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush(loginResponse);
  }

  describe('authGuard', () => {
    it('waits for initialization before deciding', () => {
      let resolved = false;

      runGuard(authGuard).subscribe(() => (resolved = true));

      expect(resolved).toBeFalse();

      completeInitialization();

      expect(resolved).toBeTrue();
    });

    it('allows an authenticated user through', () => {
      completeInitialization();
      loginAsEmployee();

      let result: boolean | UrlTree | undefined;
      runGuard(authGuard).subscribe((value) => (result = value));

      expect(result).toBeTrue();
    });

    it('redirects an anonymous user to /login and preserves the requested URL', () => {
      completeInitialization();

      let result: boolean | UrlTree | undefined;
      runGuard(authGuard, '/categories').subscribe((value) => (result = value));

      const serialized = router.serializeUrl(result as UrlTree);
      expect(serialized).toContain('/login');
      expect((result as UrlTree).queryParams['returnUrl']).toBe('/categories');
    });
  });

  describe('guestGuard', () => {
    it('allows an anonymous user to view the login page', () => {
      completeInitialization();

      let result: boolean | UrlTree | undefined;
      runGuard(guestGuard, '/login').subscribe((value) => (result = value));

      expect(result).toBeTrue();
    });

    it('redirects an already-authenticated user away from the login page', () => {
      completeInitialization();
      loginAsEmployee();

      let result: boolean | UrlTree | undefined;
      runGuard(guestGuard, '/login').subscribe((value) => (result = value));

      const serialized = router.serializeUrl(result as UrlTree);
      expect(serialized).toBe('/dashboard');
    });
  });
});
