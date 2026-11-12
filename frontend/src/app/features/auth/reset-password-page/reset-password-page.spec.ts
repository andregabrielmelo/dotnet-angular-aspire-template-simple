import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ResetPasswordPage } from './reset-password-page';
import { AccountService } from '../account.service';

describe('ResetPasswordPage', () => {
  function setup(resetPassword: ReturnType<typeof vi.fn>) {
    TestBed.configureTestingModule({
      imports: [ResetPasswordPage],
      providers: [provideRouter([]), { provide: AccountService, useValue: { resetPassword } }],
    });
    const fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.componentRef.setInput('email', 'ada@example.com');
    fixture.componentRef.setInput('code', 'abc');
    fixture.detectChanges();
    const page = fixture.componentInstance;
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    const submit = (newPassword: string) => {
      page.form.setValue({ newPassword });
      page.submit();
      fixture.detectChanges();
    };
    return { submit, text, resetPassword };
  }

  const badRequest = (errors: Record<string, string[]>) =>
    throwError(() => new HttpErrorResponse({ status: 400, error: { errors } }));

  it('sets the new password with the link email and code', () => {
    const { submit, text, resetPassword } = setup(vi.fn().mockReturnValue(of(undefined)));

    submit('a new passphrase');

    expect(resetPassword).toHaveBeenCalledWith('ada@example.com', 'abc', 'a new passphrase');
    expect(text()).toContain("you're signed out everywhere");
  });

  it('offers a new link when the code is wrong or expired', () => {
    const { submit, text } = setup(
      vi.fn().mockReturnValue(badRequest({ code: ['The link is invalid or has expired.'] })),
    );

    submit('a new passphrase');

    expect(text()).toContain('Send a new link');
  });

  it('shows a rejected password next to its field', () => {
    const { submit, text } = setup(
      vi.fn().mockReturnValue(badRequest({ newPassword: ['Passwords must be at least 8.'] })),
    );

    submit('a new passphrase');

    expect(text()).toContain('Passwords must be at least 8.');
  });

  it('does not send a short password', () => {
    const { submit, resetPassword } = setup(vi.fn());

    submit('short');

    expect(resetPassword).not.toHaveBeenCalled();
  });
});
