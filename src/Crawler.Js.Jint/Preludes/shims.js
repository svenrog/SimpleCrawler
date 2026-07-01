"use strict";
(() => {
  // shims/jint/mapIterator.ts
  var proto = Map.prototype;
  var entries = proto.entries;
  function projection(index) {
    return function() {
      var it = entries.call(this);
      var view = {
        next: function() {
          var r = it.next();
          return r.done ? r : { value: r.value[index], done: false };
        }
      };
      view[Symbol.iterator] = function() {
        return this;
      };
      return view;
    };
  }
  proto.keys = projection(0);
  proto.values = projection(1);
})();
