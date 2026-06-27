// React's scheduler defers flushes via MessageChannel: port2.postMessage triggers port1.onmessage
// as a macrotask via our setTimeout drain, so batched render updates actually commit.
(function () {
  if (globalThis.MessageChannel) return;
  function Port() { this.onmessage = null; this._other = null; }
  Port.prototype.postMessage = function (data) {
    var other = this._other;
    setTimeout(function () { if (other && other.onmessage) other.onmessage({ data: data }); }, 0);
  };
  Port.prototype.start = function () {};
  Port.prototype.close = function () {};
  Port.prototype.addEventListener = function (t, cb) { if (t === 'message') this.onmessage = cb; };
  Port.prototype.removeEventListener = function (t, cb) { if (t === 'message' && this.onmessage === cb) this.onmessage = null; };
  globalThis.MessagePort = globalThis.MessagePort || Port;
  globalThis.MessageChannel = class MessageChannel {
    constructor() {
      this.port1 = new Port();
      this.port2 = new Port();
      this.port1._other = this.port2;
      this.port2._other = this.port1;
    }
  };
})();
