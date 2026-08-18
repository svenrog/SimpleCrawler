// The window's own interface object. The realm's global is the engine's, not an instance of anything we
// declare, so the constructor can't be the global's real prototype — but every use a page makes of the name
// is either a bare reference (`typeof Window`, a `Window.prototype` patch) or the identity test
// `x instanceof Window`, and instanceof is answerable exactly: in a top-level browsing context with no
// frames, the only window is this global. Constructing one is illegal in a browser and stays illegal here.
export class Window {
    constructor() {
        throw new TypeError("Illegal constructor");
    }
}

Object.defineProperty(Window, Symbol.hasInstance, {
    value: (value: unknown) => value === (globalThis as any),
});
