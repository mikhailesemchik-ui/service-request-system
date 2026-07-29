import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

/** Protects routes that require an authenticated user. */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.initialized$.pipe(
    map(() => {
      if (authService.isAuthenticated()) {
        return true;
      }

      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }),
  );
};

/** Keeps already-authenticated users off the login page. */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.initialized$.pipe(
    map(() => (authService.isAuthenticated() ? router.parseUrl('/dashboard') : true)),
  );
};
