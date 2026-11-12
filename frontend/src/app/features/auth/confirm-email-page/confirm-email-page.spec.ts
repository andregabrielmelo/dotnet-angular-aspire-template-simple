import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ConfirmEmailPage } from './confirm-email-page';
import { AccountService } from '../account.service';

describe('ConfirmEmailPage', () => {
  function setup(query: { userId?: string; code?: string }, confirmEmail = vi.fn()) {
    TestBed.configureTestingModule({
      imports: [ConfirmEmailPage],
      providers: [provideRouter([]), { provide: AccountService, useValue: { confirmEmail } }],
    });
    const fixture = TestBed.createComponent(ConfirmEmailPage);
    fixture.componentRef.setInput('userId', query.userId);
    fixture.componentRef.setInput('code', query.code);
    fixture.detectChanges();
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    return { text, confirmEmail };
  }

  it('confirms with the link userId and code', () => {
    const { text, confirmEmail } = setup(
      { userId: '7', code: 'abc' },
      vi.fn().mockReturnValue(of(undefined)),
    );

    expect(confirmEmail).toHaveBeenCalledWith(7, 'abc');
    expect(text()).toContain('Your email is confirmed.');
  });

  it('explains an invalid or expired link', () => {
    const { text } = setup(
      { userId: '7', code: 'abc' },
      vi.fn().mockReturnValue(throwError(() => new Error('400'))),
    );

    expect(text()).toContain('This link is invalid or has expired.');
  });

  it('does not call the API for an incomplete link', () => {
    const { text, confirmEmail } = setup({ userId: 'x' });

    expect(confirmEmail).not.toHaveBeenCalled();
    expect(text()).toContain('This link is invalid or has expired.');
  });
});
