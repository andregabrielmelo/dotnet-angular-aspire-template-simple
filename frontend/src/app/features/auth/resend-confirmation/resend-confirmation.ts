import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { AccountService } from '../account.service';

/** A "send the confirmation link again" button, for the sign-in and registration pages. */
@Component({
  selector: 'app-resend-confirmation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './resend-confirmation.html',
})
export class ResendConfirmation {
  private readonly account = inject(AccountService);

  /** The address to send the link to. */
  readonly email = input.required<string>();
  readonly label = input('Send the confirmation link again');

  readonly status = signal<'idle' | 'sending' | 'sent' | 'failed'>('idle');

  resend(): void {
    if (this.status() === 'sending') {
      return;
    }

    this.status.set('sending');
    this.account.resendConfirmation(this.email()).subscribe({
      next: () => this.status.set('sent'),
      error: () => this.status.set('failed'),
    });
  }
}
