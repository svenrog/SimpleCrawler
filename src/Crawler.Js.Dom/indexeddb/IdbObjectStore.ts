import { IdbStore } from "./store";
import { IdbRequest } from "./IdbRequest"
import { IdbTransaction } from "./IdbTransaction";

export class IdbObjectStore {
    name: string;
    private _store: IdbStore;
    private _tx: IdbTransaction;

    constructor(name: string, store: IdbStore, tx: IdbTransaction) {
        this.name = name;
        this._store = store;
        this._tx = tx;
    }

    private _request(result: any): IdbRequest {
        const req = new IdbRequest();
        req.source = this;
        req.transaction = this._tx;
        req.result = result;
        this._tx._enlist(req);
        return req;
    }

    get(key: any): IdbRequest {
        return this._request(this._store.has(key) ? this._store.get(key) : undefined);
    }

    getAll(): IdbRequest {
        return this._request(Array.from(this._store.values()));
    }

    getAllKeys(): IdbRequest {
        return this._request(Array.from(this._store.keys()));
    }

    put(value: any, key?: any): IdbRequest {
        this._store.set(key, value);
        return this._request(key);
    }

    add(value: any, key?: any): IdbRequest {
        this._store.set(key, value);
        return this._request(key);
    }

    delete(key: any): IdbRequest {
        this._store.delete(key);
        return this._request(undefined);
    }

    clear(): IdbRequest {
        this._store.clear();
        return this._request(undefined);
    }

    count(): IdbRequest {
        return this._request(this._store.size);
    }

    openCursor(): IdbRequest {
        return this._request(null);
    }
}
