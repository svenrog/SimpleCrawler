# PLAN.md

This plan is based on a restructure of `dom.js` into TypeScript
This is essentially a minimal DOM implementation written in plain JavaScript, with everything living in one IIFE and mutable prototypes.

```
src/
├── index.ts                 // Public entrypoint
│
├── constants.ts             // NodeType, VOID elements, namespaces
│
├── scheduler/
│   └── taskQueue.ts         // queueMicrotask/setTimeout shim
│
├── css/
│   └── CSSStyleDeclaration.ts
│
├── dom/
│   ├── Node.ts
│   ├── Element.ts
│   ├── Text.ts
│   ├── Comment.ts
│   ├── Document.ts
│   ├── DocumentFragment.ts
│   └── utils.ts
│
├── selector/
│   └── querySelector.ts
│
├── html/
│   ├── parser.ts
│   ├── serializer.ts
│   ├── entities.ts
│   └── tokenizer.ts
│
├── url/
│   ├── URL.ts
│   ├── URLSearchParams.ts
│   └── resolve.ts
│
├── browser/
│   ├── globals.ts           // window/document/history/etc.
│   ├── history.ts
│   ├── location.ts
│   └── navigator.ts
│
├── crawler/
│   └── api.ts               // __crawlerLoadHtml(), etc.
│
└── types/
    └── internal.ts
```

## Core classes

Instead of prototype mutation:

```
function Node(type) {
    this.nodeType = type;
    ...
}
```
use
```
export abstract class Node {
    readonly nodeType: NodeType;

    parentNode: Node | null = null;
    childNodes: Node[] = [];

    protected constructor(type: NodeType) {
        this.nodeType = type;
    }

    appendChild(child: Node): Node {
        return this.insertBefore(child, null);
    }

    insertBefore(child: Node, ref: Node | null): Node {
        ...
    }
}
```
Then
```
export class Element extends Node {
    readonly tagName: string;
    readonly localName: string;

    readonly style = new CSSStyleDeclaration();

    private attrs = new Map<string, string>();

    constructor(tag: string) {
        super(NodeType.ELEMENT);
        ...
    }
}
```
## Shared enums
```
export const enum NodeType {
    ELEMENT = 1,
    TEXT = 3,
    COMMENT = 8,
    DOCUMENT = 9,
    DOCUMENT_FRAGMENT = 11,
}
```
```
export const VOID_ELEMENTS = new Set([
    "br",
    "img",
    "meta",
    "input",
    "link",
    ...
]);
```
## Strong typing
Instead of
```
this._attrs = {};
```
use
```
private readonly attributes = new Map<string, string>();
```
or
```
private readonly attributes: Record<string, string> = {};
```
Similarly,
```
private listeners: Map<
    string,
    EventListener[]
> = new Map();
```
## Separate parser from DOM

Your current file interleaves parsing with DOM creation.

I'd split it into
```
HTML
   ↓
Tokenizer
   ↓
Parser
   ↓
DOM tree
```
Then serialization becomes independent.
```
DOM
   ↓
Serializer
   ↓
HTML
```
That separation makes each piece much easier to test.

## Globals bootstrap

Instead of a huge IIFE:
```
(function(global){
   ...
})(globalThis);
```
I'd expose
```
export function installDOM(global: GlobalLike): void {
    global.document = new Document();
    global.window = global;
    global.location = new Location();
    ...
}
```

Then your entrypoint is simply
```
import { installDOM } from "./browser/globals";
installDOM(globalThis);
```
## Build pipeline
```
TypeScript modules (src/**)
      │
      ▼
tsup / esbuild — bundle + type-check
      │
      ▼
Single self-contained CJS bundle
      │
      ▼
src/SimpleCrawler.Js/Rendering/Preludes/dom.js   (committed)
```
The entry (`index.ts`) is side-effect-only: it installs the DOM and assigns the `__crawler*`
API onto `globalThis`, and exports nothing. That means the CJS output carries no runtime
`require` / `import` / `module` references, so it runs as a concatenated **plain script** on
both Jint and V8 through the existing `JsPreludes.Load("dom.js")` + `Combine` path — no ESM, no
terser, no changes to the prelude-loading mechanism. The build is a manual `npm run build` step
(same pattern as the TestHost Astro corpus); the compiled `dom.js` is committed, and `dotnet
build` stays Node-free. Note: `const enum` is unusable here — esbuild/tsup's `isolatedModules`
can't inline it — so `NodeType` and friends are plain enums or `as const` objects.

### One improvement I'd make

The uploaded implementation keeps everything in one file and one namespace, which makes it difficult to evolve as features grow.

I'd instead organize it around clear modules:

- DOM layer (Node, Element, Document)
- HTML layer (parser, serializer, entities)
- Browser layer (window, history, location, navigator)
- Runtime layer (scheduler, crawler API)

This keeps dependencies mostly one-way:
```
browser
    │
    ▼
DOM
    ▲
    │
HTML parser
    │
    ▼
crawler API
```
That architecture scales well while still bundling down into a single compact JavaScript file for deployment.