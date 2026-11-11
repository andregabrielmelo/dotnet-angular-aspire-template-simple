import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AccountService } from '../account.service';
import { AuthService } from '../auth-service';

type Mode = 'login' | 'register';

/**
 * Sign-in and registration. Signing in goes on to `returnUrl`, or /home. Registering asks the
 * user to confirm their email first; so does signing in with an unconfirmed one, with a way to
 * have the link sent again.
 */
@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-page.html',
})
export class AuthPage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  /** Where to go once signed in (a query parameter set by authGuard). */
  readonly returnUrl = input<string>();

  readonly mode = signal<Mode>('login');
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** The address a confirmation link went to, after registering. */
  readonly registeredEmail = signal<string | null>(null);
  /** Signing in was refused until the email is confirmed. */
  readonly needsConfirmation = signal(false);
  readonly resent = signal(false);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.maxLength(100)]],
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

  switchTo(mode: Mode): void {
    this.mode.set(mode);
    this.errorMessage.set(null);
    this.registeredEmail.set(null);
    this.needsConfirmation.set(false);
    this.resent.set(false);
    const { name, password } = this.form.controls;
    name.setValidators(
      mode === 'register'
        ? [Validators.required, Validators.minLength(2), Validators.maxLength(100)]
        : [Validators.maxLength(100)],
    );
    password.setValidators(
      mode === 'register'
        ? [Validators.required, Validators.minLength(8), Validators.maxLength(128)]
        : [Validators.required, Validators.maxLength(128)],
    );
    name.updateValueAndValidity();
    password.updateValueAndValidity();
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, password } = this.form.getRawValue();
    const registering = this.mode() === 'register';
    const request: Observable<void> = registering
      ? this.authService.register(name.trim(), email.trim(), password)
      : this.authService.login(email.trim(), password);

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.needsConfirmation.set(false);
    this.resent.set(false);
    request.subscribe({
      next: () => {
        if (registering) {
          this.submitting.set(false);
          this.registeredEmail.set(email.trim());
        } else {
          this.goOn();
        }
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.needsConfirmation.set(error.status === 403);
        this.errorMessage.set(this.describe(error));
      },
    });
  }

  /** Sends the confirmation link again, to the address just registered or signed in with. */
  resendConfirmation(): void {
    const email = this.registeredEmail() ?? this.form.getRawValue().email.trim();
    this.account.resendConfirmation(email).subscribe({
      next: () => this.resent.set(true),
      error: () => this.errorMessage.set('The email could not be sent. Please try again later.'),
    });
  }

  private describe(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400: {
        const errors = (error.error?.errors ?? {}) as Record<string, string[]>;
        return Object.values(errors)[0]?.[0] ?? 'Check the form and try again.';
      }
      case 401:
        return 'Wrong email or password. After 5 wrong attempts, wait 5 minutes or reset your password.';
      case 403:
        return 'Confirm your email address first: open the link we emailed you.';
      case 409:
        return 'An account with this email already exists. Sign in instead.';
      case 429:
        return 'Too many attempts. Wait a minute and try again.';
      default:
        return 'Something went wrong. Please try again later.';
    }
  }

  private goOn(): void {
    const returnUrl = this.returnUrl();
    // Only paths inside the app, never another site.
    const target = returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/home';
    this.router.navigateByUrl(target);
  }
}
