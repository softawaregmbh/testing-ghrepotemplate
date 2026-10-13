import { Component, inject, signal } from '@angular/core';
import { KENDO_GRID } from '@progress/kendo-angular-grid';
import { DialogService } from 'src/app/shared/services/dialog.service';

@Component({
    selector: 'app-user-list',
    imports: [KENDO_GRID],
    templateUrl: './user-list.component.html',
})
export default class UserListComponent {
    private dialogService = inject(DialogService);

    protected users = signal<{ id: number; name: string }[]>([
        { id: 1, name: 'John Doe' },
        { id: 2, name: 'Jane Doe' },
        { id: 3, name: 'Alice' },
        { id: 4, name: 'Bob' },
        { id: 5, name: 'Charlie' },
    ]);

    protected async openConfirmDialog() {
        const canDelete = await this.dialogService.openConfirmDelete('Test');
        console.log(canDelete);
    }
}
