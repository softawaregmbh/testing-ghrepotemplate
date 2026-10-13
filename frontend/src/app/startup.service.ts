import { Injectable, Injector } from '@angular/core';
import { FaIconLibrary } from '@fortawesome/angular-fontawesome';
import { TranslateService } from '@ngx-translate/core';
import { appIcons } from './app.icons';
import { ApplicationInsightsService } from './shared/services/application-insights.service';

@Injectable()
export class StartupService {
    public load(injector: Injector) {
        this.initIcons(injector);
        this.initTranslation(injector);
        this.initApplicationInsights(injector);
    }

    private initIcons(injector: Injector) {
        const library = injector.get(FaIconLibrary);
        library.addIcons(...appIcons);
    }

    private initTranslation(injector: Injector) {
        const translateService = injector.get(TranslateService);
        translateService.use('en');
    }

    private initApplicationInsights(injector: Injector) {
        const applicationInsightsService = injector.get(ApplicationInsightsService);
        applicationInsightsService.init();
    }
}
