export class TextDecoder {
    readonly encoding = "utf-8";

    decode(input?: any): string {
        if (input == null) return "";
        if (typeof input === "string") return input;
        const bytes = input as ArrayLike<number>;
        let out = "";
        let i = 0;
        const len = bytes.length;
        while (i < len) {
            const b1 = bytes[i++];
            if (b1 < 0x80) {
                out += String.fromCharCode(b1);
            } else if (b1 < 0xe0) {
                const b2 = bytes[i++];
                out += String.fromCharCode(((b1 & 0x1f) << 6) | (b2 & 0x3f));
            } else if (b1 < 0xf0) {
                const b2 = bytes[i++];
                const b3 = bytes[i++];
                out += String.fromCharCode(((b1 & 0x0f) << 12) | ((b2 & 0x3f) << 6) | (b3 & 0x3f));
            } else {
                const b2 = bytes[i++];
                const b3 = bytes[i++];
                const b4 = bytes[i++];
                const cp = ((b1 & 0x07) << 18) | ((b2 & 0x3f) << 12) | ((b3 & 0x3f) << 6) | (b4 & 0x3f);
                const adj = cp - 0x10000;
                out += String.fromCharCode(0xd800 | (adj >> 10), 0xdc00 | (adj & 0x3f));
            }
        }
        return out;
    }
}
