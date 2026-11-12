import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AccountService } from '../account.service';

type Status = 'idle' | 'saving' | 'done' | 'invalid-link';

/**
 * Opened from the reset email: sets a new password with the link's email and code. Every session
 * of the account ends, so the user signs in again with the new password.
 */
@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reset-password-page.html',
})
export class ResetPasswordPage {
  private readonly account = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);

  /** Query parameters of the emailed link. */
  readonly email = input<string>();
  readonly code = input<string>();

  readonly status = signal<Status>('idle');
  readonly passwordError = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  submit(): void {
    const email = this.email();
    const code = this.code();
    if (!email || !code) {
      this.status.set('invalid-link');
      return;
    }
    if (this.form.invalid || this.status() === 'saving') {
      this.form.markAllAsTouched();
      return;
    }

    this.status.set('saving');
    this.passwordError.set(null);
    this.errorMessage.set(null);
    this.account.resetPassword(email, code, this.form.getRawValue().newPassword).subscribe({
      next: () => this.status.set('done'),
      error: (error: HttpErrorResponse) => {
        const errors = (error.status === 400 ? (error.error?.errors ?? {}) : {}) as Record<
          string,
          string[]
        >;
        if (errors['code'] || errors['email']) {
          this.status.set('invalid-link');
          return;
        }
        this.status.set('idle');
        if (errors['newPassword']) {
          this.passwordError.set(errors['newPassword'][0]);
        } else {
          this.errorMessage.set('Your password could not be changed. Please try again later.');
        }
      },
    });
  }
}
