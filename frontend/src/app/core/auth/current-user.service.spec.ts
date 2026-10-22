import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CurrentUserService } from './current-user.service';
import { Permission } from './permissions';

describe('CurrentUserService', () => {
  let service: CurrentUserService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CurrentUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the profile once and answers permission checks from it', () => {
    service.load().subscribe();
    httpMock.expectOne('api/users/me').flush({
      id: 1,
      name: 'Ada',
      email: 'ada@example.com',
      permissions: [Permission.UsersRead],
    });

    service.load().subscribe();
    httpMock.expectNone('api/users/me');

    expect(service.profile()?.name).toBe('Ada');
    expect(service.hasPermission(Permission.UsersRead)).toBe(true);
    expect(service.hasPermission(Permission.UsersDelete)).toBe(false);
  });

  it('has no permissions when the profile cannot be loaded, and retries later', () => {
    service.load().subscribe();
    httpMock.expectOne('api/users/me').flush(null, { status: 500, statusText: 'Error' });

    expect(service.hasPermission(Permission.UsersRead)).toBe(false);

    service.load().subscribe();
    httpMock.expectOne('api/users/me').flush(null, { status: 500, statusText: 'Error' });
  });
});
