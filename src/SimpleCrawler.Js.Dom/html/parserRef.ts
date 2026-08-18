import type { Node } from "../dom/Node";

// parser.ts imports Element (and the HTML* subclasses) to build the tree, so Element can't import parser
// back without a class-extends-undefined init cycle. This module-level ref, set by parser.ts at load, lets
// Element.innerHTML parse a fragment at runtime without the static import.
export const parserRef: { parseFragment: ((html: unknown, context?: string) => Node[]) | null } = { parseFragment: null };
