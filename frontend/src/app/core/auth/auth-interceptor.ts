import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE } from '../api/api-paths';
import { AUTH_PATH, AuthService } from '../../features/auth/auth-service';

function relativePath(url: string): string {
  return url.startsWith('/') ? url.slice(1) : url;
}

/**
 * Notices a session that ended (it expired, or a password change elsewhere ended it): a 401 from
 * the API while signed in forgets the user and sends them to sign in. It adds nothing to the
 * request - the browser sends the session cookie, and HttpClient the antiforgery token. The auth
 * endpoints are left alone: their errors are the caller's to handle.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const path = relativePath(request.url);
  if (!path.startsWith(`${API_BASE}/`) || path.startsWith(`${AUTH_PATH}/`)) {
    return next(request);
  }

  const authService = inject(AuthService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        authService.isAuthenticated()
      ) {
        authService.clearSession();
        router.navigate(['/auth'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};
