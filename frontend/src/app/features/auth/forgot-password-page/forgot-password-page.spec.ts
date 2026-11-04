import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ForgotPasswordPage } from './forgot-password-page';
import { AccountService } from '../account.service';

describe('ForgotPasswordPage', () => {
  function setup() {
    const forgotPassword = vi.fn().mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      imports: [ForgotPasswordPage],
      providers: [provideRouter([]), { provide: AccountService, useValue: { forgotPassword } }],
    });
    const fixture = TestBed.createComponent(ForgotPasswordPage);
    fixture.detectChanges();
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    return { fixture, page: fixture.componentInstance, forgotPassword, text };
  }

  it('asks for the link and says it was sent if the account exists', () => {
    const { fixture, page, forgotPassword, text } = setup();

    page.form.setValue({ email: 'ada@example.com' });
    page.submit();
    fixture.detectChanges();

    expect(forgotPassword).toHaveBeenCalledWith('ada@example.com');
    expect(text()).toContain('If an account exists for ada@example.com');
  });

  it('does not send an invalid email', () => {
    const { page, forgotPassword } = setup();

    page.form.setValue({ email: 'not-an-email' });
    page.submit();

    expect(forgotPassword).not.toHaveBeenCalled();
  });
});
