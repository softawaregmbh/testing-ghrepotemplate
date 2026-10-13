import { DialogCloseResult, DialogResult } from '@progress/kendo-angular-dialog';
import { Observable, firstValueFrom } from 'rxjs';

export async function handleDialogResult<TResult>(dialogResult: Observable<DialogResult>): Promise<TResult | null> {
    const result = await firstValueFrom(dialogResult);
    if (result instanceof DialogCloseResult) {
        return null;
    }

    return result as TResult;
}
