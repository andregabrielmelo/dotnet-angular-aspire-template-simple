import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { authInterceptor, CSRF_HEADER } from './auth-interceptor';
import { AuthService } from '../../features/auth/auth-service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  const authService = { clearUser: vi.fn(), login: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: { url: '/home' } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('adds the CSRF header to API and backend-for-frontend requests', () => {
    http.get('api/users/me').subscribe();
    http.get('backend-for-frontend/user').subscribe();

    expect(httpMock.expectOne('api/users/me').request.headers.get(CSRF_HEADER)).toBe('1');
    expect(httpMock.expectOne('backend-for-frontend/user').request.headers.get(CSRF_HEADER)).toBe(
      '1',
    );
  });

  it('leaves other requests untouched', () => {
    http.get('assets/config.json').subscribe();

    expect(httpMock.expectOne('assets/config.json').request.headers.has(CSRF_HEADER)).toBe(false);
  });

  it('sends the user back through login when the API answers 401', () => {
    http.get('api/users/me').subscribe({ error: () => undefined });

    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(authService.clearUser).toHaveBeenCalled();
    expect(authService.login).toHaveBeenCalledWith('/home');
  });

  it('does not redirect when the session endpoint answers 401', () => {
    http.get('backend-for-frontend/user').subscribe({ error: () => undefined });

    httpMock
      .expectOne('backend-for-frontend/user')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(authService.login).not.toHaveBeenCalled();
  });
});
