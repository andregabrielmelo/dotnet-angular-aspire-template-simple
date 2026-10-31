import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import {
  Observable,
  catchError,
  defer,
  finalize,
  firstValueFrom,
  from,
  map,
  of,
  retry,
  shareReplay,
  switchMap,
  tap,
  throwError,
  timer,
} from 'rxjs';
import { API_BASE } from '../../core/api/api-paths';
import { ApiSchema } from '../../core/api/api-schema';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { REFRESH_LOCK } from '../../core/auth/refresh-lock';

export const AUTH_PATH = `${API_BASE}/auth`;

/** Set by the API (GET auth/antiforgery and every sign-in); Angular echoes it as X-XSRF-TOKEN. */
export const XSRF_COOKIE = 'XSRF-TOKEN';

type AccessTokenResponse = ApiSchema<'AccessTokenResponse'>;

/**
 * The session. The access token is kept in memory only, never in Web Storage, so a reload
 * starts without one: `ensureSession()` then gets a new one from the refresh token, an
 * HttpOnly cookie the app never sees. The API is the authority on the token; the app never
 * decodes it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly currentUser = inject(CurrentUserService);
  private readonly refreshLock = inject(REFRESH_LOCK);

  private readonly accessTokenSignal = signal<string | null>(null);
  private refreshing: Observable<boolean> | null = null;

  readonly accessToken = this.accessTokenSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);

  login(email: string, password: string): Observable<void> {
    return this.signIn(
      this.http.post<AccessTokenResponse>(`${AUTH_PATH}/login`, { email, password }),
    );
  }

  register(name: string, email: string, password: string): Observable<void> {
    return this.signIn(
      this.http.post<AccessTokenResponse>(`${AUTH_PATH}/register`, { name, email, password }),
    );
  }

  /** Signed in already, or able to be from the refresh cookie (e.g. after a reload). */
  ensureSession(): Observable<boolean> {
    return this.isAuthenticated() ? of(true) : this.refresh();
  }

  /**
   * Exchanges the refresh cookie for a new access token. Concurrent callers in this tab share
   * one request, and other tabs wait their turn (REFRESH_LOCK). `false`, with the session
   * cleared, when there is no session to refresh.
   */
  refresh(): Observable<boolean> {
    this.refreshing ??= defer(() =>
      from(this.refreshLock(() => firstValueFrom(this.refreshOnce()))),
    ).pipe(
      finalize(() => (this.refreshing = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshing;
  }

  /**
   * Signs out. The local session is gone at once, whatever the API answers: even when the
   * request fails, the app no longer presents the user as signed in.
   */
  logout(): void {
    this.withAntiforgery(this.http.post<void>(`${AUTH_PATH}/logout`, null))
      .pipe(
        catchError(() => of(null)),
        finalize(() => this.router.navigate(['/auth'])),
      )
      .subscribe();
    this.clearSession();
  }

  /** Forgets the access token and everything cached for the user. */
  clearSession(): void {
    this.accessTokenSignal.set(null);
    this.currentUser.clear();
  }

  private refreshOnce(): Observable<boolean> {
    return this.withAntiforgery(
      this.http.post<AccessTokenResponse>(`${AUTH_PATH}/refresh`, null),
    ).pipe(
      // 409: another request refreshed this session at the same moment; the cookie now holds
      // its new token, so a second try succeeds.
      retry({
        count: 1,
        delay: (error) =>
          error instanceof HttpErrorResponse && error.status === 409
            ? timer(200)
            : throwError(() => error),
      }),
      map((response) => {
        this.accessTokenSignal.set(response.accessToken);
        return true;
      }),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
    );
  }

  /** Refresh and sign-out need the antiforgery cookie, which a new tab may not have yet. */
  private withAntiforgery<T>(request: Observable<T>): Observable<T> {
    const hasToken = this.document.cookie
      .split(';')
      .some((cookie) => cookie.trim().startsWith(`${XSRF_COOKIE}=`));
    return hasToken
      ? request
      : this.http.get<void>(`${AUTH_PATH}/antiforgery`).pipe(switchMap(() => request));
  }

  private signIn(request: Observable<AccessTokenResponse>): Observable<void> {
    return request.pipe(
      tap((response) => {
        this.currentUser.clear();
        this.accessTokenSignal.set(response.accessToken);
      }),
      map(() => undefined),
    );
  }
}
