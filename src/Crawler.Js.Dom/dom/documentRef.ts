// The pure-JS DOM has exactly one Document; nodes expose it as `ownerDocument` through this shared ref,
// which globals.ts sets once the document exists. A module-level ref keeps Node free of an import cycle
// with the Document/Element graph.
export const documentRef: { current: any } = { current: null };
