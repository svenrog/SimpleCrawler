import type { Element } from "./Element";
import { HTMLAnchorElement } from "./HTMLAnchorElement";
import { HTMLScriptElement } from "./HTMLScriptElement";
import { HTMLLinkElement } from "./HTMLLinkElement";
import { HTMLSelectElement } from "./HTMLSelectElement";
import { HTMLOptionElement } from "./HTMLOptionElement";
import { HTMLImageElement } from "./HTMLImageElement";
import { HTMLIFrameElement } from "./HTMLIFrameElement";
import { HTMLVideoElement } from "./HTMLVideoElement";
import { HTMLAudioElement } from "./HTMLAudioElement";
import { HTMLDialogElement } from "./HTMLDialogElement";
import { HTMLCanvasElement } from "./HTMLCanvasElement";
import { HTMLMetaElement } from "./HTMLMetaElement";
import { HTMLInputElement } from "./HTMLInputElement";
import { HTMLTextAreaElement } from "./HTMLTextAreaElement";
import { HTMLFormElement } from "./HTMLFormElement";

// tag → factory for the element subclasses that reflect properties (anchor href, script/img src, select
// options, ...). Shared by the string parser, the tree builder and document.createElement so every
// construction path yields the same node types; unlisted tags fall back to a plain Element.
export const reflectedElementFactories: Record<string, () => Element> = {
    a: () => new HTMLAnchorElement(),
    script: () => new HTMLScriptElement(),
    link: () => new HTMLLinkElement(),
    select: () => new HTMLSelectElement(),
    option: () => new HTMLOptionElement(),
    img: () => new HTMLImageElement(),
    iframe: () => new HTMLIFrameElement(),
    video: () => new HTMLVideoElement(),
    audio: () => new HTMLAudioElement(),
    dialog: () => new HTMLDialogElement(),
    canvas: () => new HTMLCanvasElement(),
    meta: () => new HTMLMetaElement(),
    input: () => new HTMLInputElement(),
    textarea: () => new HTMLTextAreaElement(),
    form: () => new HTMLFormElement(),
};
