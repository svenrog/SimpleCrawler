import { HTMLElement } from "./HTMLElement";
import type { Element } from "./Element";

// react-dom's updateOptions (`hb`) does `node.options` then iterates its `length`; without an options
// collection that read is undefined and the whole hydration render aborts. Mirrors the live HTMLOptionsCollection
// closely enough (option descendants in tree order) for react to sync `selected`/`defaultSelected`.
export class HTMLSelectElement extends HTMLElement {
    constructor() {
        super("select");
    }

    get options(): Element[] {
        return this.getElementsByTagName("option");
    }
}
