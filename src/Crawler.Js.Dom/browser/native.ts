// jQuery/Sizzle (and Zepto, and many libs) feature-detect real browser methods with
// /^[^{]+\{\s*\[native code/.test(fn) — i.e. they stringify the method and look for "[native code]". Our host
// DOM methods are ordinary JS functions, so that probe fails and Sizzle abandons its fast native-selection
// paths (support.qsa / getElementsByClassName / matchesSelector all come out false), falling back to a slow,
// less-tested manual matcher. Giving each host method a native-looking toString lets those probes pass so
// jQuery drives our real querySelectorAll/getElementsByClassName/matches instead.

function markNative(fn: any, name: string): void {
    const label = "function " + name + "() { [native code] }";
    try {
        Object.defineProperty(fn, "toString", {
            value: function () { return label; },
            writable: true,
            configurable: true,
            enumerable: false,
        });
    } catch { /* frozen/sealed host function — leave as-is */ }
}

export function markPrototypeNative(ctor: any): void {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    for (const key of Object.getOwnPropertyNames(proto)) {
        if (key === "constructor") continue;
        const desc = Object.getOwnPropertyDescriptor(proto, key);
        if (desc && typeof desc.value === "function") markNative(desc.value, key);
    }
}
