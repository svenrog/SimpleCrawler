// In-memory backing for the IndexedDB shim. A render never persists across pages, so every database lives
// for the process and is lost when the engine is recycled — enough for bundles that gate a key-value cache
// on `window.indexedDB` (feature-detect then open/put/get) and would otherwise re-fetch on every read.

export type IdbStore = Map<any, any>;
export type IdbDatabaseData = { version: number; stores: Map<string, IdbStore> };

export const databaseRegistry = new Map<string, IdbDatabaseData>();
