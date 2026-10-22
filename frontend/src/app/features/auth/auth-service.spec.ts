import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';
import { AuthService } from './auth-service';
import { BROWSER_REDIRECT } from '../../core/auth/browser-redirect';
import { BackendForFrontendUser } from './models/backend-for-frontend-user.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let redirect: ReturnType<typeof vi.fn>;

  const user: BackendForFrontendUser = {
    name: 'Ada Lovelace',
    email: 'ada@example.com',
    logoutUrl: '/backend-for-frontend/logout?sid=abc',
  };

  beforeEach(() => {
    redirect = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: BROWSER_REDIRECT, useValue: redirect },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('is not authenticated by default', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
  });

  it('loadUser() stores the session returned by the backend for frontend', () => {
    let result: BackendForFrontendUser | null = null;
    service.loadUser().subscribe((u) => (result = u));

    httpMock.expectOne('backend-for-frontend/user').flush(user);

    expect(result).toEqual(user);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.name).toBe('Ada Lovelace');
  });

  it('loadUser() maps a 401 to no session', () => {
    let result: BackendForFrontendUser | null | undefined;
    service.loadUser().subscribe((u) => (result = u));

    httpMock
      .expectOne('backend-for-frontend/user')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('loadUser() only calls the server once', () => {
    service.loadUser().subscribe();
    httpMock.expectOne('backend-for-frontend/user').flush(user);

    service.loadUser().subscribe();
    httpMock.expectNone('backend-for-frontend/user');
  });

  it('login() and register() redirect to the backend for frontend with a return URL', () => {
    service.login('/home');
    service.register('/home');

    expect(redirect).toHaveBeenCalledWith('/backend-for-frontend/login?returnUrl=%2Fhome');
    expect(redirect).toHaveBeenCalledWith('/backend-for-frontend/register?returnUrl=%2Fhome');
  });

  it('logout() clears the session and redirects to the logout URL', () => {
    service.loadUser().subscribe();
    httpMock.expectOne('backend-for-frontend/user').flush(user);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(redirect).toHaveBeenCalledWith('/backend-for-frontend/logout?sid=abc');
  });
});
