import { InjectionToken } from '@angular/core';

/**
 * Full-page navigation, used for the OpenID Connect login/register/logout round-trips (they
 * leave the SPA for Keycloak). A token rather than a direct `window.location` call so tests can
 * replace it.
 */
export const BROWSER_REDIRECT = new InjectionToken<(url: string) => void>('BROWSER_REDIRECT', {
  providedIn: 'root',
  factory: () => (url: string) => window.location.assign(url),
});
