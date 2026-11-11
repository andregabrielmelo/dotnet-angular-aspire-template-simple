import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ANTIFORGERY_TOKEN_PATH, AuthService } from './auth-service';
import { CurrentUser, CurrentUserService } from '../../core/auth/current-user.service';

const ada: CurrentUser = { id: 1, name: 'Ada', email: 'ada@example.com', permissions: [] };

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const navigate = vi.fn();
  const clear = vi.fn();
  const load = vi.fn();
  const profile = signal<CurrentUser | null>(null);

  beforeEach(() => {
    navigate.mockReset().mockResolvedValue(true);
    clear.mockReset();
    load.mockReset();
    profile.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
        { provide: CurrentUserService, useValue: { clear, load, profile } },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('login() signs in, forgets the old profile and fetches an antiforgery token for the new session', () => {
    let done = false;
    service.login('ada@example.com', 'secret').subscribe(() => (done = true));

    const login = httpMock.expectOne('api/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({ email: 'ada@example.com', password: 'secret' });
    login.flush(null, { status: 204, statusText: 'No Content' });
    expect(clear).toHaveBeenCalled();
    expect(done).toBe(false);

    httpMock
      .expectOne(ANTIFORGERY_TOKEN_PATH)
      .flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('login() fails without fetching a token when the API refuses', () => {
    let failed = false;
    service.login('ada@example.com', 'wrong').subscribe({ error: () => (failed = true) });

    httpMock.expectOne('api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    httpMock.expectNone(ANTIFORGERY_TOKEN_PATH);
  });

  it('register() creates the account without signing in', () => {
    service.register('Ada', 'ada@example.com', 'a long password').subscribe();

    const request = httpMock.expectOne('api/auth/register');
    expect(request.request.body).toEqual({
      name: 'Ada',
      email: 'ada@example.com',
      password: 'a long password',
    });
    request.flush(null, { status: 201, statusText: 'Created' });
    expect(clear).not.toHaveBeenCalled();
  });

  it('changePassword() posts both passwords', () => {
    service.changePassword('old password', 'new password').subscribe();

    const request = httpMock.expectOne('api/auth/change-password');
    expect(request.request.body).toEqual({
      currentPassword: 'old password',
      newPassword: 'new password',
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('ensureSession() is true once the API returns the profile', () => {
    load.mockReturnValue(of(ada));
    let signedIn: boolean | undefined;

    service.ensureSession().subscribe((result) => (signedIn = result));

    expect(signedIn).toBe(true);
  });

  it('ensureSession() is false when the API knows no session', () => {
    load.mockReturnValue(of(null));
    let signedIn: boolean | undefined;

    service.ensureSession().subscribe((result) => (signedIn = result));

    expect(signedIn).toBe(false);
  });

  it('isAuthenticated() follows the loaded profile', () => {
    expect(service.isAuthenticated()).toBe(false);

    profile.set(ada);

    expect(service.isAuthenticated()).toBe(true);
  });

  it('fetchAntiforgeryToken() never fails', () => {
    let done = false;
    service.fetchAntiforgeryToken().subscribe(() => (done = true));

    httpMock
      .expectOne(ANTIFORGERY_TOKEN_PATH)
      .flush(null, { status: 503, statusText: 'Service Unavailable' });

    expect(done).toBe(true);
  });

  it('logout() signs out, forgets the user and goes to sign in', () => {
    service.logout();

    const request = httpMock.expectOne('api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(clear).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('logout() forgets the user even if the API fails', () => {
    service.logout();

    httpMock.expectOne('api/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });

    expect(clear).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
