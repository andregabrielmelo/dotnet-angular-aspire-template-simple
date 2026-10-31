import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from './auth-service';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { REFRESH_LOCK } from '../../core/auth/refresh-lock';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const navigate = vi.fn();
  const clear = vi.fn();
  let lockCalls: number;

  function setup(cookie = 'XSRF-TOKEN=token') {
    navigate.mockReset().mockResolvedValue(true);
    clear.mockReset();
    lockCalls = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
        { provide: CurrentUserService, useValue: { clear } },
        { provide: DOCUMENT, useValue: { cookie } },
        {
          provide: REFRESH_LOCK,
          useValue: <T>(task: () => Promise<T>) => {
            lockCalls++;
            return task();
          },
        },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('is not authenticated by default', () => {
    setup();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
  });

  it('login() keeps the access token in memory', () => {
    setup();
    service.login('ada@example.com', 'secret').subscribe();

    const request = httpMock.expectOne('api/auth/login');
    expect(request.request.body).toEqual({ email: 'ada@example.com', password: 'secret' });
    request.flush({ accessToken: 'access-1', expiresIn: 900 });

    expect(service.accessToken()).toBe('access-1');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('register() signs the new account in', () => {
    setup();
    service.register('Ada', 'ada@example.com', 'long secret').subscribe();

    httpMock.expectOne('api/auth/register').flush({ accessToken: 'access-1', expiresIn: 900 });

    expect(service.isAuthenticated()).toBe(true);
  });

  it('refresh() replaces the access token, inside the cross-tab lock', async () => {
    setup();
    let result: boolean | undefined;
    service.refresh().subscribe((refreshed) => (result = refreshed));

    const request = await vi.waitFor(() => httpMock.expectOne('api/auth/refresh'));
    request.flush({ accessToken: 'access-2', expiresIn: 900 });

    await vi.waitFor(() => expect(result).toBe(true));
    expect(service.accessToken()).toBe('access-2');
    expect(lockCalls).toBe(1);
  });

  it('refresh() shares one request between concurrent callers', async () => {
    setup();
    const results: boolean[] = [];
    service.refresh().subscribe((r) => results.push(r));
    service.refresh().subscribe((r) => results.push(r));

    // One request for both: expectOne fails if there were two.
    const request = await vi.waitFor(() => httpMock.expectOne('api/auth/refresh'));
    request.flush({ accessToken: 'a', expiresIn: 900 });
    await vi.waitFor(() => expect(results).toEqual([true, true]));
  });

  it('refresh() fetches the antiforgery cookie first when there is none', async () => {
    setup('');
    service.refresh().subscribe();

    await vi.waitFor(() => httpMock.expectOne('api/auth/antiforgery').flush(null));
    await vi.waitFor(() =>
      httpMock.expectOne('api/auth/refresh').flush({ accessToken: 'a', expiresIn: 900 }),
    );
  });

  it('refresh() retries once after a 409 from a concurrent refresh', async () => {
    setup();
    let result: boolean | undefined;
    service.refresh().subscribe((r) => (result = r));

    await vi.waitFor(() =>
      httpMock.expectOne('api/auth/refresh').flush(null, { status: 409, statusText: 'Conflict' }),
    );
    await vi.waitFor(() =>
      httpMock.expectOne('api/auth/refresh').flush({ accessToken: 'a', expiresIn: 900 }),
    );
    await vi.waitFor(() => expect(result).toBe(true));
  });

  it('a failed refresh leaves the app signed out', async () => {
    setup();
    service.login('ada@example.com', 'secret').subscribe();
    httpMock.expectOne('api/auth/login').flush({ accessToken: 'access-1', expiresIn: 900 });
    let result: boolean | undefined;

    service.refresh().subscribe((r) => (result = r));
    await vi.waitFor(() =>
      httpMock
        .expectOne('api/auth/refresh')
        .flush(null, { status: 401, statusText: 'Unauthorized' }),
    );

    await vi.waitFor(() => expect(result).toBe(false));
    expect(service.isAuthenticated()).toBe(false);
    expect(clear).toHaveBeenCalled();
  });

  it('ensureSession() refreshes only without an access token', async () => {
    setup();
    service.login('ada@example.com', 'secret').subscribe();
    httpMock.expectOne('api/auth/login').flush({ accessToken: 'access-1', expiresIn: 900 });

    let result: boolean | undefined;
    service.ensureSession().subscribe((r) => (result = r));

    expect(result).toBe(true);
    httpMock.expectNone('api/auth/refresh');
  });

  it('logout() clears the session at once, even when the request fails', () => {
    setup();
    service.login('ada@example.com', 'secret').subscribe();
    httpMock.expectOne('api/auth/login').flush({ accessToken: 'access-1', expiresIn: 900 });

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(clear).toHaveBeenCalled();
    httpMock.expectOne('api/auth/logout').flush(null, { status: 500, statusText: 'Error' });
    expect(navigate).toHaveBeenCalledWith(['/auth']);
  });
});
