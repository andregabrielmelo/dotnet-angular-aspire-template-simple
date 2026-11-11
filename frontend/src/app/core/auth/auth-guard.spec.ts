import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, firstValueFrom, of } from 'rxjs';
import { vi } from 'vitest';
import { authGuard } from './auth-guard';
import { AuthService } from '../../features/auth/auth-service';

describe('authGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = { url: '/users' } as RouterStateSnapshot;

  function runGuard() {
    return firstValueFrom(
      TestBed.runInInjectionContext(() => authGuard(route, state) as Observable<boolean | UrlTree>),
    );
  }

  it('allows navigation when there is a session', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { ensureSession: () => of(true) } },
        { provide: Router, useValue: { createUrlTree: vi.fn() } },
      ],
    });

    expect(await runGuard()).toBe(true);
  });

  it('redirects to /auth, remembering where to go back, when there is none', async () => {
    const urlTree = {} as UrlTree;
    const createUrlTree = vi.fn().mockReturnValue(urlTree);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { ensureSession: () => of(false) } },
        { provide: Router, useValue: { createUrlTree } },
      ],
    });

    expect(await runGuard()).toBe(urlTree);
    expect(createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: { returnUrl: '/users' },
    });
  });
});
