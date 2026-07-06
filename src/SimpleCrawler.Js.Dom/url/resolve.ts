function currentLocation(): any {
    return (globalThis as any).location;
}

export function resolveUrl(u: unknown, base?: string): string {
    const input = String(u ?? "");
    if (/^[a-zA-Z][\w+.-]*:\/\//.test(input)) return input;
    const b = String(base || currentLocation()?.href || "http://localhost/");
    const bm = b.match(/^([a-zA-Z][\w+.-]*:\/\/[^/?#]*)([^?#]*)/) || [];
    const origin = bm[1] || "http://localhost";
    if (input.charAt(0) === "/") return origin + input;
    if (input.charAt(0) === "#" || input.charAt(0) === "?") return origin + (bm[2] || "/") + input;
    const dir = (bm[2] || "/").replace(/[^/]*$/, "");
    return origin + dir + input;
}

export function applyUrl(u: string): void {
    try {
        let abs = u;
        if (u.indexOf("http") !== 0) {
            const base = currentLocation()?.origin || "http://localhost";
            abs = u.charAt(0) === "/" ? base + u : base + "/" + u;
        }
        const m = abs.match(/^(https?:)\/\/([^/?#]+)([^?#]*)(\?[^#]*)?(#.*)?$/);
        if (!m) return;
        const loc = currentLocation();
        if (!loc) return;
        loc.href = abs;
        loc.protocol = m[1];
        loc.host = m[2];
        loc.hostname = m[2].split(":")[0];
        loc.port = m[2].split(":")[1] || "";
        loc.pathname = m[3] || "/";
        loc.search = m[4] || "";
        loc.hash = m[5] || "";
        loc.origin = m[1] + "//" + m[2];
    } catch { /* a malformed history URL must not abort rendering */ }
}
