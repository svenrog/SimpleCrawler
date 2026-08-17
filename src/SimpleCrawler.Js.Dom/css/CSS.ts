// The CSS namespace object. A namespace, not a constructor: page code reads CSS.escape/CSS.supports off it
// bare, so its absence is a ReferenceError inside whatever bundle probes for a modern stylesheet feature.

// https://drafts.csswg.org/cssom/#serialize-an-identifier — the result is pasted into a selector, so an
// approximation here is a query that silently matches nothing rather than a visibly wrong answer.
function escape(value: string): string {
    const input = String(value);
    const out: string[] = [];
    const first = input.charCodeAt(0);

    for (let i = 0; i < input.length; i++) {
        const code = input.charCodeAt(i);

        // NULL is replaced rather than escaped, per the serialization algorithm.
        if (code === 0) {
            out.push("�");
            continue;
        }

        if ((code >= 0x0001 && code <= 0x001f)
            || code === 0x007f
            || (i === 0 && code >= 0x0030 && code <= 0x0039)
            || (i === 1 && code >= 0x0030 && code <= 0x0039 && first === 0x002d)) {
            out.push("\\" + code.toString(16) + " ");
            continue;
        }

        // A lone leading hyphen is an identifier a parser cannot read back, so it is escaped whole.
        if (i === 0 && code === 0x002d && input.length === 1) {
            out.push("\\" + input.charAt(i));
            continue;
        }

        if (code >= 0x0080
            || code === 0x002d
            || code === 0x005f
            || (code >= 0x0030 && code <= 0x0039)
            || (code >= 0x0041 && code <= 0x005a)
            || (code >= 0x0061 && code <= 0x007a)) {
            out.push(input.charAt(i));
            continue;
        }

        out.push("\\" + input.charAt(i));
    }

    return out.join("");
}

// Feature detection, answered as the current browser this render presents itself as: a well-formed query is
// supported. The render resolves no styles, so nothing here can be tested for real — and the alternative,
// declining everything, is what sends a bundle down a polyfill path written for browsers a decade older
// than the DOM it would then patch. Only an empty query is false, which is what the spec says of one that
// does not parse.
function supports(conditionOrProperty: string, value?: string): boolean {
    if (value === undefined) return String(conditionOrProperty).trim().length > 0;
    return String(conditionOrProperty).trim().length > 0 && String(value).trim().length > 0;
}

export const CSS = {
    escape,
    supports,
    // Houdini's custom-property registration. A page registers at init and reads nothing back, so recording
    // nothing is enough for init to survive; the render has no cascade for a registration to reach.
    registerProperty(): void { },
    // declined: CSS.px and the rest of the numeric factories (Typed OM), CSS.highlights. Neither was observed
    // on a target, and both return live objects whose arithmetic a caller would then trust.
};
