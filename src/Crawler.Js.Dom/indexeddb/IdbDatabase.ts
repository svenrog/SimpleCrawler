import { DomStringList } from "./DomStringList";
import { IdbObjectStore } from "./IdbObjectStore";
import { IdbTransaction } from "./IdbTransaction";
import { IdbDatabaseData } from "./store";

export class IdbDatabase {
    name: string;
    version: number;
    objectStoreNames: DomStringList;
    private _db: IdbDatabaseData;

    constructor(name: string, dbData: IdbDatabaseData) {
        this.name = name;
        this._db = dbData;
        this.version = dbData.version;
        this.objectStoreNames = new DomStringList(...dbData.stores.keys());
    }

    createObjectStore(name: string): IdbObjectStore {
        if (!this._db.stores.has(name)) this._db.stores.set(name, new Map());
        this.objectStoreNames = new DomStringList(...this._db.stores.keys());
        return new IdbObjectStore(name, this._db.stores.get(name)!, new IdbTransaction(this, this._db, "versionchange"));
    }

    deleteObjectStore(name: string): void {
        this._db.stores.delete(name);
        this.objectStoreNames = new DomStringList(...this._db.stores.keys());
    }

    transaction(_names: string | string[], mode?: string): IdbTransaction {
        return new IdbTransaction(this, this._db, mode || "readonly");
    }

    close(): void { }
}