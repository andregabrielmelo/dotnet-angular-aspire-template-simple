import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth-guard';
import { permissionGuard } from './core/auth/permission-guard';
import { Permission } from './core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full',
  },
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: 'home',
    canActivate: [authGuard],
    loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
  },
  {
    path: 'profile',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
  },
  {
    path: 'users',
    canActivate: [authGuard, permissionGuard(Permission.UsersRead)],
    loadComponent: () => import('./features/users/users-page').then((m) => m.UsersPage),
  },
];
