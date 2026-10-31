import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthService } from '../auth-service';

type Mode = 'login' | 'register';

/** Sign-in and registration. Both sign the user in and go on to `returnUrl`, or /home. */
@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-page.html',
})
export class AuthPage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  /** Where to go once signed in (a query parameter set by authGuard). */
  readonly returnUrl = input<string>();

  readonly mode = signal<Mode>('login');
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

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
    const request: Observable<void> =
      this.mode() === 'register'
        ? this.authService.register(name.trim(), email.trim(), password)
        : this.authService.login(email.trim(), password);

    this.submitting.set(true);
    this.errorMessage.set(null);
    request.subscribe({
      next: () => this.goOn(),
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(this.describe(error));
      },
    });
  }

  private describe(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400: {
        const errors = (error.error?.errors ?? {}) as Record<string, string[]>;
        return Object.values(errors)[0]?.[0] ?? 'Check the form and try again.';
      }
      case 401:
        return 'Wrong email or password.';
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
