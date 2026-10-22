import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, tap } from 'rxjs';
import { Permission } from './permissions';
import { API_BASE } from '../api/api-paths';
import { ApiSchema } from '../api/api-schema';

export type CurrentUser = ApiSchema<'CurrentUserResponse'>;

/**
 * The signed-in user's application profile and permissions, from GET /api/users/me (which
 * also provisions the profile on first sign-in). Loaded once and cached.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);

  private readonly profileSignal = signal<CurrentUser | null>(null);
  private loaded = false;

  readonly profile = this.profileSignal.asReadonly();
  private readonly permissions = computed(() => new Set(this.profileSignal()?.permissions ?? []));

  load(): Observable<CurrentUser | null> {
    if (this.loaded) {
      return of(this.profileSignal());
    }

    return this.http.get<CurrentUser>(`${API_BASE}/users/me`).pipe(
      catchError(() => of(null)),
      tap((profile) => {
        this.profileSignal.set(profile);
        this.loaded = profile !== null;
      }),
    );
  }

  /** Applies a change the API has confirmed (such as a rename) without reloading the profile. */
  updateName(name: string): void {
    this.profileSignal.update((profile) => (profile ? { ...profile, name } : profile));
  }

  hasPermission(permission: Permission): boolean {
    return this.permissions().has(permission);
  }
}
