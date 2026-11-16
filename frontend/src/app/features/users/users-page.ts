import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  numberAttribute,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CurrentUserService } from '../../core/auth/current-user-service';
import { Permission } from '../../core/auth/permissions';
import { UserRow } from './users-model';
import { UsersService } from './users-service';

/** Users per page; the API's default page size. */
export const PAGE_SIZE = 10;

/** `?page=` as a page number: a positive integer, or 1 when it's missing or anything else. */
function toPage(value: unknown): number {
  const page = numberAttribute(value, 1);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

/**
 * User administration. Reaching it requires users:read; deleting requires users:delete. The page
 * number lives in the URL (`/users?page=2`, bound by withComponentInputBinding), so reloading,
 * going back or sharing the link keeps it.
 */
@Component({
  selector: 'app-users-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
})
export class UsersPage {
  private readonly usersService = inject(UsersService);
  private readonly currentUser = inject(CurrentUserService);

  readonly page = input(1, { transform: toPage });

  /** The current page of users, loaded again whenever the page changes. */
  protected readonly users = rxResource({
    params: () => ({ page: this.page() }),
    stream: ({ params }) => this.usersService.list(params.page, PAGE_SIZE),
  });

  protected readonly deleteError = signal<string | null>(null);
  protected readonly canDelete = computed(() =>
    this.currentUser.hasPermission(Permission.UsersDelete),
  );

  deleteUser(user: UserRow): void {
    this.deleteError.set(null);
    this.usersService.delete(user.id).subscribe({
      // Reload the page, so the next user moves up into it.
      next: () => this.users.reload(),
      error: (error: HttpErrorResponse) =>
        this.deleteError.set(
          error.status === 403
            ? "You don't have permission to delete users."
            : `Could not delete ${user.name}.`,
        ),
    });
  }
}
