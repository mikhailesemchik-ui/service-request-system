import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, ReplaySubject, catchError, finalize, map, of, shareReplay, tap, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH, AUTH_ME_PATH, AuthenticatedUser, LoginResponse, UserRole } from './auth.models';
import { AuthStorageService } from './auth-storage.service';

const SESSION_RESTORE_TIMEOUT_MS = 8000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly authStorage = inject(AuthStorageService);
  private readonly router = inject(Router);

  private readonly currentUserSignal = signal<AuthenticatedUser | null>(null);
  private readonly isInitializingSignal = signal(true);
  private readonly initializedSubject = new ReplaySubject<void>(1);
  private restoreSession$: Observable<void> | null = null;

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isInitializing = this.isInitializingSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);

  /**
   * Emits once, exactly when startup session restoration has finished (see APP_INITIALIZER in
   * app.config.ts). Guards wait on this rather than polling the `isInitializing` signal, since a
   * plain one-shot Observable is simpler to reason about and test than signal-to-observable
   * interop for a single startup completion event.
   */
  readonly initialized$: Observable<void> = this.initializedSubject.asObservable();

  login(username: string, password: string): Observable<AuthenticatedUser> {
    const request = { username: username.trim(), password };

    return this.http.post<LoginResponse>(`${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`, request).pipe(
      tap((response) => {
        this.authStorage.setToken(response.accessToken);
        this.currentUserSignal.set(response.user);
      }),
      map((response) => response.user),
    );
  }

  logout(): void {
    this.clearSession();
    void this.router.navigateByUrl('/login');
  }

  /**
   * Resolves once at startup (see APP_INITIALIZER in app.config.ts). Safe to call more than
   * once: the underlying HTTP call is cached via `shareReplay`, so a second caller replays the
   * same in-flight/completed result instead of issuing a duplicate `/api/auth/me` request.
   */
  restoreSession(): Observable<void> {
    if (this.restoreSession$) {
      return this.restoreSession$;
    }

    const token = this.authStorage.getToken();

    if (!token) {
      this.markInitialized();
      this.restoreSession$ = of(void 0);
      return this.restoreSession$;
    }

    this.restoreSession$ = this.http.get<AuthenticatedUser>(`${environment.apiBaseUrl}${AUTH_ME_PATH}`).pipe(
      timeout(SESSION_RESTORE_TIMEOUT_MS),
      tap((user) => this.currentUserSignal.set(user)),
      map(() => void 0),
      catchError(() => {
        this.clearSession();
        return of(void 0);
      }),
      finalize(() => this.markInitialized()),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    return this.restoreSession$;
  }

  /** Read-only token access for the HTTP interceptor; never touches storage directly. */
  getAccessToken(): string | null {
    return this.authStorage.getToken();
  }

  /** Used by the interceptor on a 401 from an authenticated request. */
  handleUnauthorized(returnUrl: string): void {
    if (!this.isAuthenticated() && this.authStorage.getToken() === null) {
      return;
    }

    this.clearSession();

    if (!this.router.url.startsWith('/login')) {
      void this.router.navigate(['/login'], { queryParams: { returnUrl } });
    }
  }

  hasRole(role: UserRole): boolean {
    return this.currentUserSignal()?.role === role;
  }

  hasAnyRole(roles: readonly UserRole[]): boolean {
    const role = this.currentUserSignal()?.role;
    return role !== undefined && roles.includes(role);
  }

  private clearSession(): void {
    this.authStorage.clearToken();
    this.currentUserSignal.set(null);
  }

  private markInitialized(): void {
    this.isInitializingSignal.set(false);
    this.initializedSubject.next();
    this.initializedSubject.complete();
  }
}
