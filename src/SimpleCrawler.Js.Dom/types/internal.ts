export interface ScriptDescriptor {
    module: boolean;
    external: boolean;
    src: string;
    text: string;
    // An external classic script marked async or defer does not run where it sits: the parser is already
    // past it by the time the network answers, so every inline script after it has run first.
    deferred: boolean;
    // Position in the document's script order, which names the element back to setCurrentScript.
    index: number;
}
