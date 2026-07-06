const _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

export function btoa(input?: any): string {
    const s = input == null ? "" : String(input);
    let out = "";
    for (let i = 0; i < s.length;) {
        const c1 = s.charCodeAt(i++);
        if (c1 > 0xff) throw new Error("The string to be encoded contains characters outside of the Latin1 range.");
        const c2 = i < s.length ? s.charCodeAt(i++) : NaN;
        const c3 = i < s.length ? s.charCodeAt(i++) : NaN;
        if ((c2 > 0xff) || (c3 > 0xff)) throw new Error("The string to be encoded contains characters outside of the Latin1 range.");
        const e1 = c1 >> 2;
        const e2 = ((c1 & 3) << 4) | (isNaN(c2) ? 0 : c2 >> 4);
        const e3 = isNaN(c2) ? 64 : (((c2 & 15) << 2) | (isNaN(c3) ? 0 : c3 >> 6));
        const e4 = isNaN(c3) ? 64 : (c3 & 63);
        out += _chars[e1] + _chars[e2] + (e3 === 64 ? "=" : _chars[e3]) + (e4 === 64 ? "=" : _chars[e4]);
    }
    return out;
}

export function atob(input?: any): string {
    const s = (input == null ? "" : String(input)).replace(/[\t\n\f\r ]/g, "");
    if (s.length % 4 === 1) throw new Error("The string to be decoded is not correctly encoded.");
    let out = "";
    let bits = 0;
    let count = 0;
    for (let i = 0; i < s.length; i++) {
        const ch = s[i];
        if (ch === "=") break;
        const v = _chars.indexOf(ch);
        if (v < 0) throw new Error("The string to be decoded is not correctly encoded.");
        bits = (bits << 6) | v;
        count += 6;
        if (count >= 8) {
            count -= 8;
            out += String.fromCharCode((bits >> count) & 0xff);
        }
    }
    return out;
}
