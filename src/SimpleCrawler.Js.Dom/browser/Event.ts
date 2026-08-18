export class Event {
    type: string;
    bubbles: boolean;
    cancelable: boolean;
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

    // The pre-constructor spelling, still how a polyfill built on document.createEvent names its event —
    // and it names it *after* creating it, so the type cannot be readonly.
    initEvent(type: unknown, bubbles?: unknown, cancelable?: unknown): void {
        this.type = String(type);
        (this as any).bubbles = !!bubbles;
        (this as any).cancelable = !!cancelable;
    }
}
