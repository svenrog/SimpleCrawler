import { Element } from "./Element";
import { HTMLElement } from "./HTMLElement";

// Re-exported so the globals loop picks them up alongside the markers, but unlike them these are real
// elements the parser/createElement instantiate for property reflection (anchor href, script src, link href).
export { HTMLAnchorElement } from "./HTMLAnchorElement";
export { HTMLScriptElement } from "./HTMLScriptElement";
export { HTMLLinkElement } from "./HTMLLinkElement";
export { HTMLSelectElement } from "./HTMLSelectElement";
export { HTMLOptionElement } from "./HTMLOptionElement";
export { HTMLImageElement } from "./HTMLImageElement";
export { HTMLIFrameElement } from "./HTMLIFrameElement";
export { HTMLMediaElement } from "./HTMLMediaElement";
export { HTMLVideoElement } from "./HTMLVideoElement";
export { HTMLAudioElement } from "./HTMLAudioElement";
export { HTMLDialogElement } from "./HTMLDialogElement";
export { HTMLCanvasElement } from "./HTMLCanvasElement";
export { HTMLInputElement } from "./HTMLInputElement";
export { HTMLTextAreaElement } from "./HTMLTextAreaElement";
export { HTMLFormElement } from "./HTMLFormElement";

// Marker interfaces for `instanceof` checks. Frameworks probe element types (React's getActiveElementDeep
// does `node instanceof window.HTMLIFrameElement`, Vue references SVGElement) and a `instanceof undefined`
// throws "Right-hand side of 'instanceof' is not an object". dom.js creates most HTML nodes as a plain
// Element/HTMLElement, so these are never actually instantiated — they exist so the checks evaluate (false)
// instead of throwing.
export class HTMLButtonElement extends HTMLElement { }
export class HTMLStyleElement extends HTMLElement { }
export class HTMLUnknownElement extends HTMLElement { }
export class SVGElement extends Element { }
export class SVGSVGElement extends SVGElement { }
export class MathMLElement extends Element { }
