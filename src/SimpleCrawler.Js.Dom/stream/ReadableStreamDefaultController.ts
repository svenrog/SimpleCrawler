export class ReadableStreamDefaultController {
    _stream: any;

    constructor(stream: any) {
        this._stream = stream;
    }

    get desiredSize(): number | null {
        const s = this._stream;
        if (s._state === "errored") return null;
        if (s._state === "closed") return 0;
        return 1 - s._queue.length;
    }

    enqueue(chunk?: any): void {
        const s = this._stream;
        if (s._state !== "readable") return;
        const pending = s._readRequests.shift();
        if (pending) pending.resolve({ value: chunk, done: false });
        else s._queue.push(chunk);
    }

    close(): void {
        const s = this._stream;
        if (s._state !== "readable") return;
        if (s._queue.length === 0) s._closeInternal();
        else s._closeRequested = true;
    }

    error(e?: any): void {
        this._stream._errorInternal(e);
    }
}
