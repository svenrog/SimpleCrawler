export const navigator = {
    userAgent: "SimpleCrawler",
    platform: "",
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
