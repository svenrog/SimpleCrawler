import { HTMLElement } from "./HTMLElement";

// A <video>/<audio> can't play in a layout-less DOM, but components mount them by calling load()/play()/
// pause() synchronously (e.g. a video ref's effect does `el.onloadeddata = fn; el.load()`), so the methods
// must exist or the effect throws and trips the SPA error boundary. play() hands back a resolved promise
// because the spec-typed `el.play().catch(...)` autoplay-guard idiom awaits it.
export class HTMLMediaElement extends HTMLElement {
    get currentTime(): number {
        return 0;
    }

    set currentTime(_value: unknown) { }

    get paused(): boolean {
        return true;
    }

    get src(): string {
        return this.getAttribute("src") || "";
    }

    set src(value: unknown) {
        this.setAttribute("src", value == null ? "" : String(value));
    }

    get muted(): boolean {
        return this.hasAttribute("muted");
    }

    set muted(value: unknown) {
        if (value) this.setAttribute("muted", ""); else this.removeAttribute("muted");
    }

    load(): void { }

    play(): Promise<void> {
        return Promise.resolve();
    }

    pause(): void { }

    canPlayType(): string {
        return "";
    }
}
