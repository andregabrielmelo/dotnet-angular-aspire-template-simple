import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UsersService } from './users.service';
import { UserPage } from './users.model';

describe('UsersService', () => {
  function setup() {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    return {
      service: TestBed.inject(UsersService),
      httpMock: TestBed.inject(HttpTestingController),
    };
  }

  it('requests a page of users with the API query parameter names', () => {
    const { service, httpMock } = setup();
    let received: UserPage | undefined;

    service.list(2, 25).subscribe((page) => (received = page));
    const request = httpMock.expectOne('api/users?page=2&per_page=25');
    request.flush({ items: [], page: 2, perPage: 25, totalCount: 0, totalPages: 0 });

    expect(request.request.method).toBe('GET');
    expect(received?.page).toBe(2);
    httpMock.verify();
  });

  it('deletes a user by id', () => {
    const { service, httpMock } = setup();

    service.delete(7).subscribe();
    const request = httpMock.expectOne('api/users/7');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(request.request.method).toBe('DELETE');
    httpMock.verify();
  });

  it('updates a user by id', () => {
    const { service, httpMock } = setup();

    service.update(7, { name: 'Grace Hopper' }).subscribe();
    const request = httpMock.expectOne('api/users/7');
    request.flush({ user: { id: 7, name: 'Grace Hopper' } });

    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Grace Hopper' });
    httpMock.verify();
  });
});
