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

  const ada = {
    id: 1,
    name: 'Ada',
    email: 'ada@example.com',
    permissions: [Permission.UsersRead],
  };

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

  it('shares one request between loads made while it is on its way', () => {
    const answers: (string | undefined)[] = [];
    service.load().subscribe((profile) => answers.push(profile?.name));
    service.load().subscribe((profile) => answers.push(profile?.name));

    httpMock.expectOne('api/users/me').flush(ada);

    expect(answers).toEqual(['Ada', 'Ada']);
  });

  it('remembers a signed-out visitor until the session changes', () => {
    let answer: unknown = 'none yet';
    service.load().subscribe((profile) => (answer = profile));
    httpMock.expectOne('api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(answer).toBeNull();

    service.load().subscribe();
    httpMock.expectNone('api/users/me');

    service.clear();
    service.load().subscribe();
    httpMock.expectOne('api/users/me').flush(ada);
    expect(service.profile()?.name).toBe('Ada');
  });

  it('ignores an answer that arrives after the session changed', () => {
    service.load().subscribe();
    const stale = httpMock.expectOne('api/users/me');

    service.clear();
    stale.flush(null, { status: 401, statusText: 'Unauthorized' });

    service.load().subscribe();
    httpMock.expectOne('api/users/me').flush(ada);
    expect(service.profile()?.name).toBe('Ada');
  });
});
