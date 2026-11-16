import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { Permission } from './permissions';
import { API_BASE } from '../api/api-paths';
import { ApiSchema } from '../api/api-schema';

export type CurrentUser = ApiSchema<'CurrentUserResponse'>;

/**
 * The signed-in user's application profile and the permissions their roles grant,
 * from GET /api/users/me. The answer is kept until the session changes (`clear()`, which
 * signing in and out call): a profile, or a 401 meaning no one is signed in. Any other failure
 * gives `null` but isn't kept, so the next `load()` asks again. Calls made while a request is
 * on its way share it, so the guards of one navigation make one request.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);

  private readonly profileSignal = signal<CurrentUser | null>(null);
  /** Whether `profileSignal` is the API's answer for this session. */
  private loaded = false;
  private inFlight: Observable<CurrentUser | null> | null = null;
  /** Bumped by `clear()`, so an answer that arrives after it is ignored. */
  private generation = 0;

  readonly profile = this.profileSignal.asReadonly();
  private readonly permissions = computed(() => new Set(this.profileSignal()?.permissions ?? []));

  load(): Observable<CurrentUser | null> {
    if (this.loaded) {
      return of(this.profileSignal());
    }
    if (this.inFlight) {
      return this.inFlight;
    }

    const generation = this.generation;
    const request: Observable<CurrentUser | null> = this.http
      .get<CurrentUser>(`${API_BASE}/users/me`)
      .pipe(
        map((profile) => ({ profile, known: true })),
        catchError((error: unknown) =>
          of({
            profile: null,
            known:
              error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized,
          }),
        ),
        tap(({ profile, known }) => {
          if (generation === this.generation) {
            this.profileSignal.set(profile);
            this.loaded = known;
          }
        }),
        map(({ profile }) => profile),
        finalize(() => {
          if (this.inFlight === request) {
            this.inFlight = null;
          }
        }),
        shareReplay(1),
      );
    this.inFlight = request;
    return request;
  }

  /** Forgets the cached profile, on sign-in and sign-out. */
  clear(): void {
    this.profileSignal.set(null);
    this.loaded = false;
    this.inFlight = null;
    this.generation++;
  }

  /** Applies a change the API has confirmed (such as a rename) without reloading the profile. */
  updateName(name: string): void {
    this.profileSignal.update((profile) => (profile ? { ...profile, name } : profile));
  }

  hasPermission(permission: Permission): boolean {
    return this.permissions().has(permission);
  }
}
