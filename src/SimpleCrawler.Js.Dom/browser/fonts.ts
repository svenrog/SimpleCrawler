// document.fonts — a FontFaceSet that never loads anything. Nothing is fetched in a layout-less render, so
// the set stays empty, but it must be iterable and `ready` must resolve: a chat widget enumerates the
// families it may use with `document.fonts.ready.then(set => Array.from(set))` during its own init, so an
// absent `fonts` is a throw there rather than a skipped enumeration. A real Set carries the iteration,
// forEach and size a caller then reads.
export function createFontFaceSet(): any {
    const set = new Set() as any;
    set.ready = Promise.resolve(set);
    set.status = "loaded";
    set.check = () => true;
    set.load = () => Promise.resolve([]);
    set.addEventListener = () => { };
    set.removeEventListener = () => { };
    set.onloading = null;
    set.onloadingdone = null;
    set.onloadingerror = null;
    return set;
}
