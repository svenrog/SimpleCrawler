import { TransformStream } from "./TransformStream";
import { TextDecoder } from "../browser/TextDecoder";

export class TextDecoderStream extends TransformStream {
    readonly encoding = "utf-8";

    constructor(_label?: any, _options?: any) {
        const decoder = new TextDecoder();
        super({
            transform(chunk: any, controller: any) {
                const text = decoder.decode(chunk);
                if (text) controller.enqueue(text);
            },
        });
    }
}
