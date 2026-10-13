import { JsonPipe } from '@angular/common';
import { Component, input, numberAttribute } from '@angular/core';
import { UserAddressInfoComponent } from './user-address-info/user-address-info.component';
import { UserContactInfoComponent } from './user-contact-info/user-contact-info.component';

@Component({
    selector: 'app-user-detail',
    imports: [JsonPipe, UserAddressInfoComponent, UserContactInfoComponent],
    templateUrl: './user-detail.component.html',
})
export default class UserDetailComponent {
    // NOTE: This is bound by the router and is available at ngOnInit
    public id = input.required({ transform: numberAttribute });
}
