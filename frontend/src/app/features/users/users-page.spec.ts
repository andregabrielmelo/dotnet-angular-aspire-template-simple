import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { PAGE_SIZE, UsersPage } from './users-page';
import { UserPage } from './users-model';
import { CurrentUserService } from '../../core/auth/current-user-service';
import { Permission } from '../../core/auth/permissions';

function aPage(page: number, totalPages: number): UserPage {
  return {
    items: [{ id: page * 10 + 7, name: `Ada Lovelace ${page}`, phoneNumber: null }],
    page,
    perPage: PAGE_SIZE,
    totalCount: totalPages * PAGE_SIZE,
    totalPages,
  };
}

describe('UsersPage', () => {
  async function setup(
    permissions: string[],
    options: { page?: unknown; expectedPage?: number; totalPages?: number } = {},
  ) {
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
    if (options.page !== undefined) {
      fixture.componentRef.setInput('page', options.page);
    }
    const expectedPage = options.expectedPage ?? 1;
    TestBed.tick();
    httpMock
      .expectOne(`api/users?page=${expectedPage}&per_page=${PAGE_SIZE}`)
      .flush(aPage(expectedPage, options.totalPages ?? 1));
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, httpMock, element: fixture.nativeElement as HTMLElement };
  }

  function link(element: HTMLElement, text: string): HTMLAnchorElement | undefined {
    return Array.from(element.querySelectorAll('a')).find((a) => a.textContent?.trim() === text);
  }

  it('lists the first page without delete buttons for read-only users', async () => {
    const { element } = await setup([Permission.UsersRead]);

    expect(element.textContent).toContain('Ada Lovelace 1');
    expect(element.querySelector('button')).toBeNull();
    // A single page needs no page links.
    expect(element.querySelector('nav')).toBeNull();
  });

  it('loads the page in the URL and links to its neighbours', async () => {
    const { element } = await setup([Permission.UsersRead], {
      page: 2,
      expectedPage: 2,
      totalPages: 3,
    });

    expect(element.textContent).toContain('Ada Lovelace 2');
    expect(element.textContent).toContain('Page 2 of 3');
    expect(link(element, 'Previous')?.getAttribute('href')).toContain('page=1');
    expect(link(element, 'Next')?.getAttribute('href')).toContain('page=3');
  });

  it('has no next link on the last page', async () => {
    const { element } = await setup([Permission.UsersRead], {
      page: 3,
      expectedPage: 3,
      totalPages: 3,
    });

    expect(link(element, 'Next')).toBeUndefined();
    expect(link(element, 'Previous')).toBeDefined();
  });

  it('treats a page that is not a positive number as the first', async () => {
    const { element } = await setup([Permission.UsersRead], { page: 'abc', expectedPage: 1 });

    expect(element.textContent).toContain('Ada Lovelace 1');
  });

  it('reloads the page after users with users:delete remove a user', async () => {
    const { fixture, httpMock, element } = await setup([
      Permission.UsersRead,
      Permission.UsersDelete,
    ]);

    element.querySelector('button')!.click();
    httpMock.expectOne('api/users/17').flush(null, { status: 204, statusText: 'No Content' });
    TestBed.tick();
    httpMock
      .expectOne(`api/users?page=1&per_page=${PAGE_SIZE}`)
      .flush({ ...aPage(1, 1), items: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(element.textContent).not.toContain('Ada Lovelace');
    expect(element.textContent).toContain('No users.');
  });

  it('explains a 403 from the API', async () => {
    const { fixture, httpMock, element } = await setup([
      Permission.UsersRead,
      Permission.UsersDelete,
    ]);

    element.querySelector('button')!.click();
    httpMock.expectOne('api/users/17').flush(null, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(element.textContent).toContain("You don't have permission to delete users.");
    expect(element.textContent).toContain('Ada Lovelace 1');
  });

  it('says when the users could not be loaded', async () => {
    TestBed.configureTestingModule({
      imports: [UsersPage],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CurrentUserService, useValue: { hasPermission: () => false } },
      ],
    });
    const fixture = TestBed.createComponent(UsersPage);
    const httpMock = TestBed.inject(HttpTestingController);
    TestBed.tick();
    httpMock
      .expectOne(`api/users?page=1&per_page=${PAGE_SIZE}`)
      .flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Could not load users.');
  });
});
