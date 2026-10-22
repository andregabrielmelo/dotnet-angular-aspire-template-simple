import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, firstValueFrom, of } from 'rxjs';
import { vi } from 'vitest';
import { permissionGuard } from './permission-guard';
import { CurrentUserService } from './current-user.service';
import { Permission } from './permissions';

describe('permissionGuard', () => {
  function run(granted: boolean) {
    const urlTree = {} as UrlTree;
    const createUrlTree = vi.fn().mockReturnValue(urlTree);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: CurrentUserService,
          useValue: { load: () => of(null), hasPermission: () => granted },
        },
        { provide: Router, useValue: { createUrlTree } },
      ],
    });
    const guard = permissionGuard(Permission.UsersRead);
    const result = TestBed.runInInjectionContext(
      () =>
        guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot) as Observable<
          boolean | UrlTree
        >,
    );
    return { result: firstValueFrom(result), urlTree, createUrlTree };
  }

  it('allows users holding the permission', async () => {
    expect(await run(true).result).toBe(true);
  });

  it('sends everyone else to /home', async () => {
    const { result, urlTree, createUrlTree } = run(false);

    expect(await result).toBe(urlTree);
    expect(createUrlTree).toHaveBeenCalledWith(['/home']);
  });
});
