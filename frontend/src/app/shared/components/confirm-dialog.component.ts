import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonThemeColor, KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { DialogContentBase, KENDO_DIALOG } from '@progress/kendo-angular-dialog';

export interface ConfirmDialogResult {
    confirm: boolean;
}

@Component({
    selector: 'app-confirm-dialog',
    standalone: true,
    imports: [TranslatePipe, KENDO_BUTTON, KENDO_DIALOG],
    template: `
        <kendo-dialog-titlebar>{{ title() }}</kendo-dialog-titlebar>
        <div>{{ content() }}</div>
        <kendo-dialog-actions>
            <button kendoButton [themeColor]="confirmButton().theme" (click)="confirm()">
                {{ confirmButton().text | translate }}
            </button>
            <button kendoButton (click)="cancel()">
                {{ 'Actions.Cancel' | translate }}
            </button>
        </kendo-dialog-actions>
    `,
})
export class ConfirmDialogComponent extends DialogContentBase {
    public title = input.required<string>();
    public content = input.required<string>();
    public confirmButton = input<{ text: string; theme: ButtonThemeColor }>({
        text: 'Confirm',
        theme: 'primary',
    });

    protected cancel(): void {
        this.dialog.close({ confirm: false } as ConfirmDialogResult);
    }

    protected confirm(): void {
        this.dialog.close({ confirm: true } as ConfirmDialogResult);
    }
}
