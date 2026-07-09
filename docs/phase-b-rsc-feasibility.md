# Phase B — RSC Full-Render Feasibility (Go / No-Go)

**Status: FIXES LANDED, VERIFIED (Phase B.3, 2026-07-09).** The Phase B.2 mechanism below was disproven
in favour of a real diagnosis (fatal commit-phase exceptions silently swallowed by the task pump), and
the ordered fix list it produced has now been implemented in product code and re-verified live. Result:
**norengros.no now fully commits — 51 anchors (up from the 47-anchor SSR baseline), 0 crashes,
`pendingLanes=0`, all 549 host fibers connected.** The Phase A guard is now a no-op there instead of a
rescue. aleris.se also now commits cleanly (0 crashes, 6/6 hosts connected, up from 0 fibers attaching
at all) but still yields 0 anchors, blocked on an unrelated app-level "Client Functions cannot be passed
directly to Server Functions" server-action serialization error — ordinary site-debugging, not an
RSC-runtime-boundary problem, exactly as predicted below. See "Phase B.3 — fix verification" after the
ordered fix list.

**Status: REOPENED (Phase B.2, 2026-07-08).** The deeper instrumentation below **disproves the
original NO-GO mechanism**. Neither site suspends on a never-resolving Flight promise. Both die on
**fatal exceptions thrown inside React's commit phase that the DOM's task pump silently swallows**
(`scheduler/taskQueue.ts` `pumpTasks` wraps callbacks in `try { } catch { }`; same in
`resourceLoader.fireResourceEvent`) — which is exactly why every failure of this class presents as
"the render just settles" with zero errors. The two sites have *different* root causes, and several
are ordinary bounded shim gaps. Phase A (opt-in `EnableStreams` + SSR-baseline guard) remains correct
and safe as the shipped behaviour while this is worked.

---

## Phase B.2 — the actual mechanism (deep-dive results)

