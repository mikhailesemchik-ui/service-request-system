import { TestBed } from '@angular/core/testing';
import { AuthStorageService } from './auth-storage.service';

describe('AuthStorageService', () => {
  let service: AuthStorageService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AuthStorageService);
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('stores a token', () => {
    service.setToken('token-value');

    expect(sessionStorage.getItem('srs.auth.accessToken')).toBe('token-value');
  });

  it('retrieves a stored token', () => {
    service.setToken('token-value');

    expect(service.getToken()).toBe('token-value');
  });

  it('removes a token', () => {
    service.setToken('token-value');
    service.clearToken();

    expect(service.getToken()).toBeNull();
  });

  it('returns null when no token is present', () => {
    expect(service.getToken()).toBeNull();
  });

  it('does not store any user data alongside the token', () => {
    service.setToken('token-value');

    expect(sessionStorage.length).toBe(1);
  });
});
