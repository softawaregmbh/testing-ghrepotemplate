import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { KENDO_DIALOG } from '@progress/kendo-angular-dialog';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, RouterLink, KENDO_DIALOG],
    templateUrl: './app.component.html',
})
export class AppComponent {
    protected navigationOpened = false;
    protected profileMenuOpened = false;

    protected triggerNavigationOpened(): void {
        this.navigationOpened = !this.navigationOpened;
    }

    protected triggerProfileMenuOpened(): void {
        this.profileMenuOpened = !this.profileMenuOpened;
    }
}
