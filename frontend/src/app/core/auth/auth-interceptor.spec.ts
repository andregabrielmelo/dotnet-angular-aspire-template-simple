import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from '../../features/auth/auth-service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let signedIn: boolean;
  const clearSession = vi.fn();
  const navigate = vi.fn();

  beforeEach(() => {
    signedIn = true;
    clearSession.mockReset();
    navigate.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: { isAuthenticated: () => signedIn, clearSession },
        },
        { provide: Router, useValue: { url: '/users', navigate } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('adds no credentials: the browser sends the session cookie', () => {
    http.get('api/users/me').subscribe();

    expect(httpMock.expectOne('api/users/me').request.headers.has('Authorization')).toBe(false);
  });

  it('sends a signed-in user whose session ended to sign in, and passes the error on', () => {
    let status: number | undefined;
    http.get('api/users').subscribe({ error: (error) => (status = error.status) });

    httpMock.expectOne('api/users').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(clearSession).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/auth'], {
      queryParams: { returnUrl: '/users' },
    });
  });

  it('leaves a 401 alone when nobody was signed in (the guard handles that)', () => {
    signedIn = false;
    http.get('api/users/me').subscribe({ error: () => undefined });

    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearSession).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('leaves other errors and the auth endpoints alone', () => {
    http.get('api/users').subscribe({ error: () => undefined });
    http.post('api/auth/login', {}).subscribe({ error: () => undefined });

    httpMock.expectOne('api/users').flush(null, { status: 403, statusText: 'Forbidden' });
    httpMock.expectOne('api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(clearSession).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});
