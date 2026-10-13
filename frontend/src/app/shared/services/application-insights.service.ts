import { Injectable } from '@angular/core';
import { ApplicationInsights, ICustomProperties } from '@microsoft/applicationinsights-web';
import { environment } from 'src/environments/environment';

@Injectable({
    providedIn: 'root',
})
export class ApplicationInsightsService {
    private readonly appInsights = new ApplicationInsights({
        config: {
            connectionString: environment.appInsights.connectionString,
            enableAutoRouteTracking: true, // option to log all route changes
        },
    });

    public init() {
        // TODO: Comment this out if connection string is set
        // this.appInsights.loadAppInsights();
    }

    public logPageView(name?: string, url?: string) {
        // option to call manually
        this.appInsights.trackPageView({
            name: name,
            uri: url,
        });
    }

    public logEvent(name: string, properties?: ICustomProperties) {
        this.appInsights.trackEvent({ name: name }, properties);
    }

    public logMetric(name: string, average: number, properties?: ICustomProperties) {
        this.appInsights.trackMetric({ name: name, average: average }, properties);
    }

    public logException(exception: Error, severityLevel?: number) {
        this.appInsights.trackException({ exception: exception, severityLevel: severityLevel });
    }

    public logTrace(message: string, properties?: ICustomProperties) {
        this.appInsights.trackTrace({ message: message }, properties);
    }
}
