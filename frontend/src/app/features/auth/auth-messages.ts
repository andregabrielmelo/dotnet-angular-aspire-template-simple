import { HttpErrorResponse } from '@angular/common/http';

/** What to tell the user when signing in or up fails. */
export function describeAuthError(error: HttpErrorResponse): string {
  switch (error.status) {
    case 400: {
      const errors = (error.error?.errors ?? {}) as Record<string, string[]>;
      return Object.values(errors)[0]?.[0] ?? 'Check the form and try again.';
    }
    case 401:
      return 'Wrong email or password. After 5 wrong attempts, wait 5 minutes or reset your password.';
    case 403:
      return 'Confirm your email address first: open the link we emailed you.';
    case 409:
      return 'An account with this email already exists. Sign in instead.';
    case 429:
      return 'Too many attempts. Wait a minute and try again.';
    default:
      return 'Something went wrong. Please try again later.';
  }
}

/** Where to go once signed in: `returnUrl` if it's a path inside the app, never another site. */
export function safeReturnUrl(returnUrl: string | undefined): string {
  return returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/home';
}
