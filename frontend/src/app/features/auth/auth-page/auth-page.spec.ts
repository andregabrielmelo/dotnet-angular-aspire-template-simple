import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthPage } from './auth-page';
import { AccountService } from '../account.service';
import { AuthService } from '../auth-service';

describe('AuthPage', () => {
  function setup(signedIn = false, returnUrl?: string) {
    const authService = {
      ensureSession: () => of(signedIn),
      login: vi.fn().mockReturnValue(of(undefined)),
      register: vi.fn().mockReturnValue(of(undefined)),
    };
    const account = { resendConfirmation: vi.fn().mockReturnValue(of(undefined)) };
    TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: AccountService, useValue: account },
      ],
    });
    const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AuthPage);
    if (returnUrl) {
      fixture.componentRef.setInput('returnUrl', returnUrl);
    }
    fixture.detectChanges();
    const page = fixture.componentInstance;
    const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    return { fixture, page, authService, account, navigateByUrl, text };
  }

  it('sends an already signed-in user on to /home', () => {
    const { navigateByUrl } = setup(true);

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('signs in and goes back to the page the user came from', () => {
    const { page, authService, navigateByUrl } = setup(false, '/users');
    page.form.setValue({ name: '', email: 'ada@example.com', password: 'secret' });

    page.submit();

    expect(authService.login).toHaveBeenCalledWith('ada@example.com', 'secret');
    expect(navigateByUrl).toHaveBeenCalledWith('/users');
  });

  it('never returns to another site', () => {
    const { page, navigateByUrl } = setup(false, '//evil.example');
    page.form.setValue({ name: '', email: 'ada@example.com', password: 'secret' });

    page.submit();

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('registers with a name, requiring 8 characters of password', () => {
    const { page, authService } = setup();
    page.switchTo('register');
    page.form.setValue({ name: 'Ada', email: 'ada@example.com', password: 'short' });

    page.submit();
    expect(authService.register).not.toHaveBeenCalled();

    page.form.controls.password.setValue('long enough');
    page.submit();
    expect(authService.register).toHaveBeenCalledWith('Ada', 'ada@example.com', 'long enough');
  });

  it('asks a new account to confirm its email instead of signing in', () => {
    const { fixture, page, account, navigateByUrl, text } = setup();
    page.switchTo('register');
    page.form.setValue({ name: 'Ada', email: 'ada@example.com', password: 'long enough' });

    page.submit();
    fixture.detectChanges();

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(text()).toContain('Check your email');
    expect(text()).toContain('ada@example.com');

    page.resendConfirmation();
    fixture.detectChanges();
    expect(account.resendConfirmation).toHaveBeenCalledWith('ada@example.com');
    expect(text()).toContain('Sent. Check your inbox.');
  });

  it('offers to resend the link when the email is not confirmed yet', () => {
    const { fixture, page, authService, account, text } = setup();
    authService.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    page.form.setValue({ name: '', email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain('Confirm your email address first');
    page.resendConfirmation();
    expect(account.resendConfirmation).toHaveBeenCalledWith('ada@example.com');
  });

  it.each([
    [401, 'Wrong email or password.'],
    [409, 'An account with this email already exists. Sign in instead.'],
  ])('explains a %s', (status, message) => {
    const { fixture, page, authService, text } = setup();
    authService.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status })));
    page.form.setValue({ name: '', email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain(message);
  });

  it('shows the first validation error from the API', () => {
    const { fixture, page, authService, text } = setup();
    authService.login.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { errors: { email: ['Email must be a valid email address'] } },
          }),
      ),
    );
    page.form.setValue({ name: '', email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain('Email must be a valid email address');
  });
});
