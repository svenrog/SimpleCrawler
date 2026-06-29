export class TextEncoder {
    readonly encoding = "utf-8";

    encode(input?: any): Uint8Array {
        const s = input == null ? "" : String(input);
        const out: number[] = [];
        for (let i = 0; i < s.length;) {
            const c = s.charCodeAt(i++);
            if (c < 0x80) {
                out.push(c);
            } else if (c < 0x800) {
                out.push(0xc0 | (c >> 6), 0x80 | (c & 0x3f));
            } else if (c >= 0xd800 && c <= 0xdbff && i < s.length) {
                const c2 = s.charCodeAt(i++);
                const cp = 0x10000 + ((c & 0x3ff) << 10) + (c2 & 0x3ff);
                out.push(0xf0 | (cp >> 18), 0x80 | ((cp >> 12) & 0x3f), 0x80 | ((cp >> 6) & 0x3f), 0x80 | (cp & 0x3f));
            } else {
                out.push(0xe0 | (c >> 12), 0x80 | ((c >> 6) & 0x3f), 0x80 | (c & 0x3f));
            }
        }
        return new Uint8Array(out);
    }
}
