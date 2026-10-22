import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, firstValueFrom, of } from 'rxjs';
import { vi } from 'vitest';
import { authGuard } from './auth-guard';
import { AuthService } from '../../features/auth/auth-service';

describe('authGuard', () => {
  const route = {} as ActivatedRouteSnapshot;
  const state = {} as RouterStateSnapshot;

  function runGuard() {
    return firstValueFrom(
      TestBed.runInInjectionContext(() => authGuard(route, state) as Observable<boolean | UrlTree>),
    );
  }

  it('allows navigation when a session exists', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthService,
          useValue: { loadUser: () => of({ name: 'Ada', email: null, logoutUrl: '/x' }) },
        },
        { provide: Router, useValue: { createUrlTree: vi.fn() } },
      ],
    });

    expect(await runGuard()).toBe(true);
  });

  it('redirects to /auth when there is no session', async () => {
    const urlTree = {} as UrlTree;
    const createUrlTree = vi.fn().mockReturnValue(urlTree);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { loadUser: () => of(null) } },
        { provide: Router, useValue: { createUrlTree } },
      ],
    });

    expect(await runGuard()).toBe(urlTree);
    expect(createUrlTree).toHaveBeenCalledWith(['/auth']);
  });
});
