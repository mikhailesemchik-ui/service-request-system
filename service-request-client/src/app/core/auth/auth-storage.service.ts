import { Injectable } from '@angular/core';

/**
 * Sole point of contact with browser storage for the access token.
 *
 * Stored in `sessionStorage` (not `localStorage`): this remains readable by
 * any JavaScript running on the page and is cleared when the tab/browser
 * closes. It is not a substitute for an HttpOnly-cookie design, which the
 * backend does not currently implement.
 */
@Injectable({ providedIn: 'root' })
export class AuthStorageService {
  private static readonly tokenKey = 'srs.auth.accessToken';

  getToken(): string | null {
    return sessionStorage.getItem(AuthStorageService.tokenKey);
  }

  setToken(token: string): void {
    sessionStorage.setItem(AuthStorageService.tokenKey, token);
  }

  clearToken(): void {
    sessionStorage.removeItem(AuthStorageService.tokenKey);
  }
}
