# SimpleCrawler.Js.Jint

- Invoke deferred JS callbacks (timers, resource events) through `_engine.Invoke(...)`, never by calling a marshalled delegate directly — direct invocation NREs inside Jint when the callback has default parameters, and the failure looks like an interpreter bug.
- Keep `Engine.CatchClrExceptions()` — host-object exceptions must surface as catchable JS errors or they escape the bundle's try/catch and abort the whole render.
- Each engine instance renders exactly one page by design; engine reuse/pooling was measured at ~1-2% and deliberately removed — don't reintroduce it without new measurements.
