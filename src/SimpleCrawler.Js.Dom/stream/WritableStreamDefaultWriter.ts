export class WritableStreamDefaultWriter {
    _stream: any;

    constructor(stream: any) {
        this._stream = stream;
        stream._writer = this;
    }

    get desiredSize(): number | null {
        return this._stream && this._stream._state === "writable" ? 1 : 0;
    }

    get ready(): Promise<void> {
        return Promise.resolve();
    }

    get closed(): Promise<void> {
        return this._stream ? this._stream._closedPromise : Promise.resolve();
    }

    write(chunk?: any): Promise<void> {
        if (!this._stream) return Promise.reject(new TypeError("Writer has been released"));
        return this._stream._write(chunk);
    }

    close(): Promise<void> {
        if (!this._stream) return Promise.reject(new TypeError("Writer has been released"));
        return this._stream._close();
    }

    abort(reason?: any): Promise<void> {
        if (!this._stream) return Promise.resolve();
        return this._stream._abort(reason);
    }

    releaseLock(): void {
        if (!this._stream) return;
        this._stream._writer = null;
        this._stream = null;
    }
}
