import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService, SESSION_STORAGE_KEY, Session } from './auth-service';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { REFRESH_LOCK } from '../../core/auth/refresh-lock';

function session(accessToken: string, expiresInMs = 15 * 60_000): Session {
  return { accessToken, accessTokenExpiry: new Date(Date.now() + expiresInMs).toISOString() };
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const navigate = vi.fn();
  const clear = vi.fn();
  let lockCalls: number;

  function setup(stored: Session | null = null) {
    if (stored) {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(stored));
    }
    navigate.mockReset().mockResolvedValue(true);
    clear.mockReset();
    lockCalls = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
        { provide: CurrentUserService, useValue: { clear } },
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

  function stored(): Session | null {
    const value = localStorage.getItem(SESSION_STORAGE_KEY);
    return value ? JSON.parse(value) : null;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('is not authenticated by default', () => {
    setup();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
  });

  it('login() keeps the session in localStorage', () => {
    setup();
    service.login('ada@example.com', 'secret').subscribe();

    const request = httpMock.expectOne('api/auth/login');
    expect(request.request.body).toEqual({ email: 'ada@example.com', password: 'secret' });
    const response = session('access-1');
    request.flush(response);

    expect(service.accessToken()).toBe('access-1');
    expect(service.isAuthenticated()).toBe(true);
    expect(stored()).toEqual(response);
  });

  it('register() signs the new account in', () => {
    setup();
    service.register('Ada', 'ada@example.com', 'long secret').subscribe();

    httpMock.expectOne('api/auth/register').flush(session('access-1'));

    expect(service.isAuthenticated()).toBe(true);
  });

  it('restores the stored session, as after a reload', () => {
    setup(session('access-1'));

    expect(service.accessToken()).toBe('access-1');
  });

  it('refresh() posts {} to the refresh route, inside the cross-tab lock', async () => {
    setup(session('access-1'));
    let result: boolean | undefined;
    service.refresh().subscribe((refreshed) => (result = refreshed));

    const request = await vi.waitFor(() => httpMock.expectOne('api/auth/refresh-token'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush(session('access-2'));

    await vi.waitFor(() => expect(result).toBe(true));
    expect(service.accessToken()).toBe('access-2');
    expect(stored()?.accessToken).toBe('access-2');
    expect(lockCalls).toBe(1);
  });

  it('refresh() shares one request between concurrent callers', async () => {
    setup();
    const results: boolean[] = [];
    service.refresh().subscribe((r) => results.push(r));
    service.refresh().subscribe((r) => results.push(r));

    // One request for both: expectOne fails if there were two.
    const request = await vi.waitFor(() => httpMock.expectOne('api/auth/refresh-token'));
    request.flush(session('a'));
    await vi.waitFor(() => expect(results).toEqual([true, true]));
  });

  it('refresh() uses the token another tab stored while it waited for the lock', async () => {
    setup(session('access-1', 0));
    // The other tab refreshes first: the cookie this tab holds has been used.
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session('from-other-tab')));
    let result: boolean | undefined;

    service.refresh().subscribe((r) => (result = r));

    await vi.waitFor(() => expect(result).toBe(true));
    expect(service.accessToken()).toBe('from-other-tab');
    httpMock.expectNone('api/auth/refresh-token');
  });

  it('a failed refresh leaves the app signed out', async () => {
    setup(session('access-1'));
    let result: boolean | undefined;

    service.refresh().subscribe((r) => (result = r));
    await vi.waitFor(() =>
      httpMock
        .expectOne('api/auth/refresh-token')
        .flush(null, { status: 400, statusText: 'Bad Request' }),
    );

    await vi.waitFor(() => expect(result).toBe(false));
    expect(service.isAuthenticated()).toBe(false);
    expect(stored()).toBeNull();
    expect(clear).toHaveBeenCalled();
  });

  it('ensureSession() refreshes only without a usable access token', async () => {
    setup(session('access-1'));
    let result: boolean | undefined;

    service.ensureSession().subscribe((r) => (result = r));

    expect(result).toBe(true);
    httpMock.expectNone('api/auth/refresh-token');
  });

  it('ensureSession() refreshes an expired stored token', async () => {
    setup(session('access-1', -1000));
    let result: boolean | undefined;

    service.ensureSession().subscribe((r) => (result = r));

    await vi.waitFor(() => httpMock.expectOne('api/auth/refresh-token').flush(session('access-2')));
    await vi.waitFor(() => expect(result).toBe(true));
  });

  it('follows another tab signing out', () => {
    setup(session('access-1'));

    localStorage.removeItem(SESSION_STORAGE_KEY);
    window.dispatchEvent(new StorageEvent('storage', { key: SESSION_STORAGE_KEY }));

    expect(service.isAuthenticated()).toBe(false);
    expect(clear).toHaveBeenCalled();
  });

  it('logout() deletes the refresh token with the access token, then clears the session', () => {
    setup(session('access-1'));

    service.logout();

    const request = httpMock.expectOne('api/auth/refresh-token');
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-1');
    request.flush(null, { status: 500, statusText: 'Error' });
    expect(service.isAuthenticated()).toBe(false);
    expect(stored()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/auth']);
  });

  it('logout() refreshes an expired access token first, so the API can revoke the session', async () => {
    setup(session('access-1', -1000));

    service.logout();

    const refresh = await vi.waitFor(() =>
      httpMock.expectOne((r) => r.url === 'api/auth/refresh-token' && r.method === 'POST'),
    );
    refresh.flush(session('access-2'));
    const logout = await vi.waitFor(() =>
      httpMock.expectOne((r) => r.url === 'api/auth/refresh-token' && r.method === 'DELETE'),
    );
    expect(logout.request.headers.get('Authorization')).toBe('Bearer access-2');
    logout.flush(null, { status: 204, statusText: 'No Content' });
    expect(service.isAuthenticated()).toBe(false);
  });
});
