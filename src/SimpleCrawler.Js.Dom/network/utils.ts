export function toHeaderObject(h: any): any {
    const out: any = {};
    if (!h) return out;
    if (typeof h.forEach === "function" && !Array.isArray(h)) { h.forEach((v: any, k: string) => { out[k] = v; }); return out; }
    if (Array.isArray(h)) { for (let i = 0; i < h.length; i++) { out[h[i][0]] = h[i][1]; } return out; }
    for (const k in h) { if (Object.prototype.hasOwnProperty.call(h, k)) out[k] = h[k]; }
    return out;
}
