// Console prelude for AngleSharp JS rendering
// This shims console.error to work around Next.js's console modification
globalThis.console = globalThis.console || {
    log: function () { return __console.log(...arguments); },
    info: function () { return __console.info(...arguments); },
    warn: function () { return __console.warn(...arguments); },
    error: function () { return __console.error(...arguments); },
    debug: function () { return __console.debug(...arguments); },
    trace: function () { return __console.trace(...arguments); }, 
    dir: function () { return __console.dir(...arguments); },
    dirxml: function () { return __console.dirxml(...arguments); },
    group: function () { return __console.group(...arguments); },
    groupCollapsed: function () { return __console.groupCollapsed(...arguments); },
    groupEnd: function () { return __console.groupEnd(...arguments); }, 
    table: function () { return __console.table(...arguments); }, 
    assert: function () { return __console.assert(...arguments); }, 
    count: function () { return __console.count(...arguments); },
    countReset: function () { return __console.countReset(...arguments); },
    time: function () { return __console.time(...arguments); },
    timeEnd: function () { return __console.timeEnd(...arguments); },
    timeLog: function () { return __console.timeLog(...arguments); }, 
    clear: function () { return __console.clear(...arguments); },
};