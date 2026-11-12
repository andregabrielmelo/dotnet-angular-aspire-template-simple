import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';
import { describeAuthError, safeReturnUrl } from '../auth-messages';
import { ResendConfirmation } from '../resend-confirmation/resend-confirmation';

/**
 * Sign-in. Goes on to `returnUrl` (set by authGuard), or /home. Signing in with an unconfirmed
 * email offers to send the confirmation link again.
 */
@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, ResendConfirmation],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-page.html',
})
export class LoginPage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  /** Where to go once signed in (a query parameter set by authGuard). */
  readonly returnUrl = input<string>();

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** Signing in was refused until the email is confirmed. */
  readonly needsConfirmation = signal(false);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });

  ngOnInit(): void {
    // Signed in in another tab, or before a reload: no need to sign in again.
    this.authService.ensureSession().subscribe((signedIn) => {
      if (signedIn) {
        this.goOn();
      }
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.needsConfirmation.set(false);
    this.authService.login(email.trim(), password).subscribe({
      next: () => this.goOn(),
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.needsConfirmation.set(error.status === 403);
        this.errorMessage.set(describeAuthError(error));
      },
    });
  }

  private goOn(): void {
    this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
  }
}
