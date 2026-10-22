import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthPage } from './auth-page';
import { AuthService } from '../auth-service';

describe('AuthPage', () => {
  function setup(user: unknown) {
    const authService = {
      loadUser: () => of(user),
      login: vi.fn(),
      register: vi.fn(),
    };
    const navigateByUrl = vi.fn();
    TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: authService }],
    });
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockImplementation(navigateByUrl);
    const fixture = TestBed.createComponent(AuthPage);
    fixture.detectChanges();
    return { fixture, authService, navigateByUrl };
  }

  it('sends an already signed-in user to /home', () => {
    const { navigateByUrl } = setup({ name: 'Ada', email: null, logoutUrl: '/x' });

    expect(navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('starts login and registration through the auth service', () => {
    const { fixture, authService, navigateByUrl } = setup(null);

    fixture.componentInstance.login();
    fixture.componentInstance.register();

    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(authService.login).toHaveBeenCalled();
    expect(authService.register).toHaveBeenCalled();
  });
});
