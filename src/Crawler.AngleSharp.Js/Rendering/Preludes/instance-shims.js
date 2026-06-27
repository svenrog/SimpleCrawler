// CLR host types have no JS prototype, so instanceof with them as the right-hand side throws on V8.
// URL and Event forward to the host constructor; Node/Element/Text/Document are instanceof-only shims.
(function () {
  function ctor(host, kind) {
    var f = function () { return new host(...arguments); };
    Object.defineProperty(f, Symbol.hasInstance, { value: function (x) { return __isInstance(x, kind); } });
    return f;
  }
  function only(kind) {
    var f = function () {};
    Object.defineProperty(f, Symbol.hasInstance, { value: function (x) { return __isInstance(x, kind); } });
    return f;
  }
  globalThis.URL = ctor(__ctor_URL, 'URL');
  globalThis.Event = ctor(__ctor_Event, 'Event');
  globalThis.CustomEvent = ctor(__ctor_CustomEvent, 'CustomEvent');
  var node = only('Node');
  node.ELEMENT_NODE = 1; node.ATTRIBUTE_NODE = 2; node.TEXT_NODE = 3; node.CDATA_SECTION_NODE = 4;
  node.PROCESSING_INSTRUCTION_NODE = 7; node.COMMENT_NODE = 8; node.DOCUMENT_NODE = 9;
  node.DOCUMENT_TYPE_NODE = 10; node.DOCUMENT_FRAGMENT_NODE = 11;
  globalThis.Node = node;
  globalThis.Element = only('Element');
  globalThis.Text = only('Text');
  globalThis.Document = only('Document');
})();
