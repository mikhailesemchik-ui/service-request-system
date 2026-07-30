import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';
import { AppShellComponent } from './core/layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in',
    loadComponent: () =>
      import('./features/authentication/pages/login.page').then((m) => m.LoginPageComponent),
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard',
        loadComponent: () =>
          import('./features/dashboard/pages/dashboard.page').then((m) => m.DashboardPageComponent),
      },
      {
        path: 'categories',
        title: 'Categories',
        loadComponent: () =>
          import('./features/categories/pages/categories.page').then((m) => m.CategoriesPageComponent),
      },
      {
        path: 'requests',
        title: 'Requests',
        loadComponent: () =>
          import('./features/requests/pages/requests-list.page').then((m) => m.RequestsListPageComponent),
      },
      {
        path: 'requests/new',
        title: 'New request',
        loadComponent: () =>
          import('./features/requests/pages/create-request.page').then((m) => m.CreateRequestPageComponent),
      },
      {
        path: 'requests/:requestId',
        title: 'Request details',
        loadComponent: () =>
          import('./features/requests/pages/request-details.page').then((m) => m.RequestDetailsPageComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
