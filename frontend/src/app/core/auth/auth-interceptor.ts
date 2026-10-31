import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE } from '../api/api-paths';
import { AUTH_PATH, AuthService } from '../../features/auth/auth-service';

function relativePath(url: string): string {
  return url.startsWith('/') ? url.slice(1) : url;
}

/**
 * Adds the access token to API requests. A 401 means it expired (it lasts 15 minutes): the
 * interceptor refreshes once and retries; if the session can't be refreshed, it sends the user
 * to sign in. The auth endpoints themselves are left alone - their errors are the caller's to
 * handle, and a failing refresh must never trigger another refresh. The X-XSRF-TOKEN header
 * the auth endpoints need is added by Angular's own XSRF interceptor.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const path = relativePath(request.url);
  if (!path.startsWith(`${API_BASE}/`) || path.startsWith(`${AUTH_PATH}/`)) {
    return next(request);
  }

  const authService = inject(AuthService);
  const router = inject(Router);
  const withToken = (outgoing: HttpRequest<unknown>) => {
    const token = authService.accessToken();
    return token ? outgoing.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : outgoing;
  };

  return next(withToken(request)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            router.navigate(['/auth'], { queryParams: { returnUrl: router.url } });
            return throwError(() => error);
          }
          // Straight to `next`, so a second 401 is returned as is, never refreshed again.
          return next(withToken(request));
        }),
      );
    }),
  );
};
