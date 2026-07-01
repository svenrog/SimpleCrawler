// Jint's Map.prototype.keys()/values() iterators enumerate the live backing list, so mutating the map
// mid-iteration (e.g. `for (const k of map.keys()) map.delete(k)`, which a bundle is free to do) throws a
// CLR "Collection was modified" that escapes the bundle. The entries iterator tolerates it, so route both
// views through it; this keeps live semantics (added keys seen, deleted keys skipped) and stays a no-op
// on V8, which has neither the bug nor this prelude.

const proto = Map.prototype;
const entries = proto.entries;

function projection(index: 0 | 1) {
  return function <K, V>(this: Map<K, V>) {
    var it = entries.call(this);
    var view: Iterator<any, any> = { 
        next: function () {
            var r = it.next();
            return r.done ? r : { value: r.value[index], done: false };
        }
    };
    //@ts-expect-error
    view[Symbol.iterator] = function () { return this; }
    return view;
  };
}
//@ts-expect-error
proto.keys = projection(0);
//@ts-expect-error
proto.values = projection(1);