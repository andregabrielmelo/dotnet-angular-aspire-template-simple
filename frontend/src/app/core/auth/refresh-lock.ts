import { InjectionToken } from '@angular/core';

/**
 * Runs a refresh so that only one tab of the app refreshes at a time (the Web Locks API).
 * Tabs share the refresh cookie: the second tab then sends the already rotated cookie instead
 * of racing the first with the same one. Without Web Locks, the refresh just runs; the API
 * answers a race with 409, which AuthService retries. A token so tests can replace it.
 */
export const REFRESH_LOCK = new InjectionToken<<T>(task: () => Promise<T>) => Promise<T>>(
  'REFRESH_LOCK',
  {
    providedIn: 'root',
    factory:
      () =>
      <T>(task: () => Promise<T>) =>
        typeof navigator !== 'undefined' && navigator.locks
          ? (navigator.locks.request('apptemplate-refresh', task) as Promise<T>)
          : task(),
  },
);
