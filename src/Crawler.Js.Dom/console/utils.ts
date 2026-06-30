export function formatArgs(args: any[]): string {
    if (args.length === 0) return "";
    if (args.length === 1) return stringify(args[0]);

    const fmt = stringify(args[0]);
    if (fmt.indexOf("%") < 0) return args.map(stringify).join(" ");

    let out = "";
    let argIdx = 1;
    let i = 0;
    while (i < fmt.length) {
        if (fmt[i] === "%" && i + 1 < fmt.length && argIdx < args.length) {
            const spec = fmt[i + 1];
            if (spec === "s" || spec === "o" || spec === "O") { out += stringify(args[argIdx++]); i += 2; continue; }
            if (spec === "d" || spec === "i") { out += toInt(args[argIdx++]); i += 2; continue; }
            if (spec === "f") { out += toFloat(args[argIdx++]); i += 2; continue; }
            if (spec === "%") { out += "%"; i += 2; continue; }
        }
        out += fmt[i++];
    }
    while (argIdx < args.length) out += " " + stringify(args[argIdx++]);
    return out;
}

export function stringify(value: any): string {
    if (value === null) return "null";
    if (value === undefined) return "undefined";
    const type = typeof value;
    if (type === "string") return value;
    if (type === "number" || type === "boolean" || type === "bigint") return String(value);
    if (type === "function" || type === "symbol") return String(value);
    try { return JSON.stringify(value) ?? String(value); } catch { return String(value); }
}

export function toInt(value: any): number {
    const n = Number(value);
    return Number.isFinite(n) ? Math.trunc(n) : 0;
}

export function toFloat(value: any): number {
    const n = Number(value);
    return Number.isFinite(n) ? n : 0;
}
