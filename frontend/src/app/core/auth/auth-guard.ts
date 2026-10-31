import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../features/auth/auth-service';

/** Lets signed-in users through, after restoring the session from the refresh cookie if needed. */
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService
    .ensureSession()
    .pipe(
      map((signedIn) =>
        signedIn
          ? true
          : router.createUrlTree(['/auth'], { queryParams: { returnUrl: state.url } }),
      ),
    );
};
