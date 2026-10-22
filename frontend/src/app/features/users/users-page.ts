import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';
import { UserRow } from './users.model';
import { UsersService } from './users.service';

/** User administration. Reaching it requires users:read; deleting requires users:delete. */
@Component({
  selector: 'app-users-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users-page.html',
})
export class UsersPage implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly currentUser = inject(CurrentUserService);

  protected readonly users = signal<UserRow[]>([]);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly canDelete = () => this.currentUser.hasPermission(Permission.UsersDelete);

  ngOnInit(): void {
    this.load();
  }

  deleteUser(user: UserRow): void {
    this.errorMessage.set(null);
    this.usersService.delete(user.id).subscribe({
      next: () => this.users.update((users) => users.filter((u) => u.id !== user.id)),
      error: (error: HttpErrorResponse) =>
        this.errorMessage.set(
          error.status === 403
            ? "You don't have permission to delete users."
            : `Could not delete ${user.name}.`,
        ),
    });
  }

  private load(): void {
    this.usersService.list(1, 50).subscribe({
      next: (page) => this.users.set(page.items),
      error: () => this.errorMessage.set('Could not load users.'),
    });
  }
}
