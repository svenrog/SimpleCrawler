export const navigator = {
    userAgent: "SimpleCrawler",
    // The fallback arm of the quirksmode-descended sniffer chat and consent widgets still ship:
    // `searchVersion(navigator.userAgent) || searchVersion(navigator.appVersion)`, where each arm indexes the
    // string it is handed. The second always runs here — userAgent carries no version for the first to find —
    // so undefined is `undefined.indexOf(…)`, a TypeError aborting the whole chunk. Same string as userAgent:
    // a sniffer matching on one and not the other is reading two different browsers.
    appVersion: "SimpleCrawler",
    platform: "",
    // Read bare and immediately indexed (`for (i = 0, n = navigator.plugins.length; …)`) by those same
    // detectors, so absence is a TypeError where an empty list is a path they handle — and empty is the
    // truthful answer, this render loads no plugins.
    plugins: [] as { name: string }[],
    language: "en",
    geolocation: {
        getCurrentPosition() { },
        watchPosition() { return 0; },
        clearWatch() { },
    },
    // The beacon an analytics bundle sends on its way out. Reporting success is the point: this render
    // installs no fetch/XHR by default precisely so such a bundle runs and sets its globals while its beacon
    // goes nowhere, and sendBeacon was the one exit that threw instead of quietly no-opping. Returning false
    // would invite the documented fallback — re-send over XHR — which is the path we are avoiding.
    sendBeacon(): boolean { return true; },
    // declined: connection — and the measurement is the reason, not an oversight. Unlike sendBeacon above,
    // every observed read is *guarded* (`navigator.connection && …`), so its absence is already a path real
    // pages take deliberately; supplying a stub instead diverts them onto their adaptive branch on the
    // strength of a connection we invented, and it recovered no global on any sampled target. A shim whose
    // only measured effect is to change which branch a page takes is surface this cannot justify.
    // declined: serviceWorker — same shape. Feature-detected via `"serviceWorker" in navigator`, so absence
    // is the clean, handled path, and no sampled target read it at all. Revisit if a target is shown losing a
    // global to either.
};
