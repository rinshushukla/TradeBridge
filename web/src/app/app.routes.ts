import { Routes } from '@angular/router';

import { LayoutComponent } from './layout/layout';
import { OverviewComponent } from './pages/overview/overview';
import { ShipmentsComponent } from './pages/shipments/shipments';
import { DocumentsComponent } from './pages/documents/documents';
import { PaymentsComponent } from './pages/payments/payments';
import { TeamComponent } from './pages/team/team';
import { SettingsComponent } from './pages/settings/settings';
import { LoginComponent } from './pages/auth/login';
import { SignupComponent } from './pages/auth/signup';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'signup', component: SignupComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'overview'
      },
      {
        path: 'overview',
        component: OverviewComponent
      },
      {
        path: 'shipments',
        component: ShipmentsComponent
      },
      {
        path: 'documents',
        component: DocumentsComponent
      },
      {
        path: 'payments',
        component: PaymentsComponent
      },
      {
        path: 'team',
        component: TeamComponent
      },
      {
        path: 'settings',
        component: SettingsComponent
      }
    ]
  },

  {
    path: '**',
    redirectTo: 'overview'
  }
];
