import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth-service';
import { describeAuthError, safeReturnUrl } from '../auth-messages';
import { ResendConfirmation } from '../resend-confirmation/resend-confirmation';

/**
 * Registration. It doesn't sign in: the API emails a confirmation link, so the page then asks
 * the user to open it, with a way to have it sent again.
 */
@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, ResendConfirmation],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register-page.html',
})
export class RegisterPage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  /** The address a confirmation link went to, once registered. */
  readonly registeredEmail = signal<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  ngOnInit(): void {
    // Already signed in: there's nothing to register.
    this.authService.ensureSession().subscribe((signedIn) => {
      if (signedIn) {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? undefined;
        this.router.navigateByUrl(safeReturnUrl(returnUrl));
      }
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, email, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);
    this.authService.register(name.trim(), email.trim(), password).subscribe({
      next: () => {
        this.submitting.set(false);
        this.registeredEmail.set(email.trim());
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(describeAuthError(error));
      },
    });
  }
}
