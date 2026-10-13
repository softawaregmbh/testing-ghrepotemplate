import { inject, Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { DialogService as KendoDialogService } from '@progress/kendo-angular-dialog';
import { ConfirmDialogComponent, ConfirmDialogResult } from '../components/confirm-dialog.component';
import { handleDialogResult } from '../utils';

@Injectable({
    providedIn: 'root',
})
export class DialogService {
    private readonly dialogService = inject(KendoDialogService);
    private readonly translate = inject(TranslateService);

    public async openConfirmDelete(titleKey = 'Actions.Delete'): Promise<boolean> {
        const dialogRef = this.dialogService.open({
            content: ConfirmDialogComponent,
            width: 400,
        });

        dialogRef.content.setInput('title', this.translate.instant(titleKey));
        dialogRef.content.setInput('content', this.translate.instant('AreYouSure'));
        dialogRef.content.setInput('confirmButton', {
            text: this.translate.instant('Actions.Delete'),
            theme: 'error',
        });

        const result = await handleDialogResult<ConfirmDialogResult>(dialogRef.result);
        return result?.confirm ?? false;
    }
}
