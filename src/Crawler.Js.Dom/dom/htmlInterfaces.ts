import { Element } from "./Element";
import { HTMLElement } from "./HTMLElement";

// Marker interfaces for `instanceof` checks. Frameworks probe element types (React's getActiveElementDeep
// does `node instanceof window.HTMLIFrameElement`, Vue references SVGElement) and a `instanceof undefined`
// throws "Right-hand side of 'instanceof' is not an object". dom.js creates every HTML node as a plain
// Element/HTMLElement, so these are never actually instantiated — they exist so the checks evaluate (false)
// instead of throwing.
export class HTMLIFrameElement extends HTMLElement { }
export class HTMLInputElement extends HTMLElement { }
export class HTMLTextAreaElement extends HTMLElement { }
export class HTMLSelectElement extends HTMLElement { }
export class HTMLOptionElement extends HTMLElement { }
export class HTMLButtonElement extends HTMLElement { }
export class HTMLAnchorElement extends HTMLElement { }
export class HTMLImageElement extends HTMLElement { }
export class HTMLFormElement extends HTMLElement { }
export class HTMLStyleElement extends HTMLElement { }
export class HTMLScriptElement extends HTMLElement { }
export class HTMLLinkElement extends HTMLElement { }
export class HTMLCanvasElement extends HTMLElement { }
export class HTMLUnknownElement extends HTMLElement { }
export class SVGElement extends Element { }
export class SVGSVGElement extends SVGElement { }
export class MathMLElement extends Element { }
