import { ReadableStream } from "./ReadableStream";
import { ReadableStreamDefaultController } from "./ReadableStreamDefaultController";
import { ReadableStreamDefaultReader } from "./ReadableStreamDefaultReader";
import { WritableStream } from "./WritableStream";
import { WritableStreamDefaultWriter } from "./WritableStreamDefaultWriter";
import { TransformStream } from "./TransformStream";
import { TextDecoderStream } from "./TextDecoderStream";
import { TextEncoderStream } from "./TextEncoderStream";
import { ByteLengthQueuingStrategy, CountQueuingStrategy } from "./QueuingStrategy";
import { markPrototypeNative } from "../browser/native";

// Opt-in only: JsRenderOptions.EnableStreams. A WHATWG Streams shim over the synchronous drain.
// Bodies are buffered-complete (fetch already materializes the whole response), so this delivers
// spec-compliant reader/transform semantics, not incremental transport streaming.
export function installStreams(global: any): void {
    const ctors: Record<string, any> = {
        ReadableStream,
        ReadableStreamDefaultController,
        ReadableStreamDefaultReader,
        WritableStream,
        WritableStreamDefaultWriter,
        TransformStream,
        TextDecoderStream,
        TextEncoderStream,
        ByteLengthQueuingStrategy,
        CountQueuingStrategy,
    };
    for (const name in ctors) {
        markPrototypeNative(ctors[name]);
        global[name] = global[name] || ctors[name];
    }
}

installStreams(globalThis as any);
