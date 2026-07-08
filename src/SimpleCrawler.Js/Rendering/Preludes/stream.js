"use strict";
(() => {
  // stream/ReadableStreamDefaultController.ts
  var ReadableStreamDefaultController = class {
    constructor(stream) {
      this._stream = stream;
    }
    get desiredSize() {
      const s = this._stream;
      if (s._state === "errored") return null;
      if (s._state === "closed") return 0;
      return 1 - s._queue.length;
    }
    enqueue(chunk) {
      const s = this._stream;
      if (s._state !== "readable") return;
      const pending = s._readRequests.shift();
      if (pending) pending.resolve({ value: chunk, done: false });
      else s._queue.push(chunk);
    }
    close() {
      const s = this._stream;
      if (s._state !== "readable") return;
      if (s._queue.length === 0) s._closeInternal();
      else s._closeRequested = true;
    }
    error(e) {
      this._stream._errorInternal(e);
    }
  };

  // stream/ReadableStreamDefaultReader.ts
  var ReadableStreamDefaultReader = class {
    constructor(stream) {
      if (!stream || stream._reader) throw new TypeError("ReadableStream is locked or invalid");
      this._stream = stream;
      stream._reader = this;
      this._resolveClosed = null;
      this._rejectClosed = null;
      this._closedPromise = new Promise((resolve, reject) => {
        this._resolveClosed = resolve;
        this._rejectClosed = reject;
      });
      this._closedPromise.catch(() => {
      });
      if (stream._state === "closed") this.settleClosed();
      else if (stream._state === "errored") this.settleErrored(stream._storedError);
    }
    get closed() {
      return this._closedPromise;
    }
    read() {
      if (!this._stream) return Promise.reject(new TypeError("Reader has been released"));
      return this._stream._readChunk();
    }
    cancel(reason) {
      if (!this._stream) return Promise.resolve();
      return this._stream._cancel(reason);
    }
    releaseLock() {
      if (!this._stream) return;
      this._stream._reader = null;
      this._stream = null;
    }
    settleClosed() {
      if (this._resolveClosed) this._resolveClosed();
      this._resolveClosed = null;
      this._rejectClosed = null;
    }
    settleErrored(e) {
      if (this._rejectClosed) this._rejectClosed(e);
      this._resolveClosed = null;
      this._rejectClosed = null;
    }
  };

  // stream/ReadableStream.ts
  var ReadableStream = class _ReadableStream {
    constructor(underlyingSource, _strategy) {
      const source = underlyingSource || {};
      this._state = "readable";
      this._storedError = void 0;
      this._queue = [];
      this._closeRequested = false;
      this._readRequests = [];
      this._reader = null;
      this._pulling = false;
      this._pull = typeof source.pull === "function" ? source.pull.bind(source) : null;
      this._cancelAlgorithm = typeof source.cancel === "function" ? source.cancel.bind(source) : null;
      this._controller = new ReadableStreamDefaultController(this);
      if (typeof source.start === "function") {
        try {
          const started = source.start.call(source, this._controller);
          if (started && typeof started.then === "function") started.then(void 0, (e) => this._errorInternal(e));
        } catch (e) {
          this._errorInternal(e);
        }
      }
    }
    get locked() {
      return this._reader !== null;
    }
    getReader(options) {
      if (options && options.mode === "byob") throw new TypeError("byob readers are not supported");
      return new ReadableStreamDefaultReader(this);
    }
    cancel(reason) {
      if (this.locked) return Promise.reject(new TypeError("Cannot cancel a locked stream"));
      return this._cancel(reason);
    }
    pipeTo(destination, _options) {
      const reader = this.getReader();
      const writer = destination.getWriter();
      return new Promise((resolve, reject) => {
        const step = () => {
          reader.read().then((result) => {
            if (result.done) {
              Promise.resolve(writer.close()).then(() => {
                reader.releaseLock();
                resolve();
              }, reject);
              return;
            }
            Promise.resolve(writer.write(result.value)).then(step, reject);
          }, reject);
        };
        step();
      });
    }
    pipeThrough(transform, options) {
      this.pipeTo(transform.writable, options).catch(() => {
      });
      return transform.readable;
    }
    tee() {
      const reader = this.getReader();
      const controllers = [];
      let reading = false;
      let ended = false;
      const pump = () => {
        if (ended || reading) return;
        reading = true;
        return reader.read().then((result) => {
          reading = false;
          if (result.done) {
            ended = true;
            for (const c of controllers) c.close();
            return;
          }
          for (const c of controllers) c.enqueue(result.value);
        }, (e) => {
          for (const c of controllers) c.error(e);
        });
      };
      const branch = () => new _ReadableStream({
        start: (c) => {
          controllers.push(c);
        },
        pull: () => pump(),
        cancel: (reason) => reader.cancel(reason)
      });
      return [branch(), branch()];
    }
    [Symbol.asyncIterator]() {
      const reader = this.getReader();
      return {
        next() {
          return reader.read().then((result) => result.done ? { value: void 0, done: true } : { value: result.value, done: false });
        },
        return(value) {
          reader.releaseLock();
          return Promise.resolve({ value, done: true });
        },
        [Symbol.asyncIterator]() {
          return this;
        }
      };
    }
    _readChunk() {
      if (this._state === "errored") return Promise.reject(this._storedError);
      if (this._queue.length > 0) {
        const chunk = this._queue.shift();
        if (this._queue.length === 0 && this._closeRequested) this._closeInternal();
        else this._pullIfNeeded();
        return Promise.resolve({ value: chunk, done: false });
      }
      if (this._state === "closed") return Promise.resolve({ value: void 0, done: true });
      const pending = new Promise((resolve, reject) => {
        this._readRequests.push({ resolve, reject });
      });
      this._pullIfNeeded();
      return pending;
    }
    _pullIfNeeded() {
      if (!this._pull || this._pulling || this._state !== "readable") return;
      this._pulling = true;
      try {
        const pulled = this._pull(this._controller);
        if (pulled && typeof pulled.then === "function") {
          pulled.then(() => {
            this._pulling = false;
          }, (e) => {
            this._pulling = false;
            this._errorInternal(e);
          });
        } else {
          this._pulling = false;
        }
      } catch (e) {
        this._pulling = false;
        this._errorInternal(e);
      }
    }
    _closeInternal() {
      if (this._state !== "readable") return;
      this._state = "closed";
      for (const request of this._readRequests.splice(0)) request.resolve({ value: void 0, done: true });
      if (this._reader) this._reader.settleClosed();
    }
    _errorInternal(e) {
      if (this._state !== "readable") return;
      this._state = "errored";
      this._storedError = e;
      this._queue = [];
      for (const request of this._readRequests.splice(0)) request.reject(e);
      if (this._reader) this._reader.settleErrored(e);
    }
    _cancel(reason) {
      this._queue = [];
      this._closeInternal();
      let result;
      try {
        result = this._cancelAlgorithm ? this._cancelAlgorithm(reason) : void 0;
      } catch (e) {
        return Promise.reject(e);
      }
      return Promise.resolve(result).then(() => void 0);
    }
  };

  // stream/WritableStreamDefaultWriter.ts
  var WritableStreamDefaultWriter = class {
    constructor(stream) {
      this._stream = stream;
      stream._writer = this;
    }
    get desiredSize() {
      return this._stream && this._stream._state === "writable" ? 1 : 0;
    }
    get ready() {
      return Promise.resolve();
    }
    get closed() {
      return this._stream ? this._stream._closedPromise : Promise.resolve();
    }
    write(chunk) {
      if (!this._stream) return Promise.reject(new TypeError("Writer has been released"));
      return this._stream._write(chunk);
    }
    close() {
      if (!this._stream) return Promise.reject(new TypeError("Writer has been released"));
      return this._stream._close();
    }
    abort(reason) {
      if (!this._stream) return Promise.resolve();
      return this._stream._abort(reason);
    }
    releaseLock() {
      if (!this._stream) return;
      this._stream._writer = null;
      this._stream = null;
    }
  };

  // stream/WritableStream.ts
  var WritableStream = class {
    constructor(underlyingSink, _strategy) {
      this._sink = underlyingSink || {};
      this._writer = null;
      this._state = "writable";
      this._storedError = void 0;
      this._resolveClosed = null;
      this._closedPromise = new Promise((resolve) => {
        this._resolveClosed = resolve;
      });
      this._closedPromise.catch(() => {
      });
      if (typeof this._sink.start === "function") {
        try {
          this._sink.start(this._controller());
        } catch (e) {
          this._state = "errored";
          this._storedError = e;
        }
      }
    }
    get locked() {
      return this._writer !== null;
    }
    getWriter() {
      if (this._writer) throw new TypeError("WritableStream is locked");
      return new WritableStreamDefaultWriter(this);
    }
    abort(reason) {
      return this._abort(reason);
    }
    close() {
      return this._close();
    }
    _controller() {
      return { error: (e) => {
        this._state = "errored";
        this._storedError = e;
      } };
    }
    _write(chunk) {
      if (this._state === "errored") return Promise.reject(this._storedError);
      try {
        const result = typeof this._sink.write === "function" ? this._sink.write(chunk, this._controller()) : void 0;
        return Promise.resolve(result).then(() => void 0);
      } catch (e) {
        this._state = "errored";
        this._storedError = e;
        return Promise.reject(e);
      }
    }
    _close() {
      if (this._state === "errored") return Promise.reject(this._storedError);
      if (this._state === "closed") return Promise.resolve();
      this._state = "closed";
      if (this._resolveClosed) this._resolveClosed();
      try {
        const result = typeof this._sink.close === "function" ? this._sink.close() : void 0;
        return Promise.resolve(result).then(() => void 0);
      } catch (e) {
        return Promise.reject(e);
      }
    }
    _abort(reason) {
      if (this._state === "errored") return Promise.reject(this._storedError);
      this._state = "errored";
      this._storedError = reason;
      try {
        const result = typeof this._sink.abort === "function" ? this._sink.abort(reason) : void 0;
        return Promise.resolve(result).then(() => void 0);
      } catch (e) {
        return Promise.reject(e);
      }
    }
  };

  // stream/TransformStream.ts
  var TransformStream = class {
    constructor(transformer, _writableStrategy, _readableStrategy) {
      const t = transformer || {};
      let readableController;
      this.readable = new ReadableStream({ start: (c) => {
        readableController = c;
      } });
      const transformController = {
        get desiredSize() {
          return readableController.desiredSize;
        },
        enqueue: (chunk) => readableController.enqueue(chunk),
        error: (e) => readableController.error(e),
        terminate: () => readableController.close()
      };
      this.writable = new WritableStream({
        start: () => typeof t.start === "function" ? t.start(transformController) : void 0,
        write: (chunk) => typeof t.transform === "function" ? t.transform(chunk, transformController) : transformController.enqueue(chunk),
        close: () => Promise.resolve(typeof t.flush === "function" ? t.flush(transformController) : void 0).then(() => readableController.close()),
        abort: (reason) => readableController.error(reason)
      });
    }
  };

  // browser/TextDecoder.ts
  var TextDecoder = class {
    constructor() {
      this.encoding = "utf-8";
    }
    decode(input) {
      if (input == null) return "";
      if (typeof input === "string") return input;
      const bytes = input;
      let out = "";
      let i = 0;
      const len = bytes.length;
      while (i < len) {
        const b1 = bytes[i++];
        if (b1 < 128) {
          out += String.fromCharCode(b1);
        } else if (b1 < 224) {
          const b2 = bytes[i++];
          out += String.fromCharCode((b1 & 31) << 6 | b2 & 63);
        } else if (b1 < 240) {
          const b2 = bytes[i++];
          const b3 = bytes[i++];
          out += String.fromCharCode((b1 & 15) << 12 | (b2 & 63) << 6 | b3 & 63);
        } else {
          const b2 = bytes[i++];
          const b3 = bytes[i++];
          const b4 = bytes[i++];
          const cp = (b1 & 7) << 18 | (b2 & 63) << 12 | (b3 & 63) << 6 | b4 & 63;
          const adj = cp - 65536;
          out += String.fromCharCode(55296 | adj >> 10, 56320 | adj & 63);
        }
      }
      return out;
    }
  };

  // stream/TextDecoderStream.ts
  var TextDecoderStream = class extends TransformStream {
    constructor(_label, _options) {
      const decoder = new TextDecoder();
      super({
        transform(chunk, controller) {
          const text = decoder.decode(chunk);
          if (text) controller.enqueue(text);
        }
      });
      this.encoding = "utf-8";
    }
  };

  // browser/TextEncoder.ts
  var TextEncoder = class {
    constructor() {
      this.encoding = "utf-8";
    }
    encode(input) {
      const s = input == null ? "" : String(input);
      const out = [];
      for (let i = 0; i < s.length; ) {
        const c = s.charCodeAt(i++);
        if (c < 128) {
          out.push(c);
        } else if (c < 2048) {
          out.push(192 | c >> 6, 128 | c & 63);
        } else if (c >= 55296 && c <= 56319 && i < s.length) {
          const c2 = s.charCodeAt(i++);
          const cp = 65536 + ((c & 1023) << 10) + (c2 & 1023);
          out.push(240 | cp >> 18, 128 | cp >> 12 & 63, 128 | cp >> 6 & 63, 128 | cp & 63);
        } else {
          out.push(224 | c >> 12, 128 | c >> 6 & 63, 128 | c & 63);
        }
      }
      return new Uint8Array(out);
    }
  };

  // stream/TextEncoderStream.ts
  var TextEncoderStream = class extends TransformStream {
    constructor() {
      const encoder = new TextEncoder();
      super({
        transform(chunk, controller) {
          controller.enqueue(encoder.encode(chunk == null ? "" : String(chunk)));
        }
      });
      this.encoding = "utf-8";
    }
  };

  // stream/QueuingStrategy.ts
  var CountQueuingStrategy = class {
    constructor(init) {
      this.highWaterMark = init && typeof init.highWaterMark === "number" ? init.highWaterMark : 1;
    }
    size() {
      return 1;
    }
  };
  var ByteLengthQueuingStrategy = class {
    constructor(init) {
      this.highWaterMark = init && typeof init.highWaterMark === "number" ? init.highWaterMark : 1;
    }
    size(chunk) {
      return chunk && typeof chunk.byteLength === "number" ? chunk.byteLength : 0;
    }
  };

  // browser/native.ts
  function markNative(fn, name) {
    const label = "function " + name + "() { [native code] }";
    try {
      Object.defineProperty(fn, "toString", {
        value: function() {
          return label;
        },
        writable: true,
        configurable: true,
        enumerable: false
      });
    } catch {
    }
  }
  function markPrototypeNative(ctor) {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    for (const key of Object.getOwnPropertyNames(proto)) {
      if (key === "constructor") continue;
      const desc = Object.getOwnPropertyDescriptor(proto, key);
      if (desc && typeof desc.value === "function") markNative(desc.value, key);
    }
  }

  // stream/index.ts
  function installStreams(global) {
    const ctors = {
      ReadableStream,
      ReadableStreamDefaultController,
      ReadableStreamDefaultReader,
      WritableStream,
      WritableStreamDefaultWriter,
      TransformStream,
      TextDecoderStream,
      TextEncoderStream,
      ByteLengthQueuingStrategy,
      CountQueuingStrategy
    };
    for (const name in ctors) {
      markPrototypeNative(ctors[name]);
      global[name] = global[name] || ctors[name];
    }
  }
  installStreams(globalThis);
})();
