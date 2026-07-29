import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH, LoginResponse } from './auth.models';
import { AuthStorageService } from './auth-storage.service';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const loginUrl = `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authStorage: AuthStorageService;
  let authService: AuthService;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authStorage = TestBed.inject(AuthStorageService);
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches the Bearer token to requests targeting the backend API', () => {
    authStorage.setToken('jwt-token');

    http.get(`${environment.apiBaseUrl}/api/categories`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/categories`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush([]);
  });

  it('does not attach an Authorization header when no token is stored', () => {
    http.get(`${environment.apiBaseUrl}/api/categories`).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/categories`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('does not attach the token to requests outside the backend API', () => {
    authStorage.setToken('jwt-token');

    http.get('https://external.example.test/data').subscribe();

    const req = httpMock.expectOne('https://external.example.test/data');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('does not attach a token to the login request when no token exists', () => {
    http.post(loginUrl, { username: 'jane.doe', password: 'Password123!' }).subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({} as LoginResponse);
  });

  it('clears the session and redirects to /login on a 401 from an authenticated request', () => {
    authService.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush({
      accessToken: 'jwt-token',
      tokenType: 'Bearer',
      expiresAt: '2026-01-01T00:00:00Z',
      user: { id: 1, username: 'jane.doe', displayName: 'Jane Doe', email: 'jane.doe@example.test', role: 'Employee' },
    });

    const navigateSpy = spyOn(router, 'navigate');

    http.get(`${environment.apiBaseUrl}/api/categories`).subscribe({ error: () => undefined });
    httpMock
      .expectOne(`${environment.apiBaseUrl}/api/categories`)
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(authStorage.getToken()).toBeNull();
    expect(authService.isAuthenticated()).toBeFalse();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], jasmine.any(Object));
  });

  it('does not redirect when the login endpoint itself returns 401', () => {
    const navigateSpy = spyOn(router, 'navigate');

    http.post(loginUrl, { username: 'jane.doe', password: 'wrong' }).subscribe({ error: () => undefined });

    httpMock
      .expectOne(loginUrl)
      .flush({ detail: 'Invalid username or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('does not clear the session on a 403 response', () => {
    authService.login('jane.doe', 'Password123!').subscribe();
    httpMock.expectOne(loginUrl).flush({
      accessToken: 'jwt-token',
      tokenType: 'Bearer',
      expiresAt: '2026-01-01T00:00:00Z',
      user: { id: 1, username: 'jane.doe', displayName: 'Jane Doe', email: 'jane.doe@example.test', role: 'Employee' },
    });

    const navigateSpy = spyOn(router, 'navigate');

    http.post(`${environment.apiBaseUrl}/api/categories`, {}).subscribe({ error: () => undefined });
    httpMock
      .expectOne(`${environment.apiBaseUrl}/api/categories`)
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(authStorage.getToken()).toBe('jwt-token');
    expect(authService.isAuthenticated()).toBeTrue();
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
