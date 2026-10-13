import { Route } from '@angular/router';

export const module1Routes: Route = {
    loadComponent: () => import('./module1.component'),
    children: [{ path: '', loadComponent: () => import('./routed1/routed1.component') }],
};
