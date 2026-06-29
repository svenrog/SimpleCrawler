import { Event } from "./Event";

export class CustomEvent extends Event {
    detail: any;

    constructor(type: string, init?: any) {
        super(type, init);
        this.detail = init && init.detail !== undefined ? init.detail : null;
    }
}
