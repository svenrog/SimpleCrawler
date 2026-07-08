import { WritableStreamDefaultWriter } from "./WritableStreamDefaultWriter";

export class WritableStream {
    _sink: any;
    _writer: any;
    _state: "writable" | "closed" | "errored";
    _storedError: any;
    _closedPromise: Promise<void>;
    _resolveClosed: (() => void) | null;

    constructor(underlyingSink?: any, _strategy?: any) {
        this._sink = underlyingSink || {};
        this._writer = null;
        this._state = "writable";
        this._storedError = undefined;
        this._resolveClosed = null;
        this._closedPromise = new Promise<void>((resolve) => { this._resolveClosed = resolve; });
        this._closedPromise.catch(() => { });
        if (typeof this._sink.start === "function") {
            try {
                this._sink.start(this._controller());
            } catch (e) {
                this._state = "errored";
                this._storedError = e;
            }
        }
    }

    get locked(): boolean {
        return this._writer !== null;
    }

    getWriter(): any {
        if (this._writer) throw new TypeError("WritableStream is locked");
        return new WritableStreamDefaultWriter(this);
    }

    abort(reason?: any): Promise<void> {
        return this._abort(reason);
    }

    close(): Promise<void> {
        return this._close();
    }

    _controller(): any {
        return { error: (e?: any) => { this._state = "errored"; this._storedError = e; } };
    }

    _write(chunk: any): Promise<void> {
        if (this._state === "errored") return Promise.reject(this._storedError);
        try {
            const result = typeof this._sink.write === "function" ? this._sink.write(chunk, this._controller()) : undefined;
            return Promise.resolve(result).then(() => undefined);
        } catch (e) {
            this._state = "errored";
            this._storedError = e;
            return Promise.reject(e);
        }
    }

    _close(): Promise<void> {
        if (this._state === "errored") return Promise.reject(this._storedError);
        if (this._state === "closed") return Promise.resolve();
        this._state = "closed";
        if (this._resolveClosed) this._resolveClosed();
        try {
            const result = typeof this._sink.close === "function" ? this._sink.close() : undefined;
            return Promise.resolve(result).then(() => undefined);
        } catch (e) {
            return Promise.reject(e);
        }
    }

    _abort(reason: any): Promise<void> {
        if (this._state === "errored") return Promise.reject(this._storedError);
        this._state = "errored";
        this._storedError = reason;
        try {
            const result = typeof this._sink.abort === "function" ? this._sink.abort(reason) : undefined;
            return Promise.resolve(result).then(() => undefined);
        } catch (e) {
            return Promise.reject(e);
        }
    }
}
