import { Injectable, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, switchMap, tap } from 'rxjs';
import { API_BASE } from '../../core/api/api-paths';
import { CurrentUser, CurrentUserService } from '../../core/auth/current-user.service';

export const AUTH_PATH = `${API_BASE}/auth`;

/** Sets the XSRF-TOKEN cookie, which HttpClient sends back as X-XSRF-TOKEN on unsafe requests. */
export const ANTIFORGERY_TOKEN_PATH = `${AUTH_PATH}/antiforgery-token`;

/**
 * The session. Signing in makes the API set its session cookie (HttpOnly, so the app never sees
 * it), which the browser sends with every same-origin request, in every tab. The app only knows
 * whether it is signed in by asking: GET /api/users/me answers with the profile, or 401.
 *
 * A signed-in session's unsafe requests also need an antiforgery token, bound to the user:
 * the app fetches one at startup (app.config.ts) and again after signing in, and Angular's
 * HttpClient adds it as the X-XSRF-TOKEN header.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http: HttpClient = inject(HttpClient);
  private readonly router: Router = inject(Router);
  private readonly currentUser: CurrentUserService = inject(CurrentUserService);

  readonly isAuthenticated = computed(() => this.currentUser.profile() !== null);

  login(email: string, password: string): Observable<void> {
    return this.http.post<void>(`${AUTH_PATH}/login`, { email, password }).pipe(
      tap(() => this.currentUser.clear()),
      switchMap(() => this.fetchAntiforgeryToken()),
    );
  }

  /**
   * Creates an account. It doesn't sign in: the API emails a confirmation link, and the account
   * can sign in once it's confirmed.
   */
  register(name: string, email: string, password: string): Observable<void> {
    return this.http.post<void>(`${AUTH_PATH}/register`, { name, email, password });
  }

  /** Changes the password. This session stays signed in; the API ends the others within minutes. */
  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${AUTH_PATH}/change-password`, { currentPassword, newPassword });
  }

  /** Whether there is a session: loads the profile, which the API only gives a signed-in user. */
  ensureSession(): Observable<boolean> {
    return this.currentUser.load().pipe(map((profile: CurrentUser | null) => profile !== null));
  }

  /** Gets an antiforgery token for whoever is signed in now (or no one). Never fails. */
  fetchAntiforgeryToken(): Observable<void> {
    return this.http.get<void>(ANTIFORGERY_TOKEN_PATH).pipe(
      map(() => undefined),
      catchError(() => of(undefined)),
    );
  }

  /**
   * Signs out: the API deletes the session cookie. The app forgets the user whatever the API
   * answers, so it never presents them as signed in afterwards.
   */
  logout(): void {
    this.http
      .post<void>(`${AUTH_PATH}/logout`, {})
      .pipe(
        catchError(() => of(null)),
        finalize(() => {
          this.clearSession();
          this.router.navigate(['/auth']);
        }),
      )
      .subscribe();
  }

  /** Forgets everything cached about the user, once the session is gone. */
  clearSession(): void {
    this.currentUser.clear();
  }
}
