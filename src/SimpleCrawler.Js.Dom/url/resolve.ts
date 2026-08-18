function currentLocation(): any {
    return (globalThis as any).location;
}

export function resolveUrl(u: unknown, base?: string): string {
    const input = String(u ?? "");
    if (/^[a-zA-Z][\w+.-]*:\/\//.test(input)) return input;
    const b = String(base || currentLocation()?.href || "http://localhost/");
    const bm = b.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)/) || [];
    const scheme = bm[1] || "http:";
    const origin = bm[2] ? scheme + "//" + bm[2] : "http://localhost";
    // A scheme-relative reference carries its own authority and only borrows the base's scheme. Read as a
    // path it becomes an origin-relative one whose first segment is a hostname, so the request goes to the
    // page's own server and 404s — and a loader that builds its CDN prefix as "//" + host is a common shape.
    if (input.slice(0, 2) === "//") return scheme + input;
    if (input.charAt(0) === "/") return origin + input;
    if (input.charAt(0) === "#" || input.charAt(0) === "?") return origin + (bm[3] || "/") + input;
    const dir = (bm[3] || "/").replace(/[^/]*$/, "");
    return origin + dir + input;
}

export function applyUrl(u: string): void {
    try {
        const abs = resolveUrl(u);
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
