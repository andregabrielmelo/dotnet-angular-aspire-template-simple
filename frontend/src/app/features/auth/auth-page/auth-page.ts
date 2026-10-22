import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthService } from '../auth-service';

/**
 * Entry point for signed-out users. The actual forms are Keycloak's hosted pages: both buttons
 * leave the app, and Keycloak sends the browser back to /home once the user is signed in.
 */
@Component({
  selector: 'app-auth-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-page.html',
})
export class AuthPage implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly externalProviders = toSignal(this.authService.loadExternalProviders(), {
    initialValue: [],
  });

  ngOnInit(): void {
    this.authService.loadUser().subscribe((user) => {
      if (user) {
        this.router.navigateByUrl('/home');
      }
    });
  }

  login(): void {
    this.authService.login();
  }

  register(): void {
    this.authService.register();
  }

  loginWith(providerAlias: string): void {
    this.authService.loginWith(providerAlias);
  }
}