Probe: `.spike/spike.js` v3 (untracked) — everything from v2 plus: Flight chunk-map capture via a
`Map.prototype.set` hook (Flight chunks are Promise subclasses with plain `status`/`value`/`reason`
fields — this build has no `_response`, which is why v2's inference went wrong), `__webpack_require__.e/.l`
settlement tracking, ReadableStream read/close accounting, `__next_f` row accounting, a minimal
`__REACT_DEVTOOLS_GLOBAL_HOOK__` (per-fiber unmount log + commit counter), FiberRoot lane inspection,
DOM mutation logging with node serials, `reportError` capture, and **catch-log-rethrow wrappers around
queueMicrotask/setTimeout/rAF/MessageChannel callbacks** (the decisive one: it surfaces what the pump
swallows).

### norengros.no — React 19 double-owned hoistable crash mid-commit

1. **The Flight pipeline is completely healthy** (overturns the old verdict): the chunk map holds 168
   chunks — **0 pending, 0 blocked, 0 errored, root `fulfilled`**. The stream delivers all rows and
   closes (`done=true`). All 33 `require.e` chunk-ensures resolve. The 24 `resolved_model` chunks are
   rows nobody ever *read* (they lazily initialize on first read) — including the row that contains the
   entire `<div class="app">` tree.
2. Hydration bails to **client-render-from-root** (silently — Next.js supplies its own
   `onRecoverableError` and deliberately swallows bailout-to-CSR; React's recoverable-error report is
   also deferred to a recovery commit that never happens). React then walks through **16–25 commits**
   (run-to-run timing variance), acquiring the real `html`/`head`/`body` singletons, deleting the SSR
   body content (hoistables spared — hence the observed "115 script + 52 link + 12 meta" corpse), and
   building the **full app tree detached** (the per-fiber unmount log shows real content: footer social
   `<a href="https://www.facebook.com/norengros/">` etc.).
3. **Terminal event:** during the deletion phase of a late commit, React's `commitDeletionEffectsOnFiber`
   case 26 (Hoistable) runs `stateNode.parentNode.removeChild(stateNode)` on the SSR
   `<meta charset="utf-8">` — but **two fibers own that same DOM node** (unmount log: `meta#22
   parent=head` deleted successfully early in the cascade, then `meta#22 parent=NULL` again at the end),
   so the second deletion throws `TypeError: Cannot read properties of null (reading 'removeChild')`.
   The pump swallows it; React's work loop unwinds mid-commit; `pendingLanes=2` is left stuck with no
   scheduled callback; and because React does deletions before placements **in the same mutation pass,
   the placement that would have attached the app tree to `<body>` never runs**. The DOM-mutation log
   confirms no `div`/`svg`/`a` was ever inserted into the body while connected.
4. Old claims corrected: "no root committed" — false, the `__reactContainer$` marker sits on the
   `document` node itself, which the v2 walk (started at `documentElement`) skipped; "0 errors" — false,
   the fatal error existed and was swallowed; "never-resolving Flight promise" — false, see (1).
5. The double-owned hoistable itself is React-internals fiber bookkeeping under a multi-teardown thrash
   (matches known React 19 hoistable `removeChild` null reports). Environment factors that *amplify* the
   thrash (auth-gated `/api/*` 400s driving error-boundary cycles, missing `document.readyState`, etc.)
   are the realistic in-scope lever, not patching React.

### aleris.se — plain shim gaps, no RSC involvement at all

The old report's "same silent suspension, stronger case" was wrong. With errors surfaced, aleris died
on the **first render attempt (0 commits, 0 fibers)** at a missing DOM API, then each fix peeled the
next layer (validated live by hot-patching the prototypes from the spike):

| Fix applied (spike hotfix) | Next failure surfaced | Commits |
|---|---|---|
| — | `TypeError: l.removeAttributeNode is not a function` in `acquireSingletonInstance` (React strips singleton attrs via `for (c = el.attributes; c.length;) el.removeAttributeNode(c[0])` — also requires a **live** `attributes` collection; our snapshot array would loop forever) | 0 |
| live `attributes` + `removeAttributeNode` | React #423 (hydration error → root client render) then **#446** `"resourceRoot" was expected to exist` — `getHoistableRoot(container)` is `container.getRootNode?.() ?? container.ownerDocument`, and `ownerDocument` is null for the document → missing **`Node.getRootNode`** | 1 |
| + `getRootNode` | `document.getElementsByName is not a function` | 2 |
| + `getElementsByName` | app-level Flight error "Client Functions cannot be passed directly to Server Functions" (server-action serialization); dynamic chunks now load (11.5k modules registered vs 591) | 10 |

Still 0 anchors at cutoff, but the remaining work is ordinary site-debugging of the same kind as the
rest of the JS-DOM backlog, **not** a runtime-boundary problem. Also observed on aleris: the Flight
stream never closes (`done=false`) — `document.readyState` is absent in our DOM and `DOMContentLoaded`
never fires, which gates Next's stream close and is a plausible contributor to its hydration error.

### Product actions this implies (ordered)

1. **Stop swallowing fatal task errors** — log (at minimum debug-level via `__crawlerLog`) exceptions
   caught in `taskQueue.pumpTasks` and `resourceLoader.fireResourceEvent`. This single change converts
   every future "render settles silently" into a named exception with a stack that V8 labels by chunk URL.
2. **Shim the proven gaps** (each validated live on aleris): live `NamedNodeMap`-style
   `Element.attributes`, `removeAttributeNode`/`getAttributeNode`/`setAttributeNode`, `Node.getRootNode`,
   `document.getElementsByName`, global `reportError`; consider `document.readyState` +
   `DOMContentLoaded` dispatch (Next gates Flight stream close on them).
3. Re-run norengros after 1–2: if the teardown thrash calms (or the terminal commit survives), the full
   client tree — whose links demonstrably exist — attaches, and the guard becomes a no-op instead of a
   rescue.
4. Phase C verdict: **no longer a proven NO-GO, but not yet a GO** — the norengros React-internal
   double-delete is the one item that may stay out of reach. Routing hard RSC sites to headless remains
   the pragmatic default meanwhile; the Phase A guard stays regardless.

## Phase B.3 — fix verification (2026-07-09)

Items 1 and 2 above are implemented in product code (not the spike): `JsRenderer.ConfigureDiagnostics`
embeds `__crawlerDiagnostic` unconditionally and logs at Debug; `taskQueue.pumpTasks` and
`resourceLoader.fireResourceEvent` route their catches through `diagnostics.ts`'s `reportSwallowed`;
`Element.attributes` is a live `Proxy`-backed view over the same attribute map that
`removeAttributeNode`/`getAttributeNode`/`setAttributeNode` mutate; `Node.getRootNode`,
`Document.getElementsByName`, global `reportError`, and `Document.readyState` (always `"complete"` — this
render has no real loading phase) + a `DOMContentLoaded` dispatch after bundle top-level execution (before
the drain loop) are all in place. Re-ran both sites with the same `.spike/spike.js` v3 probe:

| Signal | norengros.no (before → after) | aleris.se (before → after) |
|---|---|---|
| Crash | `TypeError: null.removeChild` mid-commit → **none** | first-render `removeAttributeNode`/`getRootNode`/`getElementsByName` throws → **none** |
| `pendingLanes` at finalize | stuck at 2 → **0** | n/a (never reached a root before) → **0** |
| Host fibers connected | thrash, app tree built detached → **549/549 connected** | 0 fibers attached → **6/6 connected** |
| Anchors | 0 → **51** (SSR baseline was 47 — guard is now a no-op) | 0 → **0** (unchanged) |
| Remaining blocker | none — fully committed | app-level `Error: Client Functions cannot be passed directly to Server Functions` (server-action serialization) |

norengros: the double-owned-hoistable double-delete predicted as "the one item that may stay out of
reach" **did not recur** — swallowed-exception logging plus the shim fixes were enough that the terminal
commit no longer throws, so the confirmed cause (the deletion-before-placement crash) simply doesn't
happen anymore. This flips the Phase C verdict for RSC full-render from "not yet a GO" to **working** for
at least this class of site; the Phase A guard remains as a correctness backstop, not the primary path.

aleris: confirms the doc's prediction exactly — once the shim gaps stopped crashing the render, what's
left is an ordinary app-level bug (unrelated to RSC/streaming), i.e. normal JS-DOM backlog work, not a
runtime-boundary problem.

Reproducibility: the `JSRENDER_SPIKE_FILE` seam was reverted after this verification pass (spike-only
tooling, not a permanent product change — same convention as the original Phase B spike). Re-apply the
~4 lines documented under "Reproducibility" below to re-run the probe.

---

## Original Phase B report (2026-07-08, superseded where it conflicts with B.2 above)

**Status (original): NO-GO.** Full React Server Components client render is **not reachable with bounded
shimming**; it sits past the “reimplement the runtime” boundary. Phase C should **not** be pursued.
The shipped Phase A behaviour (opt-in `EnableStreams` + SSR-baseline guard) is the correct stopping
point. Recommendation: leave it as-is and route RSC sites whose server shell lacks needed links to the
Playwright/Puppeteer backends.

Site studied: a live Next.js App-Router (RSC) site. Harness:
`rendersize v8 react --url <site> --fetch --streams` plus an env-gated instrumentation probe.

---

## What Phase B was asked to determine

1. Does React’s client-reference resolver (`__webpack_require__(id)` for the `I[...]` refs) return the
   module, or throw?
2. Is the collapse a **hydration-mismatch → client-render** (fixable by making hydration succeed), or
   **client components suspending forever**?
3. Can a React dev build surface real error messages instead of swallowed digests?

---

## Verdict and the one-sentence reason

Every bounded shim works and every client-reference module resolves — yet React’s hydration stalls on a
**never-resolving Promise inside `react-server-dom-webpack`’s client-reference / Flight-model resolution
gate**, commits nothing, and the task queue goes idle with zero errors. Making that gate resolve means
reproducing the RSC runtime’s async chunk-loading handshake, not shimming a missing API.

---

## Evidence (live site, V8 + `EnableStreams` + `EnableFetch`)

### The Flight payload is delivered intact (the streams shim is correct)
- 71 `self.__next_f.push([1,…])` rows, **36 webpack chunks** register, runtime initializes.
- **63 client-reference definitions** (`I[modId,[chunks],export]`) and **283 `$L` lazy markers** in the
  SSR tree.

### The webpack module graph is healthy
- **988 modules registered, 897 executed, 0 eval throws.** Nothing fails to initialize.

### Q1 — client-reference resolution: **returns the module, does not throw**
- Probe exfiltrated `__webpack_require__` from a module factory’s 3rd argument and test-resolved every
  `I[...]` ID parsed from the surviving Flight scripts.
- **61 / 61 resolved; 0 undefined; 0 threw.** (`req.e` and `req.f` are present — webpack’s chunk-ensure
  API is available to the Flight client.)
- This **rules out** “unresolvable client reference” as the cause.

### Q2 — collapse type: **neither mismatch-render nor clean-suspend; it stalls mid-hydration**
Compared against the SSR shell (≈688 elements / 47 anchors), the post-React tree is **≈184 elements /
0 anchors** — and those 184 survivors are **115 `<script>` + 52 `<link>` + 12 `<meta>`**: the body has
been reduced to *only its bootstrap*. React DOM scan at finalize:

| Signal | Value | Meaning |
|---|---|---|
| `__reactContainer$` on any node | **0** | No root was attached/committed |
| `__reactFiber$` / `__reactProps$` | **4**, on `<html>` and `<body>` only | Hydration began at the document root and walked to `<body>` — then stopped |
| Suspense / Comment markers (`<!--$-->`, `$RC`, `$RS`) | **0** | Not even fallback boundaries committed |
| Task queue `pending` | **0** | Not starvation — the queue settled |
| Errors / throws | **0** React errors | Silent stall, not a thrown/digested error |

So hydration started, suspended at/near the root on the lazy Flight model, and **never resumed**: no
client tree, no fallbacks, no error. The four fibers never became a committed root.

### Q3 — real error messages: **there are no errors to decode**
The bundle wires `window.onerror → console.error`; the probe also wrapped `console.error/warn/info`.
Captured output contained **only two app-level API permission errors** (`cookie:companyContext`,
`data:inventory`, both HTTP 400) — **zero** `Minified React error #N`, zero hydration-mismatch warnings.
React is not throwing; it is silently awaiting. A dev build would therefore not surface a hidden error;
it would at most confirm the suspended-render warning. (See *Dev-build consideration* below.)

### Second-site corroboration (www.aleris.se)

Re-ran the identical probe against a second, independent Next.js App-Router RSC site to check that the
collapse is a class property of RSC, not something site-specific. Same mechanism — even starker:

| Signal | norengros.no | aleris.se |
|---|---|---|
| Client refs resolved | 61/61, 0 threw | **25/25, 0 threw** (18 are real component fns) |
| Modules register / execute / throw | 988 / 897 / 0 | 591 / 515 / 0 |
| `__reactContainer$` committed | 0 | 0 |
| React fibers attached | 4 (`<html>`,`<body>`) | **0** — didn't attach at all |
| Task queue at finalize | idle (`pending=0`) | idle (`pending=0`) |
| React errors | 0 | 0 |
| Collapsed body | 115 `<script>` + 52 `<link>` | 78 `<script>` + 18 `<link>` |

aleris.se is the **stronger** case for the routing recommendation: its SSR shell contains **0 anchors**
(the root element is `$L17`, a client component; 123 `$L` lazy markers), so every link lives behind
client render. The JS backend yields nothing here (0 anchors with or without streams), so aleris.se
**must** use the Playwright/Puppeteer backend — the Phase A guard has no SSR links to fall back on.

---

## Why this is past the boundary (bounded vs unbounded)

The bounded shims are all present and correct: WHATWG Streams (`stream/`), `fetch`/XHR, the DOM surface,
`MessageChannel` (functional — `postMessage` enqueues through the task queue, so React’s scheduler
callback path is pumped). Modules load; client refs resolve; chunks ensure.

The break is **inside `react-server-dom-webpack`**: its client-reference lazy thunks and Flight-model
deserialization suspend on an internal Promise that depends on the runtime’s own chunk-loading signal
handshake — not merely on the module being in webpack’s cache. Driving that gate to completion means
faithfully reproducing the RSC runtime’s async coordination between React Flight and the bundler. That is
runtime-reimplementation territory, not API-shimming. It is the same class of work as a real browser JS
engine driving React’s concurrent hydration across macrotask boundaries.

**It is also not a drain-depth or scheduler-pump problem:** the queue is idle (`pending = 0`) at finalize
— more pump turns would not change the outcome, because the Promise React is awaiting is never enqueued
into our task queue at all.

## Why full render would not help crawling even if reached

The client components depend on **authenticated runtime data**. Observed runtime fetches
(`/api/wholesaler/list`, `/api/price`, `/api/inventory`, …) returned **403/400 (“User does not have
access”)**. A crawler cannot authenticate to these. So a *faithful* client render would reproduce the
client tree **against empty/forbidden data** — skeletons or nothing — which is **worse** than the
server-rendered HTML that already contains the target links (the 47 anchors live in the SSR shell). The
server already rendered the links with data; React’s client render would discard them.

This is the independent clincher: full client render is both **out of bounded-shim reach** and **of no
crawl benefit** on this class of site.

---

## Recommendation

1. **Keep Phase A as the shipped behaviour.** `EnableStreams` + the `__crawlerCaptureBaseline` /
   `__crawlerGuardRegression` guard already guarantee RSC sites are never worse than their server shell
   (norengros: 47 anchors with streams on == streams off). No change needed.
2. **Do not pursue Phase C** (the hydration/client-render fix). Past the boundary; no crawl payoff.
3. **Routing:** for an RSC site whose SSR shell genuinely lacks links the crawler needs, use the
   Playwright/Puppeteer backends — consistent with the existing scope boundary
   (`src/SimpleCrawler.Js.Dom/CLAUDE.md`).
4. **Default backend stays `HtmlAgilityPack`.** For RSC sites where the bundle aborts before mutating
   anything link-relevant, static extraction already yields the full link set byte-for-byte.

---

## Dev-build consideration (Q3 fidelity)

The spike instrumented the **production** bundle directly (chunk-registration Proxy, `__webpack_require__`
exfiltration, client-ref resolution test, React fiber/container scan, console capture) rather than
substituting a React development build. Rationale:

- The failure mode is **silent suspension, not a thrown error** — so the dev build’s primary value
  (un-digested error messages) has nothing to decode. The instrumentation above is *higher*-signal for
  the actual question (“is it throwing, suspending, or mismatching?”).
- Overriding prod React inside a Next production bundle is fragile (different module graph; risks
  conflating failures), and a local **dev** Next app uses dev React whose hydration/suspense behaviour
  differs from production, so it would not faithfully reproduce the prod collapse.

If a future investigation wants belt-and-suspenders, the cleanest faithful setup is a local
**production** Next build (`next build && next start`) with a single client + server component, rendered
through the crawler — not an in-flight prod-React override.

---

## Reproducibility

The instrumentation lives in `.spike/spike.js` (untracked, local). It is loaded by a small env-gated seam in
`JsRenderer.RunAsync` (read `JSRENDER_SPIKE_FILE`; if set and the file exists, `engine.Execute` its
contents after the DOM/streams/fetch preludes, before bundle execution). The seam was reverted from
`master` after the spike (NO-GO → no permanent product change); to re-run, re-apply those ~5 lines, then:

```sh
JSRENDER_SPIKE_FILE="D:/Projects/CSharp/SimpleCrawler/.spike/spike.js" \
  dotnet run --project tests/SimpleCrawler.ProfileRunner -c Release -- \
  rendersize v8 react --url https://www.norengros.no/ --fetch --streams
```

Read the `[SPIKE] PRE-GUARD …` line and the dumped `rendersize-live-v8-streamsTrue.html`. With the probe
active the baseline-restore is intentionally skipped, so the dump shows the **collapsed** (post-React)
tree; without the probe, the guard restores the SSR shell (47 anchors).
