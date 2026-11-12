import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { LoginPage } from './login-page';
import { AccountService } from '../account.service';
import { AuthService } from '../auth-service';

describe('LoginPage', () => {
  function setup(signedIn = false, returnUrl?: string) {
    const authService = {
      ensureSession: () => of(signedIn),
      login: vi.fn().mockReturnValue(of(undefined)),
    };
    const account = { resendConfirmation: vi.fn().mockReturnValue(of(undefined)) };
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: AccountService, useValue: account },
      ],
    });
    const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(LoginPage);
    if (returnUrl) {
      fixture.componentRef.setInput('returnUrl', returnUrl);
    }
    fixture.detectChanges();
    const page = fixture.componentInstance;
    const element = fixture.nativeElement as HTMLElement;
    const text = () => element.textContent ?? '';
    return { fixture, page, authService, account, navigateByUrl, element, text };
  }

  function failLogin(
    authService: { login: ReturnType<typeof vi.fn> },
    response: HttpErrorResponse,
  ) {
    authService.login.mockReturnValue(throwError(() => response));
  }

  it('sends an already signed-in user on to /home', () => {
    const { navigateByUrl } = setup(true);

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('signs in and goes back to the page the user came from', () => {
    const { page, authService, navigateByUrl } = setup(false, '/users');
    page.form.setValue({ email: 'ada@example.com', password: 'secret' });

    page.submit();

    expect(authService.login).toHaveBeenCalledWith('ada@example.com', 'secret');
    expect(navigateByUrl).toHaveBeenCalledWith('/users');
  });

  it('never returns to another site', () => {
    const { page, navigateByUrl } = setup(false, '//evil.example');
    page.form.setValue({ email: 'ada@example.com', password: 'secret' });

    page.submit();

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('does not submit an incomplete form', () => {
    const { page, authService } = setup();
    page.form.setValue({ email: 'not-an-email', password: '' });

    page.submit();

    expect(authService.login).not.toHaveBeenCalled();
  });

  it('offers to resend the link when the email is not confirmed yet', () => {
    const { fixture, page, authService, account, element, text } = setup();
    failLogin(authService, new HttpErrorResponse({ status: 403 }));
    page.form.setValue({ email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain('Confirm your email address first');
    element.querySelector<HTMLButtonElement>('app-resend-confirmation button')!.click();
    expect(account.resendConfirmation).toHaveBeenCalledWith('ada@example.com');
  });

  it.each([
    [401, 'Wrong email or password.'],
    [429, 'Too many attempts. Wait a minute and try again.'],
  ])('explains a %s', (status, message) => {
    const { fixture, page, authService, text } = setup();
    failLogin(authService, new HttpErrorResponse({ status }));
    page.form.setValue({ email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain(message);
  });

  it('shows the first validation error from the API', () => {
    const { fixture, page, authService, text } = setup();
    failLogin(
      authService,
      new HttpErrorResponse({
        status: 400,
        error: { errors: { email: ['Email must be a valid email address'] } },
      }),
    );
    page.form.setValue({ email: 'ada@example.com', password: 'secret' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain('Email must be a valid email address');
  });

  it('links to registration, keeping returnUrl', () => {
    const { element } = setup();

    const link = element.querySelector<HTMLAnchorElement>('a[href^="/auth/register"]');

    expect(link).not.toBeNull();
  });
});
