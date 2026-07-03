"use strict";
(() => {
  // indexeddb/enqueue.ts
  var queue = globalThis.queueMicrotask;
  function enqueue(cb) {
    queue(cb);
  }

  // indexeddb/DomStringList.ts
  var DomStringList = class extends Array {
    contains(name) {
      return this.indexOf(name) !== -1;
    }
    item(index) {
      return index >= 0 && index < this.length ? this[index] : null;
    }
  };

  // indexeddb/IdbRequest.ts
  var IdbRequest = class {
    constructor() {
      this.result = void 0;
      this.error = null;
      this.readyState = "pending";
      this.onsuccess = null;
      this.onerror = null;
      this.source = null;
      this.transaction = null;
    }
    _succeed(value) {
      this.result = value;
      this.readyState = "done";
      if (typeof this.onsuccess === "function") {
        try {
          this.onsuccess({ target: this, type: "success" });
        } catch {
        }
      }
    }
    _fail(error) {
      this.error = error;
      this.readyState = "done";
      if (typeof this.onerror === "function") {
        try {
          this.onerror({ target: this, type: "error" });
        } catch {
        }
      }
    }
  };

  // indexeddb/IdbObjectStore.ts
  var IdbObjectStore = class {
    constructor(name, store, tx) {
      this.name = name;
      this._store = store;
      this._tx = tx;
    }
    _request(result) {
      const req = new IdbRequest();
      req.source = this;
      req.transaction = this._tx;
      req.result = result;
      this._tx._enlist(req);
      return req;
    }
    get(key) {
      return this._request(this._store.has(key) ? this._store.get(key) : void 0);
    }
    getAll() {
      return this._request(Array.from(this._store.values()));
    }
    getAllKeys() {
      return this._request(Array.from(this._store.keys()));
    }
    put(value, key) {
      this._store.set(key, value);
      return this._request(key);
    }
    add(value, key) {
      this._store.set(key, value);
      return this._request(key);
    }
    delete(key) {
      this._store.delete(key);
      return this._request(void 0);
    }
    clear() {
      this._store.clear();
      return this._request(void 0);
    }
    count() {
      return this._request(this._store.size);
    }
    openCursor() {
      return this._request(null);
    }
  };

  // indexeddb/IdbTransaction.ts
  var IdbTransaction = class {
    constructor(db, dbData, mode) {
      this.error = null;
      this.oncomplete = null;
      this.onerror = null;
      this.onabort = null;
      this._aborted = false;
      this._requests = [];
      this.db = db;
      this._db = dbData;
      this.mode = mode;
      enqueue(() => this._complete());
    }
    objectStore(name) {
      let store = this._db.stores.get(name);
      if (!store) {
        store = /* @__PURE__ */ new Map();
        this._db.stores.set(name, store);
      }
      return new IdbObjectStore(name, store, this);
    }
    _enlist(req) {
      this._requests.push(req);
    }
    abort() {
      this._aborted = true;
      if (typeof this.onabort === "function") {
        try {
          this.onabort({ target: this, type: "abort" });
        } catch {
        }
      }
    }
    _complete() {
      if (this._aborted) return;
      for (const req of this._requests) req._succeed(req.result);
      if (typeof this.oncomplete === "function") {
        try {
          this.oncomplete({ target: this, type: "complete" });
        } catch {
        }
      }
    }
  };

  // indexeddb/IdbDatabase.ts
  var IdbDatabase = class {
    constructor(name, dbData) {
      this.name = name;
      this._db = dbData;
      this.version = dbData.version;
      this.objectStoreNames = new DomStringList(...dbData.stores.keys());
    }
    createObjectStore(name) {
      if (!this._db.stores.has(name)) this._db.stores.set(name, /* @__PURE__ */ new Map());
      this.objectStoreNames = new DomStringList(...this._db.stores.keys());
      return new IdbObjectStore(name, this._db.stores.get(name), new IdbTransaction(this, this._db, "versionchange"));
    }
    deleteObjectStore(name) {
      this._db.stores.delete(name);
      this.objectStoreNames = new DomStringList(...this._db.stores.keys());
    }
    transaction(_names, mode) {
      return new IdbTransaction(this, this._db, mode || "readonly");
    }
    close() {
    }
  };

  // indexeddb/IdbOpenDbRequest.ts
  var IdbOpenDbRequest = class extends IdbRequest {
    constructor() {
      super(...arguments);
      this.onupgradeneeded = null;
      this.onblocked = null;
    }
  };

  // indexeddb/store.ts
  var databaseRegistry = /* @__PURE__ */ new Map();

  // indexeddb/index.ts
  var indexedDB = {
    open(name, version) {
      const req = new IdbOpenDbRequest();
      enqueue(() => {
        let dbData = databaseRegistry.get(name);
        const isNew = !dbData;
        if (!dbData) {
          dbData = { version: version || 1, stores: /* @__PURE__ */ new Map() };
          databaseRegistry.set(name, dbData);
        }
        const db = new IdbDatabase(name, dbData);
        req.result = db;
        req.readyState = "done";
        const needsUpgrade = isNew || typeof version === "number" && version > dbData.version;
        if (needsUpgrade) {
          dbData.version = version || dbData.version || 1;
          db.version = dbData.version;
          if (typeof req.onupgradeneeded === "function") {
            try {
              req.onupgradeneeded({ target: req, type: "upgradeneeded", oldVersion: 0, newVersion: db.version });
            } catch {
            }
          }
        }
        if (typeof req.onsuccess === "function") {
          try {
            req.onsuccess({ target: req, type: "success" });
          } catch {
          }
        }
      });
      return req;
    },
    deleteDatabase(name) {
      const req = new IdbOpenDbRequest();
      enqueue(() => {
        databaseRegistry.delete(name);
        req._succeed(void 0);
      });
      return req;
    },
    databases() {
      return Promise.resolve(Array.from(databaseRegistry.entries()).map(([name, d]) => ({ name, version: d.version })));
    },
    cmp(a, b) {
      return a < b ? -1 : a > b ? 1 : 0;
    }
  };

  // indexeddb/prelude.ts
  globalThis.indexedDB = globalThis.indexedDB || indexedDB;
})();
