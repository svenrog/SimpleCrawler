export function installScrollApi(global: any):void {
    global.scrollTo = () => { };
    global.scrollBy = () => { };
    global.scrollByLines = () => { };
    global.scrollByPages = () => { };
    // The single-pass render never scrolls, so the read-only scroll offsets are always at the origin.
    global.scrollX = 0;
    global.scrollY = 0;
    global.pageXOffset = 0;
    global.pageYOffset = 0;
}