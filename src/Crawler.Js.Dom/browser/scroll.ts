export function installScrollApi(global: any):void {
    global.scrollTo = () => { };
    global.scrollBy = () => { };
    global.scrollByLines = () => { };
    global.scrollByPages = () => { };
}