import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ProfilePage } from './profile-page';
import { CurrentUserService, CurrentUser } from '../../core/auth/current-user.service';
import { UsersService } from '../users/users.service';

describe('ProfilePage', () => {
  const profile: CurrentUser = { id: 7, name: 'Ada', email: 'ada@example.com', permissions: [] };

  function setup(update: ReturnType<typeof vi.fn>) {
    const updateName = vi.fn();
    TestBed.configureTestingModule({
      imports: [ProfilePage],
      providers: [
        provideRouter([]),
        { provide: UsersService, useValue: { update } },
        {
          provide: CurrentUserService,
          useValue: { load: () => of(profile), profile: signal(profile), updateName },
        },
      ],
    });
    const fixture = TestBed.createComponent(ProfilePage);
    fixture.detectChanges();
    return { fixture, page: fixture.componentInstance, updateName };
  }

  it('starts from the current name', () => {
    const { page } = setup(vi.fn());

    expect(page.form.getRawValue().name).toBe('Ada');
  });

  it('saves the trimmed name for the signed-in user', () => {
    const update = vi.fn().mockReturnValue(of({ user: { id: 7, name: 'Ada Lovelace' } }));
    const { fixture, page, updateName } = setup(update);

    page.form.setValue({ name: '  Ada Lovelace ' });
    page.save();
    fixture.detectChanges();

    expect(update).toHaveBeenCalledWith(7, { name: 'Ada Lovelace' });
    expect(updateName).toHaveBeenCalledWith('Ada Lovelace');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Profile saved.');
  });

  it('does not save an empty name', () => {
    const update = vi.fn();
    const { page } = setup(update);

    page.form.setValue({ name: '' });
    page.save();

    expect(update).not.toHaveBeenCalled();
  });

  it('shows the API validation message for the name', () => {
    const update = vi.fn().mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { errors: { name: ['Name is too long'] } },
          }),
      ),
    );
    const { page } = setup(update);

    page.save();

    expect(page.status()).toBe('idle');
    expect(page.errorMessage()).toBe('Name is too long');
  });
});
