import { ErrorHandler, Injectable, inject } from '@angular/core';
import { ApplicationInsightsService } from './application-insights.service';

@Injectable()
export class ErrorHandlerService extends ErrorHandler {
    private readonly appInsightsService = inject(ApplicationInsightsService);

    public override handleError(error: Error) {
        this.appInsightsService.logException(error); // Manually log exception
        super.handleError(error);
    }
}
