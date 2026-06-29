export class Event {
    readonly type: string;
    readonly bubbles: boolean;
    readonly cancelable: boolean;
    readonly timeStamp: number;
    isTrusted = false;
    defaultPrevented = false;
    eventPhase = 0;
    target: any = null;
    currentTarget: any = null;
    _stoppedImmediate = false;

    constructor(type: string, init?: any) {
        this.type = String(type);
        this.bubbles = !!(init && init.bubbles);
        this.cancelable = !!(init && init.cancelable);
        this.timeStamp = Date.now();
    }

    preventDefault(): void {
        if (this.cancelable) this.defaultPrevented = true;
    }

    stopPropagation(): void { }

    stopImmediatePropagation(): void {
        this._stoppedImmediate = true;
    }
}
