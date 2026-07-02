import { IdbRequest } from "./IdbRequest";

export class IdbOpenDbRequest extends IdbRequest {
    onupgradeneeded: any = null;
    onblocked: any = null;
}