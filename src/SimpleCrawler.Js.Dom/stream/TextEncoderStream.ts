import { TransformStream } from "./TransformStream";
import { TextEncoder } from "../browser/TextEncoder";

export class TextEncoderStream extends TransformStream {
    readonly encoding = "utf-8";

    constructor() {
        const encoder = new TextEncoder();
        super({
            transform(chunk: any, controller: any) {
                controller.enqueue(encoder.encode(chunk == null ? "" : String(chunk)));
            },
        });
    }
}
