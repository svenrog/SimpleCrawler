import { indexOfCI } from "./entities";

export interface TagScanners {
    tagName: RegExp;
    ws: RegExp;
    attrName: RegExp;
    bareVal: RegExp;
}

export function createTagScanners(): TagScanners {
    return {
        tagName: /[a-zA-Z][a-zA-Z0-9:_-]*/y,
        ws: /[\t\n\f\r ]+/y,
        attrName: /[^\t\n\f\r \/>"'<=]+/y,
        bareVal: /[^\t\n\f\r >]*/y,
    };
}

export function findRawTextClose(input: string, tag: string, from: number): number {
    return indexOfCI(input, "</" + tag, from);
}
