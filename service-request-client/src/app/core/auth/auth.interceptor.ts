import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AUTH_LOGIN_PATH } from './auth.models';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isApiRequest = request.url.startsWith(environment.apiBaseUrl);
  const token = isApiRequest ? authService.getAccessToken() : null;

  const authorizedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      const isLoginRequest = request.url === `${environment.apiBaseUrl}${AUTH_LOGIN_PATH}`;

      if (error instanceof HttpErrorResponse && error.status === 401 && isApiRequest && !isLoginRequest) {
        authService.handleUnauthorized(router.url);
      }

      return throwError(() => error);
    }),
  );
};
