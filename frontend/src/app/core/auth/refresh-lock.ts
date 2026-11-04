import { InjectionToken } from '@angular/core';

/**
 * Runs a refresh so that only one tab of the app refreshes at a time (the Web Locks API).
 * Tabs share the refresh cookie, which works once: the second tab, inside the lock, finds the
 * first tab's new access token in localStorage and uses it instead of refreshing. Without Web
 * Locks the refresh just runs, and a tab that loses the race is signed out. A token so tests
 * can replace it.
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
