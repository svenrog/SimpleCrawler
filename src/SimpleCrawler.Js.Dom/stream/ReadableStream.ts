import { ReadableStreamDefaultController } from "./ReadableStreamDefaultController";
import { ReadableStreamDefaultReader } from "./ReadableStreamDefaultReader";

interface ReadRequest {
    resolve: (result: any) => void;
    reject: (error: any) => void;
}

// A pull-based readable stream over the synchronous drain: read() returns a Promise that settles either
// immediately (a queued chunk) or when the source next enqueues. There is no real backpressure or timing
// since the whole render collapses wall-clock time; the source is expected to deliver a bounded body.
export class ReadableStream {
    _state: "readable" | "closed" | "errored";
    _storedError: any;
    _queue: any[];
    _closeRequested: boolean;
    _readRequests: ReadRequest[];
    _controller: ReadableStreamDefaultController;
    _reader: any;
    _pull: ((controller: any) => any) | null;
    _cancelAlgorithm: ((reason: any) => any) | null;
    _pulling: boolean;

    constructor(underlyingSource?: any, _strategy?: any) {
        const source = underlyingSource || {};
        this._state = "readable";
        this._storedError = undefined;
        this._queue = [];
        this._closeRequested = false;
        this._readRequests = [];
        this._reader = null;
        this._pulling = false;
        this._pull = typeof source.pull === "function" ? source.pull.bind(source) : null;
        this._cancelAlgorithm = typeof source.cancel === "function" ? source.cancel.bind(source) : null;
        this._controller = new ReadableStreamDefaultController(this);
        if (typeof source.start === "function") {
            try {
                const started = source.start.call(source, this._controller);
                if (started && typeof started.then === "function") started.then(undefined, (e: any) => this._errorInternal(e));
            } catch (e) {
                this._errorInternal(e);
            }
        }
    }

    get locked(): boolean {
        return this._reader !== null;
    }

    getReader(options?: any): any {
        if (options && options.mode === "byob") throw new TypeError("byob readers are not supported");
        return new ReadableStreamDefaultReader(this);
    }

    cancel(reason?: any): Promise<void> {
        if (this.locked) return Promise.reject(new TypeError("Cannot cancel a locked stream"));
        return this._cancel(reason);
    }

    pipeTo(destination: any, _options?: any): Promise<void> {
        const reader = this.getReader();
        const writer = destination.getWriter();
        return new Promise<void>((resolve, reject) => {
            const step = () => {
                reader.read().then((result: any) => {
                    if (result.done) {
                        Promise.resolve(writer.close()).then(() => { reader.releaseLock(); resolve(); }, reject);
                        return;
                    }
                    Promise.resolve(writer.write(result.value)).then(step, reject);
                }, reject);
            };
            step();
        });
    }

    pipeThrough(transform: any, options?: any): any {
        this.pipeTo(transform.writable, options).catch(() => { });
        return transform.readable;
    }

    tee(): [ReadableStream, ReadableStream] {
        const reader = this.getReader();
        const controllers: any[] = [];
        let reading = false;
        let ended = false;
        const pump = (): any => {
            if (ended || reading) return;
            reading = true;
            return reader.read().then((result: any) => {
                reading = false;
                if (result.done) {
                    ended = true;
                    for (const c of controllers) c.close();
                    return;
                }
                for (const c of controllers) c.enqueue(result.value);
            }, (e: any) => {
                for (const c of controllers) c.error(e);
            });
        };
        const branch = () => new ReadableStream({
            start: (c: any) => { controllers.push(c); },
            pull: () => pump(),
            cancel: (reason: any) => reader.cancel(reason),
        });
        return [branch(), branch()];
    }

    [Symbol.asyncIterator](): any {
        const reader = this.getReader();
        return {
            next() {
                return reader.read().then((result: any) => result.done
                    ? { value: undefined, done: true }
                    : { value: result.value, done: false });
            },
            return(value?: any) {
                reader.releaseLock();
                return Promise.resolve({ value, done: true });
            },
            [Symbol.asyncIterator]() { return this; },
        };
    }

    _readChunk(): Promise<any> {
        if (this._state === "errored") return Promise.reject(this._storedError);
        if (this._queue.length > 0) {
            const chunk = this._queue.shift();
            if (this._queue.length === 0 && this._closeRequested) this._closeInternal();
            else this._pullIfNeeded();
            return Promise.resolve({ value: chunk, done: false });
        }
        if (this._state === "closed") return Promise.resolve({ value: undefined, done: true });
        const pending = new Promise<any>((resolve, reject) => { this._readRequests.push({ resolve, reject }); });
        this._pullIfNeeded();
        return pending;
    }

    _pullIfNeeded(): void {
        if (!this._pull || this._pulling || this._state !== "readable") return;
        this._pulling = true;
        try {
            const pulled = this._pull(this._controller);
            if (pulled && typeof pulled.then === "function") {
                pulled.then(() => { this._pulling = false; }, (e: any) => { this._pulling = false; this._errorInternal(e); });
            } else {
                this._pulling = false;
            }
        } catch (e) {
            this._pulling = false;
            this._errorInternal(e);
        }
    }

    _closeInternal(): void {
        if (this._state !== "readable") return;
        this._state = "closed";
        for (const request of this._readRequests.splice(0)) request.resolve({ value: undefined, done: true });
        if (this._reader) this._reader.settleClosed();
    }

    _errorInternal(e: any): void {
        if (this._state !== "readable") return;
        this._state = "errored";
        this._storedError = e;
        this._queue = [];
        for (const request of this._readRequests.splice(0)) request.reject(e);
        if (this._reader) this._reader.settleErrored(e);
    }

    _cancel(reason?: any): Promise<void> {
        this._queue = [];
        this._closeInternal();
        let result: any;
        try {
            result = this._cancelAlgorithm ? this._cancelAlgorithm(reason) : undefined;
        } catch (e) {
            return Promise.reject(e);
        }
        return Promise.resolve(result).then(() => undefined);
    }
}
