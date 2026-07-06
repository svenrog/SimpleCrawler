import { enqueue } from "./enqueue";
import { IdbDatabase } from "./IdbDatabase";
import { IdbObjectStore } from "./IdbObjectStore";
import { IdbRequest } from "./IdbRequest";
import { IdbDatabaseData } from "./store";

export class IdbTransaction {
    db: IdbDatabase;
    mode: string;
    error: any = null;
    oncomplete: any = null;
    onerror: any = null;
    onabort: any = null;
    private _db: IdbDatabaseData;
    private _aborted = false;
    private _requests: IdbRequest[] = [];

    constructor(db: IdbDatabase, dbData: IdbDatabaseData, mode: string) {
        this.db = db;
        this._db = dbData;
        this.mode = mode;
        // The store operations run synchronously right after the caller wires up oncomplete; settling on
        // the next task turn lets those requests and their onsuccess handlers fire before completion.
        enqueue(() => this._complete());
    }

    objectStore(name: string): IdbObjectStore {
        let store = this._db.stores.get(name);
        if (!store) {
            store = new Map();
            this._db.stores.set(name, store);
        }
        return new IdbObjectStore(name, store, this);
    }

    _enlist(req: IdbRequest): void {
        this._requests.push(req);
    }

    abort(): void {
        this._aborted = true;
        if (typeof this.onabort === "function") {
            try { this.onabort({ target: this, type: "abort" }); } catch { /* as above */ }
        }
    }

    private _complete(): void {
        if (this._aborted) return;
        for (const req of this._requests) req._succeed(req.result);
        if (typeof this.oncomplete === "function") {
            try { this.oncomplete({ target: this, type: "complete" }); } catch { /* as above */ }
        }
    }
}