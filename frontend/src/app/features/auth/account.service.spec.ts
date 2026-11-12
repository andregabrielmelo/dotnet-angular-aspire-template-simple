import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AccountService } from './account.service';

describe('AccountService', () => {
  let service: AccountService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AccountService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it.each([
    [
      'confirmEmail',
      () => service.confirmEmail(7, 'code'),
      'api/auth/confirm-email',
      { userId: 7, code: 'code' },
    ],
    [
      'resendConfirmation',
      () => service.resendConfirmation('ada@example.com'),
      'api/auth/resend-confirmation',
      { email: 'ada@example.com' },
    ],
    [
      'forgotPassword',
      () => service.forgotPassword('ada@example.com'),
      'api/auth/forgot-password',
      { email: 'ada@example.com' },
    ],
    [
      'resetPassword',
      () => service.resetPassword('ada@example.com', 'code', 'new secret'),
      'api/auth/reset-password',
      { email: 'ada@example.com', code: 'code', newPassword: 'new secret' },
    ],
  ])('%s posts to its endpoint', (_name, call, url, body) => {
    call().subscribe();

    const request = httpMock.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(null);
  });
});
