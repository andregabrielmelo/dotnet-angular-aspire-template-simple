import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiSchema } from '../../core/api/api-schema';
import { AUTH_PATH } from './auth-service';

type ConfirmEmailRequest = ApiSchema<'ConfirmEmailRequest'>;
type ResendConfirmationRequest = ApiSchema<'ResendConfirmationRequest'>;
type ForgotPasswordRequest = ApiSchema<'ForgotPasswordRequest'>;
type ResetPasswordRequest = ApiSchema<'ResetPasswordRequest'>;

/**
 * The account flows that don't sign anyone in: confirming an email and resetting a password,
 * with the codes from the links the API emails. Signing in, out and changing the password are
 * AuthService's, because they change the session.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);

  confirmEmail(userId: number, code: string): Observable<void> {
    const body: ConfirmEmailRequest = { userId, code };
    return this.http.post<void>(`${AUTH_PATH}/confirm-email`, body);
  }

  /** Always succeeds (202): the API doesn't say whether the email has an account. */
  resendConfirmation(email: string): Observable<void> {
    const body: ResendConfirmationRequest = { email };
    return this.http.post<void>(`${AUTH_PATH}/resend-confirmation`, body);
  }

  /** Always succeeds (202): the API doesn't say whether the email has an account. */
  forgotPassword(email: string): Observable<void> {
    const body: ForgotPasswordRequest = { email };
    return this.http.post<void>(`${AUTH_PATH}/forgot-password`, body);
  }

  resetPassword(email: string, code: string, newPassword: string): Observable<void> {
    const body: ResetPasswordRequest = { email, code, newPassword };
    return this.http.post<void>(`${AUTH_PATH}/reset-password`, body);
  }
}
