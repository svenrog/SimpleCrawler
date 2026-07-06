const NAMED: Record<string, string> = {
    amp: "&", lt: "<", gt: ">", quot: '"', apos: "'", nbsp: " ", copy: "©", reg: "®",
    trade: "™", hellip: "…", mdash: "—", ndash: "–", lsquo: "‘", rsquo: "’",
    ldquo: "“", rdquo: "”", laquo: "«", raquo: "»", deg: "°", plusmn: "±",
    times: "×", divide: "÷", micro: "µ", euro: "€", pound: "£", cent: "¢", yen: "¥",
    sect: "§", para: "¶", middot: "·", bull: "•", frac12: "½", frac14: "¼",
    frac34: "¾", sup2: "²", sup3: "³",
};

export function decodeEntities(s: string): string {
    if (s.indexOf("&") < 0) return s;
    return s.replace(/&#(x?[0-9a-fA-F]+);|&([a-zA-Z][a-zA-Z0-9]*);/g, (m, num, name) => {
        if (num != null) {
            const code = num.charAt(0) === "x" || num.charAt(0) === "X"
                ? parseInt(num.slice(1), 16)
                : parseInt(num, 10);
            return code > 0 && isFinite(code) ? String.fromCharCode(code) : m;
        }
        return Object.prototype.hasOwnProperty.call(NAMED, name) ? NAMED[name] : m;
    });
}

export function indexOfCI(haystack: string, needle: string, from: number): number {
    const n = needle.length, hl = haystack.length;
    for (let p = from; p <= hl - n; p++) {
        for (let q = 0; q < n; q++) {
            const c = haystack.charAt(p + q);
            if (c !== needle.charAt(q) && c.toLowerCase() !== needle.charAt(q)) break;
            if (q === n - 1) return p;
        }
    }
    return -1;
}
