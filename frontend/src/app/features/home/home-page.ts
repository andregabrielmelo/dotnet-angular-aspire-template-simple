import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth-service';
import { CurrentUserService } from '../../core/auth/current-user.service';
import { Permission } from '../../core/auth/permissions';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.html',
})
export class HomePage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly currentUser = inject(CurrentUserService);

  protected readonly user = this.authService.user;
  // From GET /api/users/me - a real API call through the backend for frontend (cookie in,
  // Bearer token out) that also creates the user's profile on first sign-in.
  protected readonly profile = this.currentUser.profile;
  protected readonly canManageUsers = () => this.currentUser.hasPermission(Permission.UsersRead);

  ngOnInit(): void {
    this.currentUser.load().subscribe();
  }

  logout(): void {
    this.authService.logout();
  }
}
