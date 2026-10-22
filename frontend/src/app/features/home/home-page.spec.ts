import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { HomePage } from './home-page';
import { AuthService } from '../auth/auth-service';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';

describe('HomePage', () => {
  const logout = vi.fn();

  function setup(permissions: string[]) {
    logout.mockClear();
    TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal({ name: 'Ada Lovelace', email: 'ada@example.com', logoutUrl: '/x' }),
            logout,
          },
        },
        {
          provide: CurrentUserService,
          useValue: {
            load: () => of(null),
            profile: signal({ id: 7, name: 'Ada Lovelace', email: 'ada@example.com', permissions }),
            hasPermission: (p: string) => permissions.includes(p),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
    return { fixture, text: (fixture.nativeElement as HTMLElement).textContent };
  }

  it('shows the user name and the API profile', () => {
    const { text } = setup([]);

    expect(text).toContain('Ada Lovelace');
    expect(text).toContain('Profile #7');
  });

  it('offers user management only with users:read', () => {
    expect(setup([]).text).not.toContain('Manage users');

    TestBed.resetTestingModule();
    expect(setup([Permission.UsersRead]).text).toContain('Manage users');
  });

  it('logs out through the auth service', () => {
    const { fixture } = setup([]);

    fixture.componentInstance.logout();

    expect(logout).toHaveBeenCalled();
  });
});
