# SimpleCrawler.Js.V8

- Scripts/modules execute with a `DocumentInfo` named by their URL — keep that wiring; it makes V8 stack frames point at the failing chunk, which is the main debugging lever for minified bundles.
- `V8RuntimePool` recycles isolates and caps the heap via `V8EngineOptions.MaxHeapSizeMb` (default 256) — this guards against render-cache leaks on heterogeneous sites; don't hand out unpooled runtimes.
- Page JS cannot extend ClearScript host types (no prototype chain) — anything extendable/`instanceof`-able must be a JS class in the `SimpleCrawler.Js.Dom` prelude.
- The managed and native ClearScript.V8 packages are pinned through the shared `ClearScriptV8Version` csproj property; bump them together, never individually.
