import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { RegisterPage } from './register-page';
import { AccountService } from '../account.service';
import { AuthService } from '../auth-service';

describe('RegisterPage', () => {
  function setup(signedIn = false) {
    const authService = {
      ensureSession: () => of(signedIn),
      register: vi.fn().mockReturnValue(of(undefined)),
    };
    const account = { resendConfirmation: vi.fn().mockReturnValue(of(undefined)) };
    TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authService },
        { provide: AccountService, useValue: account },
      ],
    });
    const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();
    const page = fixture.componentInstance;
    const element = fixture.nativeElement as HTMLElement;
    const text = () => element.textContent ?? '';
    return { fixture, page, authService, account, navigateByUrl, element, text };
  }

  it('sends an already signed-in user on to /home', () => {
    const { navigateByUrl } = setup(true);

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('requires a name and 8 characters of password', () => {
    const { page, authService } = setup();
    page.form.setValue({ name: 'Ada', email: 'ada@example.com', password: 'short' });

    page.submit();
    expect(authService.register).not.toHaveBeenCalled();

    page.form.controls.password.setValue('long enough');
    page.submit();
    expect(authService.register).toHaveBeenCalledWith('Ada', 'ada@example.com', 'long enough');
  });

  it('asks the new account to confirm its email instead of signing in', () => {
    const { fixture, page, account, navigateByUrl, element, text } = setup();
    page.form.setValue({ name: 'Ada', email: 'ada@example.com', password: 'long enough' });

    page.submit();
    fixture.detectChanges();

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(text()).toContain('Check your email');
    expect(text()).toContain('ada@example.com');

    element.querySelector<HTMLButtonElement>('app-resend-confirmation button')!.click();
    fixture.detectChanges();
    expect(account.resendConfirmation).toHaveBeenCalledWith('ada@example.com');
    expect(text()).toContain('Sent. Check your inbox.');
  });

  it('says so when the email already has an account', () => {
    const { fixture, page, authService, text } = setup();
    authService.register.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    page.form.setValue({ name: 'Ada', email: 'ada@example.com', password: 'long enough' });

    page.submit();
    fixture.detectChanges();

    expect(text()).toContain('An account with this email already exists. Sign in instead.');
  });
});
