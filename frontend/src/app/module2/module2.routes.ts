import { Route } from '@angular/router';

export const module2Routes: Route = {
    loadComponent: () => import('./module2.component'),
    children: [
        {
            path: '',
            loadComponent: () => import('./user-list/user-list.component'),
        },
        {
            path: ':id',
            loadComponent: () => import('./user-detail/user-detail.component'),
        },
    ],
};
