import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../features/auth/auth-service';

/** Lets signed-in users through: the API knows the session (its cookie) or answers 401. */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService
    .ensureSession()
    .pipe(
      map((signedIn: boolean) =>
        signedIn
          ? true
          : router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } }),
      ),
    );
};
