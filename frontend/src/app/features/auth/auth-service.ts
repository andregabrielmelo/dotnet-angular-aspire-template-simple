import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { BROWSER_REDIRECT } from '../../core/auth/browser-redirect';
import { BackendForFrontendUser } from './models/backend-for-frontend-user.model';

export const BACKEND_FOR_FRONTEND_PATH = 'backend-for-frontend';

/**
 * Session state backed by the backend for frontend. Login, registration and logout are full-page
 * redirects through Keycloak; the resulting session is an HTTP-only cookie, so this service never
 * sees or stores a token - it only asks the backend for frontend who the current user is.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly redirect = inject(BROWSER_REDIRECT);

  private readonly userSignal = signal<BackendForFrontendUser | null>(null);
  private loaded = false;

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.userSignal() !== null);

  /** Asks the backend for frontend for the current session once, then serves it from memory. */
  loadUser(): Observable<BackendForFrontendUser | null> {
    if (this.loaded) {
      return of(this.userSignal());
    }

    return this.http.get<BackendForFrontendUser>(`${BACKEND_FOR_FRONTEND_PATH}/user`).pipe(
      map((user): BackendForFrontendUser | null => user),
      catchError(() => of(null)),
      tap((user) => {
        this.userSignal.set(user);
        this.loaded = true;
      }),
    );
  }

  /** Forgets the cached session, e.g. after the API reports it has expired. */
  clearUser(): void {
    this.userSignal.set(null);
    this.loaded = false;
  }

  login(returnUrl = '/home'): void {
    this.redirect(`/${BACKEND_FOR_FRONTEND_PATH}/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  register(returnUrl = '/home'): void {
    this.redirect(
      `/${BACKEND_FOR_FRONTEND_PATH}/register?returnUrl=${encodeURIComponent(returnUrl)}`,
    );
  }

  /** Ends both the local session and the Keycloak session, then returns to the app's root. */
  logout(): void {
    const logoutUrl = this.userSignal()?.logoutUrl;
    this.clearUser();
    this.redirect(logoutUrl ?? '/');
  }
}
