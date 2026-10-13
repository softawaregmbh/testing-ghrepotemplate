import { Component } from '@angular/core';
import { Unrouted1Component } from './unrouted1/unrouted1.component';
import { Unrouted2Component } from './unrouted2/unrouted2.component';

@Component({
    selector: 'app-routed1',
    imports: [Unrouted1Component, Unrouted2Component],
    templateUrl: './routed1.component.html',
})
export default class Routed1Component {}
