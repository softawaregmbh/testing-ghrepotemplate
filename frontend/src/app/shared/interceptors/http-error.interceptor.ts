import { HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiError } from '../models';

export const httpErrorInterceptor: HttpInterceptorFn = (
    request: HttpRequest<unknown>,
    next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
    return next(request).pipe(
        catchError((err) => {
            const apiError: ApiError = {
                code: err.status,
                message: err.message,
                title: err.statusText,
            };
            return throwError(() => apiError);
        }),
    );
};
