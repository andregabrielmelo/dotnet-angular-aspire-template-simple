import { Routes } from '@angular/router';

/** Public pages: signing in and up, and the pages the account emails link to. */
export const AUTH_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./auth-page/auth-page').then((m) => m.AuthPage),
  },
  {
    path: 'confirm-email',
    loadComponent: () =>
      import('./confirm-email-page/confirm-email-page').then((m) => m.ConfirmEmailPage),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./forgot-password-page/forgot-password-page').then((m) => m.ForgotPasswordPage),
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./reset-password-page/reset-password-page').then((m) => m.ResetPasswordPage),
  },
];
