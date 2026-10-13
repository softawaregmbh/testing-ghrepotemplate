import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
    ApplicationConfig,
    ErrorHandler,
    inject,
    Injector,
    LOCALE_ID,
    provideAppInitializer,
    provideBrowserGlobalErrorListeners,
    provideZoneChangeDetection,
} from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { appRoutes } from './app.routes';
import { httpErrorInterceptor } from './shared/interceptors/http-error.interceptor';
import { ErrorHandlerService } from './shared/services/errorhandler.service';
import { provideTranslateHttpLoader } from './shared/services/translate-http-loader.service';
import { StartupService } from './startup.service';

export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        provideZoneChangeDetection({ eventCoalescing: true }),
        provideRouter(appRoutes, withComponentInputBinding()),
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideAnimations(),

        StartupService,
        provideAppInitializer(() => inject(StartupService).load(inject(Injector))),

        { provide: LOCALE_ID, useValue: 'en' },
        { provide: ErrorHandler, useClass: ErrorHandlerService },

        // 3rd party dependencies
        provideTranslateService({
            fallbackLang: 'en',
            // Adjust `useHttpBackend` here if interceptors should be bypassed.
            // May be needed when using MsalInterceptor for authentication.
            loader: provideTranslateHttpLoader({ useHttpBackend: false }),
        }),
    ],
};
