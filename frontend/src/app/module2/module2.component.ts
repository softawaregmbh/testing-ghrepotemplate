import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { GetItemByIdQuery, ItemsClient } from '../shared/services/generated/api-clients';
import { NotificationService } from '../shared/services/notification.service';
import { AdminApiService } from './shared/services/admin-api.service';

@Component({
    selector: 'app-module2',
    imports: [RouterOutlet],
    providers: [AdminApiService], // NOTE: We only use AdminApiService in this module, so we only need to provide it here.
    templateUrl: './module2.component.html',
})
export default class Module2Component implements OnInit {
    private readonly itemsClient = inject(ItemsClient);
    private readonly notificationService = inject(NotificationService);

    protected isLoading = false;

    public async ngOnInit() {
        try {
            this.isLoading = true;
            const item = await firstValueFrom(
                this.itemsClient.getItemByIdQuery(
                    new GetItemByIdQuery({
                        id: '00000000-0000-0000-0000-000000000000',
                    }),
                ),
            );
            console.log(item);
            this.notificationService.showSuccess('Success');
        } catch (error) {
            console.error(error);
            this.notificationService.showError('Error');
        } finally {
            this.isLoading = false;
        }
    }
}
