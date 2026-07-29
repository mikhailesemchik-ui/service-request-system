import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { UserRole, isUserRole } from './auth.models';
import { AuthService } from './auth.service';

/**
 * Reusable role guard. Reads allowed roles from route data, e.g.:
 * `{ path: 'admin', canActivate: [roleGuard], data: { roles: ['Admin'] } }`.
 *
 * Missing or malformed `data.roles` denies access (redirect to `/dashboard`) rather than
 * throwing or granting access by default. A role mismatch never logs the user out — they
 * simply lack permission for this route.
 */
export const roleGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const rawRoles: unknown = route.data['roles'];
  const allowedRoles: UserRole[] = Array.isArray(rawRoles) ? rawRoles.filter(isUserRole) : [];

  return authService.initialized$.pipe(
    map(() => {
      if (!authService.isAuthenticated()) {
        return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      }

      if (allowedRoles.length > 0 && authService.hasAnyRole(allowedRoles)) {
        return true;
      }

      return router.parseUrl('/dashboard');
    }),
  );
};
