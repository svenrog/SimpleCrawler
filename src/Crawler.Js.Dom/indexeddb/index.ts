import { enqueue } from "../scheduler/taskQueue";
import { IdbDatabase } from "./IdbDatabase";
import { IdbOpenDbRequest } from "./IdbOpenDbRequest";
import { databaseRegistry } from "./store";

export const indexedDB = {
    open(name: string, version?: number): IdbOpenDbRequest {
        const req = new IdbOpenDbRequest();
        enqueue(() => {
            let dbData = databaseRegistry.get(name);
            const isNew = !dbData;
            if (!dbData) {
                dbData = { version: version || 1, stores: new Map() };
                databaseRegistry.set(name, dbData);
            }
            const db = new IdbDatabase(name, dbData);
            req.result = db;
            req.readyState = "done";
            const needsUpgrade = isNew || (typeof version === "number" && version > dbData.version);
            if (needsUpgrade) {
                dbData.version = version || dbData.version || 1;
                db.version = dbData.version;
                if (typeof req.onupgradeneeded === "function") {
                    try { req.onupgradeneeded({ target: req, type: "upgradeneeded", oldVersion: 0, newVersion: db.version }); } catch { /* as above */ }
                }
            }
            if (typeof req.onsuccess === "function") {
                try { req.onsuccess({ target: req, type: "success" }); } catch { /* as above */ }
            }
        });
        return req;
    },

    deleteDatabase(name: string): IdbOpenDbRequest {
        const req = new IdbOpenDbRequest();
        enqueue(() => {
            databaseRegistry.delete(name);
            req._succeed(undefined);
        });
        return req;
    },

    databases(): Promise<{ name: string; version: number }[]> {
        return Promise.resolve(Array.from(databaseRegistry.entries()).map(([name, d]) => ({ name, version: d.version })));
    },

    cmp(a: any, b: any): number {
        return a < b ? -1 : a > b ? 1 : 0;
    },
};
