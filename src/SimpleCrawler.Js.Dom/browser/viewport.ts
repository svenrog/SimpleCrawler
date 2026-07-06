// The headless render reports a desktop viewport (default 1920x1080, overridable from JsRenderOptions.Viewport
// via __crawlerSetViewport) so responsive bundles take their desktop branch — a wider layout typically exposes
// more inline navigation links than the collapsed mobile menu. matchMedia evaluates the dimensional features
// bundles actually branch on against this viewport, instead of the old always-false stub that made every app
// believe it was on a tiny screen.
let _width = 1920;
let _height = 1080;

export function setViewport(width: unknown, height: unknown): void {
    const w = Number(width);
    const h = Number(height);
    if (w > 0) _width = Math.floor(w);
    if (h > 0) _height = Math.floor(h);
}

export function viewportWidth(): number {
    return _width;
}

export function viewportHeight(): number {
    return _height;
}

function numeric(value: string): number {
    const m = /-?\d*\.?\d+/.exec(value);
    return m ? parseFloat(m[0]) : NaN;
}

// devicePixelRatio is fixed at 1, so resolution features compare against 1dppx (96dpi).
function resolutionDppx(value: string): number {
    const n = numeric(value);
    if (isNaN(n)) return NaN;
    if (/dpi/i.test(value)) return n / 96;
    if (/dpcm/i.test(value)) return n / 37.795;
    return n;
}

function matchFeature(name: string, value: string): boolean {
    switch (name) {
        case "min-width": case "min-device-width": return _width >= numeric(value);
        case "max-width": case "max-device-width": return _width <= numeric(value);
        case "width": case "device-width": return _width === numeric(value);
        case "min-height": case "min-device-height": return _height >= numeric(value);
        case "max-height": case "max-device-height": return _height <= numeric(value);
        case "height": case "device-height": return _height === numeric(value);
        case "min-resolution": return 1 >= resolutionDppx(value);
        case "max-resolution": return 1 <= resolutionDppx(value);
        case "resolution": return resolutionDppx(value) === 1;
        case "orientation": return value === "portrait" ? _height > _width : _width >= _height;
        // An unmodelled feature must never veto a layout (the crawl must not hide content), so it matches.
        default: return true;
    }
}

function matchClause(clause: string): boolean {
    const inner = clause.replace(/^\(/, "").replace(/\)$/, "");
    const colon = inner.indexOf(":");
    if (colon < 0) return true;
    const name = inner.slice(0, colon).trim().toLowerCase();
    const value = inner.slice(colon + 1).trim().toLowerCase();
    return matchFeature(name, value);
}

function matchSingle(query: string): boolean {
    let q = query.trim().toLowerCase();
    if (!q) return true;

    let negate = false;
    if (q.indexOf("not ") === 0) {
        negate = true;
        q = q.slice(4).trim();
    }

    const typeMatch = /^(all|screen|print|speech)\b/.exec(q);
    if (typeMatch) {
        const type = typeMatch[1];
        q = q.slice(type.length).trim();
        if (q.indexOf("and") === 0) q = q.slice(3).trim();
        if (type === "print" || type === "speech") return negate;
        if (!q) return !negate;
    }

    const clauses = q.split(/\band\b/).map((c) => c.trim()).filter((c) => c.length > 0);
    const result = clauses.every((c) => matchClause(c));
    return negate ? !result : result;
}

function matches(query: unknown): boolean {
    const list = String(query == null ? "" : query).split(",");
    return list.some((q) => matchSingle(q));
}

export function installViewport(global: any): void {
    const define = (name: string, get: () => any) => Object.defineProperty(global, name, { get, configurable: true });
    define("innerWidth", () => _width);
    define("innerHeight", () => _height);
    define("outerWidth", () => _width);
    define("outerHeight", () => _height);
    global.devicePixelRatio = 1;
    global.screen = {
        get width() { return _width; },
        get height() { return _height; },
        get availWidth() { return _width; },
        get availHeight() { return _height; },
        colorDepth: 24,
        pixelDepth: 24,
        orientation: {
            get type() { return _width >= _height ? "landscape-primary" : "portrait-primary"; },
            angle: 0,
            addEventListener() { },
            removeEventListener() { },
        },
    };
    global.visualViewport = {
        get offsetLeft() { return 0; },
        get offsetTop() { return 0; },
        get pageLeft() { return 0; },
        get pageTop() { return 0; },
        get width() { return _width },
        get height() { return _height },
        get scale() { return 1 },
        onresize: null,
        onscroll: null,
        onscrollend: null,
        addEventListener() { },
        removeEventListener() { },
    };
    global.matchMedia = (query: unknown) => ({
        matches: matches(query),
        media: String(query == null ? "" : query),
        onchange: null,
        addListener() { },
        removeListener() { },
        addEventListener() { },
        removeEventListener() { },
        dispatchEvent() { return false; },
    });
}
