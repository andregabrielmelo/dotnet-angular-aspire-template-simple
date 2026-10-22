import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService, BACKEND_FOR_FRONTEND_PATH } from '../../features/auth/auth-service';

/** Must match AntiforgeryHeaderMiddleware in AppTemplate.BackendForFrontend. */
export const CSRF_HEADER = 'X-CSRF';

function isBackendRequest(url: string): boolean {
  const path = url.startsWith('/') ? url.slice(1) : url;
  return path.startsWith('api/') || path.startsWith(`${BACKEND_FOR_FRONTEND_PATH}/`);
}

/**
 * Adds the CSRF header the backend for frontend requires on API and session calls (the session
 * cookie itself is attached by the browser). An API 401 means the session is gone - e.g. the
 * refresh token expired - so the user is sent back through login.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isBackendRequest(request.url)) {
    return next(request);
  }

  const authService = inject(AuthService);
  const router = inject(Router);

  return next(request.clone({ setHeaders: { [CSRF_HEADER]: '1' } })).pipe(
    catchError((error: unknown) => {
      const isApiUnauthorized =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.includes(BACKEND_FOR_FRONTEND_PATH);

      if (isApiUnauthorized) {
        authService.clearUser();
        authService.login(router.url);
      }

      return throwError(() => error);
    }),
  );
};
