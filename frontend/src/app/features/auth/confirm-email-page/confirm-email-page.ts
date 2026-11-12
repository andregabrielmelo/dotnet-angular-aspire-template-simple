import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccountService } from '../account.service';

type Status = 'confirming' | 'confirmed' | 'failed';

/** Opened from the confirmation email: confirms the address with the link's userId and code. */
@Component({
  selector: 'app-confirm-email-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './confirm-email-page.html',
})
export class ConfirmEmailPage implements OnInit {
  private readonly account = inject(AccountService);

  /** Query parameters of the emailed link. */
  readonly userId = input<string>();
  readonly code = input<string>();

  readonly status = signal<Status>('confirming');

  ngOnInit(): void {
    const userId = Number(this.userId());
    const code = this.code();
    if (!Number.isInteger(userId) || userId <= 0 || !code) {
      this.status.set('failed');
      return;
    }

    this.account.confirmEmail(userId, code).subscribe({
      next: () => this.status.set('confirmed'),
      error: () => this.status.set('failed'),
    });
  }
}
