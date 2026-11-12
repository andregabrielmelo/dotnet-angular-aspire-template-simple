import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AccountService } from '../account.service';

/** Asks for a password reset link. The answer is the same whether or not the email has an account. */
@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forgot-password-page.html',
})
export class ForgotPasswordPage {
  private readonly account = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly sentTo = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const email = this.form.getRawValue().email.trim();
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.account.forgotPassword(email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sentTo.set(email);
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          error.status === 400
            ? (error.error?.errors?.email?.[0] ?? 'Enter a valid email address.')
            : 'Something went wrong. Please try again later.',
        );
      },
    });
  }
}
