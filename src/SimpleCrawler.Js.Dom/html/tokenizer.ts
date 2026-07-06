import { indexOfCI } from "./entities";

// Hand-rolled character-code scanners for the string HTML parser. These replace sticky regexes: the
// patterns were pure ASCII character classes, so a charCodeAt loop is exactly equivalent while skipping
// the regex engine (the dominant per-token cost). Char codes: tab 9, LF 10, FF 12, CR 13, space 32,
// '-' 45, '/' 47, '0'-'9' 48-57, ':' 58, '=' 61, '>' 62, 'A'-'Z' 65-90, '<' 60, '"' 34, "'" 39, '_' 95,
// 'a'-'z' 97-122.

// HTML whitespace per spec: tab, LF, FF, CR, space (matches the old /[\t\n\f\r ]/ class).
export function isHtmlSpace(c: number): boolean {
    return c === 32 || c === 9 || c === 10 || c === 13 || c === 12;
}

export function skipSpace(src: string, i: number, len: number): number {
    while (i < len && isHtmlSpace(src.charCodeAt(i))) i++;
    return i;
}

export function isAlpha(c: number): boolean {
    return (c >= 65 && c <= 90) || (c >= 97 && c <= 122);
}

// A tag name is [a-zA-Z][a-zA-Z0-9:_-]*; returns the index past the name, or -1 when `start` is not a
// name-start character (the old sticky regex returned null in that case).
export function matchTagName(src: string, start: number, len: number): number {
    if (start >= len || !isAlpha(src.charCodeAt(start))) return -1;
    let i = start + 1;
    while (i < len) {
        const c = src.charCodeAt(i);
        if ((c >= 65 && c <= 90) || (c >= 97 && c <= 122) || (c >= 48 && c <= 57) || c === 45 || c === 58 || c === 95) i++;
        else break;
    }
    return i;
}

// An attribute name runs until whitespace or one of / > " ' < = (old /[^\t\n\f\r \/>"'<=]+/). Returns the
// end index, which equals `start` when the character there cannot start a name.
export function scanAttrName(src: string, i: number, len: number): number {
    while (i < len) {
        const c = src.charCodeAt(i);
        if (isHtmlSpace(c) || c === 47 || c === 62 || c === 34 || c === 39 || c === 60 || c === 61) break;
        i++;
    }
    return i;
}

// An unquoted attribute value runs until whitespace or > (old /[^\t\n\f\r >]*/).
export function scanBareValue(src: string, i: number, len: number): number {
    while (i < len) {
        const c = src.charCodeAt(i);
        if (isHtmlSpace(c) || c === 62) break;
        i++;
    }
    return i;
}

export function findRawTextClose(input: string, tag: string, from: number): number {
    return indexOfCI(input, "</" + tag, from);
}
