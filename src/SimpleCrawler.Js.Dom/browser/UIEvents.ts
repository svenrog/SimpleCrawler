import { Event } from "./Event";

// The event families a page constructs to drive its own UI — `new MouseEvent("click", …)` on a synthetic
// click, `new KeyboardEvent` on a shortcut shim. Nothing here is ever dispatched by a user, but the
// constructors are named at module scope, where an absent global is `X is not defined` for the whole chunk.
// Each carries the init members its handlers read; none has behaviour, because nothing points or types.
export class UIEvent extends Event {
    detail: number;
    view: any;

    constructor(type: string, init?: any) {
        super(type, init);
        this.detail = init && init.detail ? Number(init.detail) : 0;
        this.view = init && init.view !== undefined ? init.view : null;
    }
}

export class MouseEvent extends UIEvent {
    readonly button: number;
    readonly buttons: number;
    readonly clientX: number;
    readonly clientY: number;
    readonly screenX: number;
    readonly screenY: number;
    readonly pageX: number;
    readonly pageY: number;
    readonly altKey: boolean;
    readonly ctrlKey: boolean;
    readonly metaKey: boolean;
    readonly shiftKey: boolean;
    readonly relatedTarget: any;

    constructor(type: string, init?: any) {
        super(type, init);
        const i = init || {};
        this.button = Number(i.button) || 0;
        this.buttons = Number(i.buttons) || 0;
        this.clientX = Number(i.clientX) || 0;
        this.clientY = Number(i.clientY) || 0;
        this.screenX = Number(i.screenX) || 0;
        this.screenY = Number(i.screenY) || 0;
        this.pageX = Number(i.pageX) || this.clientX;
        this.pageY = Number(i.pageY) || this.clientY;
        this.altKey = !!i.altKey;
        this.ctrlKey = !!i.ctrlKey;
        this.metaKey = !!i.metaKey;
        this.shiftKey = !!i.shiftKey;
        this.relatedTarget = i.relatedTarget !== undefined ? i.relatedTarget : null;
    }
}

export class PointerEvent extends MouseEvent {
    readonly pointerId: number;
    readonly pointerType: string;
    readonly isPrimary: boolean;

    constructor(type: string, init?: any) {
        super(type, init);
        const i = init || {};
        this.pointerId = Number(i.pointerId) || 0;
        this.pointerType = i.pointerType ? String(i.pointerType) : "";
        this.isPrimary = !!i.isPrimary;
    }
}

export class KeyboardEvent extends UIEvent {
    readonly key: string;
    readonly code: string;
    readonly keyCode: number;
    readonly which: number;
    readonly repeat: boolean;
    readonly altKey: boolean;
    readonly ctrlKey: boolean;
    readonly metaKey: boolean;
    readonly shiftKey: boolean;

    constructor(type: string, init?: any) {
        super(type, init);
        const i = init || {};
        this.key = i.key ? String(i.key) : "";
        this.code = i.code ? String(i.code) : "";
        this.keyCode = Number(i.keyCode) || 0;
        this.which = Number(i.which) || this.keyCode;
        this.repeat = !!i.repeat;
        this.altKey = !!i.altKey;
        this.ctrlKey = !!i.ctrlKey;
        this.metaKey = !!i.metaKey;
        this.shiftKey = !!i.shiftKey;
    }
}

export class FocusEvent extends UIEvent {
    readonly relatedTarget: any;

    constructor(type: string, init?: any) {
        super(type, init);
        this.relatedTarget = init && init.relatedTarget !== undefined ? init.relatedTarget : null;
    }
}

export class InputEvent extends UIEvent {
    readonly data: string | null;
    readonly inputType: string;

    constructor(type: string, init?: any) {
        super(type, init);
        const i = init || {};
        this.data = i.data !== undefined ? String(i.data) : null;
        this.inputType = i.inputType ? String(i.inputType) : "";
    }
}

export class WheelEvent extends MouseEvent {
    readonly deltaX: number;
    readonly deltaY: number;
    readonly deltaMode: number;

    constructor(type: string, init?: any) {
        super(type, init);
        const i = init || {};
        this.deltaX = Number(i.deltaX) || 0;
        this.deltaY = Number(i.deltaY) || 0;
        this.deltaMode = Number(i.deltaMode) || 0;
    }
}
