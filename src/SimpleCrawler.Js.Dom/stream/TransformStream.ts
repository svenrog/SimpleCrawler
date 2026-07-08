import { ReadableStream } from "./ReadableStream";
import { WritableStream } from "./WritableStream";

export class TransformStream {
    readable: ReadableStream;
    writable: WritableStream;

    constructor(transformer?: any, _writableStrategy?: any, _readableStrategy?: any) {
        const t = transformer || {};
        let readableController: any;
        this.readable = new ReadableStream({ start: (c: any) => { readableController = c; } });
        const transformController = {
            get desiredSize() { return readableController.desiredSize; },
            enqueue: (chunk?: any) => readableController.enqueue(chunk),
            error: (e?: any) => readableController.error(e),
            terminate: () => readableController.close(),
        };
        this.writable = new WritableStream({
            start: () => typeof t.start === "function" ? t.start(transformController) : undefined,
            write: (chunk: any) => typeof t.transform === "function"
                ? t.transform(chunk, transformController)
                : transformController.enqueue(chunk),
            close: () => Promise.resolve(typeof t.flush === "function" ? t.flush(transformController) : undefined)
                .then(() => readableController.close()),
            abort: (reason: any) => readableController.error(reason),
        });
    }
}
