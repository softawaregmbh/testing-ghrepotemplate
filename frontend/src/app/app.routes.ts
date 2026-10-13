import { Routes } from '@angular/router';
import { module1Routes } from './module1/module1.routes';
import { module2Routes } from './module2/module2.routes';

export const appRoutes: Routes = [
    { path: '', redirectTo: 'start', pathMatch: 'full' },
    {
        path: 'start',
        loadComponent: () => import('./start/start.component'),
    },
    { path: 'module1', ...module1Routes },
    { path: 'module2', ...module2Routes },
];
