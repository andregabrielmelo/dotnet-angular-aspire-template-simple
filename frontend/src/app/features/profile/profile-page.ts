import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { AuthService } from '../auth/auth-service';
import { UsersService } from '../users/users.service';

type Status = 'idle' | 'saving' | 'saved';

/**
 * The signed-in user's own profile, which the API lets anyone update, and their password.
 * Changing the password signs every other device out; this tab gets a new session.
 */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile-page.html',
})
export class ProfilePage implements OnInit {
  private readonly currentUser = inject(CurrentUserService);
  private readonly users = inject(UsersService);
  private readonly authService = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  readonly profile = this.currentUser.profile;
  readonly status = signal<Status>('idle');
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
  });

  readonly passwordStatus = signal<Status>('idle');
  readonly passwordErrors = signal<{ currentPassword?: string; newPassword?: string }>({});
  readonly passwordMessage = signal<string | null>(null);

  readonly passwordForm = this.formBuilder.nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  ngOnInit(): void {
    this.currentUser.load().subscribe((profile) => {
      if (profile) {
        this.form.setValue({ name: profile.name });
      }
    });
  }

  save(): void {
    const profile = this.profile();
    if (!profile || this.form.invalid || this.status() === 'saving') {
      this.form.markAllAsTouched();
      return;
    }

    const name = this.form.getRawValue().name.trim();
    this.status.set('saving');
    this.errorMessage.set(null);

    this.users.update(profile.id, { name }).subscribe({
      next: () => {
        this.currentUser.updateName(name);
        this.status.set('saved');
      },
      error: (error: HttpErrorResponse) => {
        this.status.set('idle');
        this.errorMessage.set(
          error.status === 400
            ? (error.error?.errors?.name?.[0] ?? 'Check the name and try again.')
            : 'Your profile could not be saved. Please try again later.',
        );
      },
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid || this.passwordStatus() === 'saving') {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.passwordStatus.set('saving');
    this.passwordErrors.set({});
    this.passwordMessage.set(null);

    this.authService.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.passwordForm.reset();
        this.passwordStatus.set('saved');
      },
      error: (error: HttpErrorResponse) => {
        this.passwordStatus.set('idle');
        const errors = (error.status === 400 ? (error.error?.errors ?? {}) : {}) as Record<
          string,
          string[]
        >;
        if (errors['currentPassword'] || errors['newPassword']) {
          this.passwordErrors.set({
            currentPassword: errors['currentPassword']?.[0],
            newPassword: errors['newPassword']?.[0],
          });
        } else {
          this.passwordMessage.set('Your password could not be changed. Please try again later.');
        }
      },
    });
  }
}
