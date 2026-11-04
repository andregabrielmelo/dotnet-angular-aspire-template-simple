import { DOCUMENT, DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
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
  shareReplay,
  switchMap,
  tap,
} from 'rxjs';
import { API_BASE } from '../../core/api/api-paths';
import { ApiSchema } from '../../core/api/api-schema';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { REFRESH_LOCK } from '../../core/auth/refresh-lock';

export const AUTH_PATH = `${API_BASE}/auth`;

/** Refresh (POST) and sign out (DELETE); the only route the refresh cookie is sent to. */
export const REFRESH_TOKEN_PATH = `${AUTH_PATH}/refresh-token`;

/** Where the session is kept in localStorage, shared by the app's tabs. */
export const SESSION_STORAGE_KEY = 'apptemplate.session';

/** An access token this close to its expiry is refreshed before it is sent. */
const EXPIRY_MARGIN_MS = 30_000;

export type Session = ApiSchema<'AccessTokenResponse'>;

/**
 * The session. The access token and its expiry are kept in localStorage, so a reload or a new
 * tab is still signed in; the refresh token is an HttpOnly cookie the app never sees, sent by
 * the browser only to `api/auth/refresh-token`. The API is the authority on the token; the app
 * never decodes it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly currentUser = inject(CurrentUserService);
  private readonly refreshLock = inject(REFRESH_LOCK);

  private readonly sessionSignal = signal<Session | null>(this.readStoredSession());
  private refreshing: Observable<boolean> | null = null;

  readonly accessToken = computed(() => this.sessionSignal()?.accessToken ?? null);
  readonly isAuthenticated = computed(() => this.sessionSignal() !== null);

  constructor() {
    // Another tab signed in, refreshed or signed out: follow it.
    const window = this.document.defaultView;
    const onStorage = (event: StorageEvent) => {
      if (event.key === SESSION_STORAGE_KEY || event.key === null) {
        this.adopt(this.readStoredSession());
      }
    };
    window?.addEventListener('storage', onStorage);
    inject(DestroyRef).onDestroy(() => window?.removeEventListener('storage', onStorage));
  }

  login(email: string, password: string): Observable<void> {
    return this.signIn(this.http.post<Session>(`${AUTH_PATH}/login`, { email, password }));
  }

  /**
   * Creates an account. It doesn't sign in: the API emails a confirmation link, and the account
   * can sign in once it's confirmed.
   */
  register(name: string, email: string, password: string): Observable<void> {
    return this.http.post<void>(`${AUTH_PATH}/register`, { name, email, password });
  }

  /**
   * Changes the password. The API ends every other session and returns a new one for this tab,
   * which replaces the current session (an expired access token is refreshed first).
   */
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    const ready = this.isExpiring() ? this.refresh() : of(true);
    return ready.pipe(
      switchMap(() =>
        this.signIn(
          this.http.post<Session>(
            `${AUTH_PATH}/change-password`,
            { currentPassword, newPassword },
            { headers: this.bearer() },
          ),
        ),
      ),
    );
  }

  /**
   * Signed in with a usable access token, or able to get one from the refresh cookie (after a
   * stored token expired, say).
   */
  ensureSession(): Observable<boolean> {
    return this.isExpiring() ? this.refresh() : of(true);
  }

  /** The access token is missing or about to expire, so it should be refreshed before use. */
  isExpiring(): boolean {
    const session = this.sessionSignal();
    return session === null || expiresSoon(session);
  }

  /**
   * Exchanges the refresh cookie for a new access token. Concurrent callers in this tab share
   * one request, and other tabs wait their turn (REFRESH_LOCK). `false`, with the session
   * cleared, when there is no session to refresh.
   */
  refresh(): Observable<boolean> {
    const before = this.accessToken();
    this.refreshing ??= defer(() =>
      from(this.refreshLock(() => firstValueFrom(this.refreshOnce(before)))),
    ).pipe(
      finalize(() => (this.refreshing = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshing;
  }

  /**
   * Signs out: the API deletes the refresh token and revokes the access token, which it needs
   * to be valid, so an expired one is refreshed first. The local session is gone whatever the
   * API answers, so the app never presents the user as signed in afterwards.
   */
  logout(): void {
    const signOut = this.isExpiring() ? this.refresh() : of(true);
    signOut
      .pipe(
        switchMap((signedIn) => {
          const token = this.accessToken();
          return signedIn && token
            ? this.http.delete<void>(REFRESH_TOKEN_PATH, { headers: this.bearer() })
            : of(null);
        }),
        catchError(() => of(null)),
        finalize(() => {
          this.clearSession();
          this.router.navigate(['/auth']);
        }),
      )
      .subscribe();
  }

  /** Forgets the access token and everything cached for the user, in every tab. */
  clearSession(): void {
    this.store(null);
  }

  private refreshOnce(before: string | null): Observable<boolean> {
    // Another tab refreshed while this one waited for the lock: its token is in storage, and the
    // cookie this tab would send has already been used.
    const stored = this.readStoredSession();
    if (stored && stored.accessToken !== before && !expiresSoon(stored)) {
      this.adopt(stored);
      return of(true);
    }

    return this.http.post<Session>(REFRESH_TOKEN_PATH, {}).pipe(
      map((session) => {
        this.store(session);
        return true;
      }),
      catchError(() => {
        this.clearSession();
        return of(false);
      }),
    );
  }

  /** The interceptor leaves api/auth/* alone, so the endpoints there that need the token get it here. */
  private bearer(): Record<string, string> {
    const token = this.accessToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  private signIn(request: Observable<Session>): Observable<void> {
    return request.pipe(
      tap((session) => {
        this.currentUser.clear();
        this.store(session);
      }),
      map(() => undefined),
    );
  }

  private store(session: Session | null): void {
    try {
      if (session) {
        this.storage?.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
      } else {
        this.storage?.removeItem(SESSION_STORAGE_KEY);
      }
    } catch {
      // Storage can be full or blocked; the session then lasts as long as this page.
    }
    this.adopt(session);
  }

  private adopt(session: Session | null): void {
    if (session === null) {
      this.currentUser.clear();
    }
    this.sessionSignal.set(session);
  }

  private readStoredSession(): Session | null {
    try {
      const value = this.storage?.getItem(SESSION_STORAGE_KEY);
      const session = value ? (JSON.parse(value) as Partial<Session>) : null;
      return session?.accessToken && session.accessTokenExpiry ? (session as Session) : null;
    } catch {
      return null;
    }
  }

  private get storage(): Storage | undefined {
    try {
      return this.document.defaultView?.localStorage;
    } catch {
      return undefined;
    }
  }
}

function expiresSoon(session: Session): boolean {
  return Date.parse(session.accessTokenExpiry) - Date.now() < EXPIRY_MARGIN_MS;
}
