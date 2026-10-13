import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, InjectionToken, Provider, inject } from '@angular/core';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import cacheBusting from './../../../../i18n-cache-busting.json';

export interface TranslateHttpLoaderConfig {
    /**
     * Use HttpBackend instead of HttpClient to bypass all interceptors (including MsalInterceptor)
     * This prevents the endless login redirect loop when loading translations before authentication
     */
    useHttpBackend: boolean;
}

export const TRANSLATE_HTTP_LOADER_CONFIG = new InjectionToken<Partial<TranslateHttpLoaderConfig>>(
    'MY_TRANSLATE_HTTP_LOADER_CONFIG',
);

@Injectable({
    providedIn: 'root',
})
export class TranslateHttpLoader implements TranslateLoader {
    private readonly config = inject(TRANSLATE_HTTP_LOADER_CONFIG);
    private readonly http = this.config.useHttpBackend ? new HttpClient(inject(HttpBackend)) : inject(HttpClient);

    public getTranslation(lang: string) {
        const path = `/assets/i18n/${lang}.json?v=${(cacheBusting as never)[lang]}`;
        return this.http.get<TranslationObject>(path);
    }
}

/**
 * Works similar to the default TranslateHttpLoader from @ngx-translate/http-loader,
 * but has a smarter cache busting mechanism. The default loader appends a timestamp query parameter
 * which prevents effective caching by the browser.
 */
export function provideTranslateHttpLoader(config: Partial<TranslateHttpLoaderConfig> = {}): Provider[] {
    const useBackend = config.useHttpBackend ?? false;

    return [
        {
            provide: TRANSLATE_HTTP_LOADER_CONFIG,
            useValue: config,
        },
        {
            provide: TranslateLoader,
            useClass: TranslateHttpLoader,
            deps: [useBackend ? HttpBackend : HttpClient, TRANSLATE_HTTP_LOADER_CONFIG],
        },
    ];
}
