import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { CurrentUserService } from './current-user.service';
import { Permission } from './permissions';

/** Allows the route only for users holding `permission`; others are sent to /home. */
export function permissionGuard(permission: Permission): CanActivateFn {
  return () => {
    const currentUser = inject(CurrentUserService);
    const router = inject(Router);

    return currentUser
      .load()
      .pipe(
        map(() => (currentUser.hasPermission(permission) ? true : router.createUrlTree(['/home']))),
      );
  };
}
