// Console prelude for AngleSharp JS rendering
// This shims console.error to work around Next.js's console modification
globalThis.console = globalThis.console || {
    log: function () { return _console.log(...arguments); },
    info: function () { return _console.info(...arguments); },
    warn: function () { return _console.warn(...arguments); },
    error: function () { return _console.error(...arguments); },
    debug: function () { return _console.debug(...arguments); },
    trace: function () { return _console.trace(...arguments); }, 
    dir: function () { return _console.dir(...arguments); },
    dirxml: function () { return _console.dirxml(...arguments); },
    group: function () { return _console.group(...arguments); },
    groupCollapsed: function () { return _console.groupCollapsed(...arguments); },
    groupEnd: function () { return _console.groupEnd(...arguments); }, 
    table: function () { return _console.table(...arguments); }, 
    assert: function () { return _console.assert(...arguments); }, 
    count: function () { return _console.count(...arguments); },
    countReset: function () { return _console.countReset(...arguments); },
    time: function () { return _console.time(...arguments); },
    timeEnd: function () { return _console.timeEnd(...arguments); },
    timeLog: function () { return _console.timeLog(...arguments); }, 
    clear: function () { return _console.clear(...arguments); },
};