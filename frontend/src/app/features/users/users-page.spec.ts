import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { UsersPage } from './users-page';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';

describe('UsersPage', () => {
  function setup(permissions: string[]) {
    TestBed.configureTestingModule({
      imports: [UsersPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CurrentUserService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
    const fixture = TestBed.createComponent(UsersPage);
    const httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpMock.expectOne('api/users?page=1&per_page=50').flush({
      items: [{ id: 7, name: 'Ada Lovelace', phoneNumber: null }],
      page: 1,
      perPage: 50,
      totalCount: 1,
      totalPages: 1,
    });
    fixture.detectChanges();
    return { fixture, httpMock, element: fixture.nativeElement as HTMLElement };
  }

  it('lists users without delete buttons for read-only users', () => {
    const { element } = setup([Permission.UsersRead]);

    expect(element.textContent).toContain('Ada Lovelace');
    expect(element.querySelector('button')).toBeNull();
  });

  it('lets users with users:delete remove a user', () => {
    const { fixture, httpMock, element } = setup([Permission.UsersRead, Permission.UsersDelete]);

    element.querySelector('button')!.click();
    httpMock.expectOne('api/users/7').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(element.textContent).not.toContain('Ada Lovelace');
  });

  it('explains a 403 from the API', () => {
    const { fixture, httpMock, element } = setup([Permission.UsersRead, Permission.UsersDelete]);

    element.querySelector('button')!.click();
    httpMock.expectOne('api/users/7').flush(null, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(element.textContent).toContain("You don't have permission to delete users.");
    expect(element.textContent).toContain('Ada Lovelace');
  });
});
