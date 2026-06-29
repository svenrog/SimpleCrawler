import { applyUrl } from "../url/resolve";

export function createHistory(): any {
    return {
        pushState: (_s: unknown, _t: unknown, u?: string) => { if (u) applyUrl(u); },
        replaceState: (_s: unknown, _t: unknown, u?: string) => { if (u) applyUrl(u); },
        go: () => { },
        back: () => { },
        forward: () => { },
        length: 1,
        state: null,
    };
}
