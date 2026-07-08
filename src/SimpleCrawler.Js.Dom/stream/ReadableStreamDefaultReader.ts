export class ReadableStreamDefaultReader {
    _stream: any;
    _closedPromise: Promise<void>;
    _resolveClosed: (() => void) | null;
    _rejectClosed: ((e: any) => void) | null;

    constructor(stream: any) {
        if (!stream || stream._reader) throw new TypeError("ReadableStream is locked or invalid");
        this._stream = stream;
        stream._reader = this;
        this._resolveClosed = null;
        this._rejectClosed = null;
        this._closedPromise = new Promise<void>((resolve, reject) => {
            this._resolveClosed = resolve;
            this._rejectClosed = reject;
        });
        // Swallow the closed-promise rejection unless the consumer opts in, so a stream that
        // errors without anyone awaiting reader.closed doesn't surface as an unhandled rejection.
        this._closedPromise.catch(() => { });
        if (stream._state === "closed") this.settleClosed();
        else if (stream._state === "errored") this.settleErrored(stream._storedError);
    }

    get closed(): Promise<void> {
        return this._closedPromise;
    }

    read(): Promise<any> {
        if (!this._stream) return Promise.reject(new TypeError("Reader has been released"));
        return this._stream._readChunk();
    }

    cancel(reason?: any): Promise<void> {
        if (!this._stream) return Promise.resolve();
        return this._stream._cancel(reason);
    }

    releaseLock(): void {
        if (!this._stream) return;
        this._stream._reader = null;
        this._stream = null;
    }

    settleClosed(): void {
        if (this._resolveClosed) this._resolveClosed();
        this._resolveClosed = null;
        this._rejectClosed = null;
    }

    settleErrored(e: any): void {
        if (this._rejectClosed) this._rejectClosed(e);
        this._resolveClosed = null;
        this._rejectClosed = null;
    }
}
