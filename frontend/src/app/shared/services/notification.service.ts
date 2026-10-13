import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { NotificationService as KendoNotificationService } from '@progress/kendo-angular-notification';

@Injectable({
    providedIn: 'root',
})
export class NotificationService {
    private readonly notificationService = inject(KendoNotificationService);
    private readonly translate = inject(TranslateService);

    public showSuccess(contentKey: string) {
        this.notificationService.show({
            content: this.translate.instant(contentKey),
            type: { style: 'success' },
            hideAfter: 1000,
        });
    }

    public showError(contentKey: string) {
        this.notificationService.show({
            content: this.translate.instant(contentKey),
            type: { style: 'error' },
            hideAfter: 2000,
        });
    }
}
