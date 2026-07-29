import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH, AUTH_ME_PATH, AuthenticatedUser, LoginResponse } from './auth.models';
import { AuthStorageService } from './auth-storage.service';
import { AuthService } from './auth.service';

const testUser: AuthenticatedUser = {
  id: 1,
  username: 'jane.doe',
  displayName: 'Jane Doe',
  email: 'jane.doe@example.test',
  role: 'Employee',
};

const loginResponse: LoginResponse = {
  accessToken: 'jwt-token',
  tokenType: 'Bearer',
  expiresAt: '2026-01-01T00:00:00Z',
  user: testUser,
};

const loginUrl = `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;
const meUrl = `${environment.apiBaseUrl}${AUTH_ME_PATH}`;

describe('AuthService', () => {
  let service: AuthService;
  let authStorage: AuthStorageService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    service = TestBed.inject(AuthService);
    authStorage = TestBed.inject(AuthStorageService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('stores the access token on successful login', () => {
    service.login('jane.doe', 'Password123!').subscribe();

    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(authStorage.getToken()).toBe('jwt-token');
  });

  it('updates the current user on successful login', () => {
    service.login('jane.doe', 'Password123!').subscribe();

    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(service.currentUser()).toEqual(testUser);
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('trims the submitted username', () => {
    service.login('  jane.doe  ', 'Password123!').subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.body.username).toBe('jane.doe');
    req.flush(loginResponse);
  });

  it('does not trim the submitted password', () => {
    service.login('jane.doe', '  Password123!  ').subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.body.password).toBe('  Password123!  ');
    req.flush(loginResponse);
  });

  it('does not retain a token when login fails', () => {
    service.login('jane.doe', 'wrong-password').subscribe({ error: () => undefined });

    httpMock
      .expectOne(loginUrl)
      .flush({ detail: 'Invalid username or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(authStorage.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('clears token and user state and navigates to /login on logout', () => {
    service.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush(loginResponse);

    const navigateByUrlSpy = spyOn(router, 'navigateByUrl');

    service.logout();

    expect(authStorage.getToken()).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(navigateByUrlSpy).toHaveBeenCalledWith('/login');
  });

  it('completes restoration immediately, unauthenticated, when no token is present', (done) => {
    service.restoreSession().subscribe(() => {
      expect(service.isInitializing()).toBeFalse();
      expect(service.isAuthenticated()).toBeFalse();
      done();
    });

    httpMock.expectNone(meUrl);
  });

  it('loads the current user when restoration succeeds', () => {
    authStorage.setToken('jwt-token');

    service.restoreSession().subscribe();
    httpMock.expectOne(meUrl).flush(testUser);

    expect(service.currentUser()).toEqual(testUser);
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isInitializing()).toBeFalse();
  });

  it('removes an invalid token when restoration fails', () => {
    authStorage.setToken('stale-token');

    service.restoreSession().subscribe();
    httpMock.expectOne(meUrl).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(authStorage.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.isInitializing()).toBeFalse();
  });

  it('does not issue a duplicate /api/auth/me request when restoreSession is called twice', () => {
    authStorage.setToken('jwt-token');

    service.restoreSession().subscribe();
    service.restoreSession().subscribe();

    httpMock.expectOne(meUrl).flush(testUser);

    expect(service.currentUser()).toEqual(testUser);
  });

  it('reports role membership correctly', () => {
    service.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush(loginResponse);

    expect(service.hasRole('Employee')).toBeTrue();
    expect(service.hasRole('Admin')).toBeFalse();
    expect(service.hasAnyRole(['Admin', 'Employee'])).toBeTrue();
    expect(service.hasAnyRole(['Admin', 'SupportAgent'])).toBeFalse();
  });
});
