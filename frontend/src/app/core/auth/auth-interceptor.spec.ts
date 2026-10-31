import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';
import { AuthService } from '../../features/auth/auth-service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let token: string | null;
  const refresh = vi.fn();
  const navigate = vi.fn();

  beforeEach(() => {
    token = 'access-1';
    refresh.mockReset();
    navigate.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { accessToken: () => token, refresh } },
        { provide: Router, useValue: { url: '/users', navigate } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('adds the access token to API requests', () => {
    http.get('api/users/me').subscribe();

    expect(httpMock.expectOne('api/users/me').request.headers.get('Authorization')).toBe(
      'Bearer access-1',
    );
  });

  it('leaves the auth endpoints and other requests alone', () => {
    http.post('api/auth/login', {}).subscribe();
    http.get('assets/config.json').subscribe();

    expect(httpMock.expectOne('api/auth/login').request.headers.has('Authorization')).toBe(false);
    expect(httpMock.expectOne('assets/config.json').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('refreshes once on a 401 and retries with the new token', () => {
    refresh.mockImplementation(() => {
      token = 'access-2';
      return of(true);
    });
    let body: unknown;
    http.get('api/users/me').subscribe((b) => (body = b));

    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    const retry = httpMock.expectOne('api/users/me');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ id: 1 });

    expect(body).toEqual({ id: 1 });
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('does not refresh again when the retry is also a 401', () => {
    refresh.mockReturnValue(of(true));
    let failed = false;
    http.get('api/users/me').subscribe({ error: () => (failed = true) });

    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failed).toBe(true);
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('sends the user to sign in when the session cannot be refreshed', () => {
    refresh.mockReturnValue(of(false));
    http.get('api/users/me').subscribe({ error: () => undefined });

    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).toHaveBeenCalledWith(['/auth'], { queryParams: { returnUrl: '/users' } });
  });

  it('never refreshes because of an auth endpoint', () => {
    http.post('api/auth/refresh', null).subscribe({ error: () => undefined });
    http.post('api/auth/login', {}).subscribe({ error: () => undefined });

    httpMock.expectOne('api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(refresh).not.toHaveBeenCalled();
  });

  it('passes other errors through', () => {
    let status = 0;
    http.get('api/users').subscribe({ error: (e) => (status = e.status) });

    httpMock.expectOne('api/users').flush(null, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(refresh).not.toHaveBeenCalled();
  });
});
