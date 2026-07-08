import { doc } from "../browser/globals";
import { parseHTML } from "../html/parser";
import { serializeNode } from "../html/serializer";
import { applyUrl } from "../url/resolve";
import { pumpTasks, pendingCount } from "../scheduler/taskQueue";
import { takeResources, pendingResourceCount, fireResourceEvent } from "../dom/resourceLoader";
import { setViewport } from "../browser/viewport";
import { enableDomProfile, dumpDomProfile } from "../profiling/domProfiler";
import { HTMLScriptElement } from "../dom/HTMLScriptElement";
import type { ScriptDescriptor } from "../types/internal";
import { NodeType } from "../types/NodeType";

// The host sets document.currentScript around each classic script execution (and clears it after) so
// webpack's auto-public-path — which reads document.currentScript.src and, under Next, asserts the value is
// `instanceof HTMLScriptElement` — sees a real script element instead of undefined. A fresh JS instance is
// required: only a genuine HTMLScriptElement satisfies the instanceof check on both engines.
function setCurrentScript(src: unknown): void {
    if (src == null) {
        doc.currentScript = null;
        return;
    }
    const script = new HTMLScriptElement();
    const s = String(src);
    if (s) script.src = s;
    // A real currentScript is attached to the document, and bundles self-remove with
    // currentScript.parentNode.removeChild(currentScript). Point parentNode at a live container (never adding
    // the synthetic node to its childNodes) so that call resolves to a harmless no-op instead of throwing on null.
    script.parentNode = (doc.head || doc.body || doc.documentElement) as any;
    doc.currentScript = script as any;
}

function collectScripts(): ScriptDescriptor[] {
    const out: ScriptDescriptor[] = [];
    if (!doc.documentElement) return out;
    function walk(n: any): void {
        for (const c of n.childNodes) {
            if (c.nodeType !== NodeType.Element) continue;
            if (c.localName === "script") {
                const type = c.getAttribute("type") || "";
                if (type && type !== "text/javascript" && type !== "module" && type !== "application/javascript") {
                    walk(c);
                    continue;
                }
                out.push({
                    module: type === "module",
                    external: !!c.getAttribute("src"),
                    src: c.getAttribute("src") || "",
                    text: c.textContent,
                });
            }
            walk(c);
        }
    }
    walk(doc.documentElement);
    return out;
}

// The document base URL for resolving relative script/resource URLs: the first in-document <base href>
// (head, document order) or "" when absent. A <base href="/"> on a page served from a nested path is why
// a relative <script src="main.js"> must resolve against the base, not the page URL — otherwise the host
// fetches the SPA's catch-all HTML fallback and the engine chokes on "Unexpected token <".
function getBaseHref(): string {
    if (!doc.documentElement) return "";
    let found = "";
    function walk(n: any): boolean {
        for (const c of n.childNodes) {
            if (c.nodeType !== NodeType.Element) continue;
            if (c.localName === "base") {
                const href = c.getAttribute("href");
                if (href) { found = href; return true; }
            }
            if (walk(c)) return true;
        }
        return false;
    }
    walk(doc.documentElement);
    return found;
}

// Mirrors the AngleSharp static extractor (QuerySelectorAll("a"), link[rel=canonical], meta[name=robots])
// but reads the live DOM directly, avoiding the serialize→reparse round trip. A naive childNodes walk is
// true parity with serializeNode: the serializer also iterates childNodes, so template .content and
// shadowRoot (both off the childNodes axis) are skipped by both paths.
function collectLinks(): { anchors: (string | null)[]; canonical: string | null; robots: string | null } {
    const anchors: (string | null)[] = [];
    let canonical: string | null = null;
    let robots: string | null = null;
    if (!doc.documentElement) return { anchors, canonical, robots };
    function walk(n: any): void {
        for (const c of n.childNodes) {
            if (c.nodeType !== NodeType.Element) continue;
            const tag = c.localName;
            if (tag === "a") {
                anchors.push(c.getAttribute("href"));
            } else if (canonical == null && tag === "link") {
                const rel = (c.getAttribute("rel") || "").toLowerCase().split(/\s+/);
                if (rel.indexOf("canonical") >= 0) canonical = c.getAttribute("href");
            } else if (robots == null && tag === "meta") {
                if ((c.getAttribute("name") || "").toLowerCase() === "robots") robots = c.getAttribute("content");
            }
            walk(c);
        }
    }
    walk(doc.documentElement);
    return { anchors, canonical, robots };
}

function countAnchors(): number {
    if (!doc.documentElement) return 0;
    let count = 0;
    function walk(n: any): void {
        for (const c of n.childNodes) {
            if (c.nodeType !== NodeType.Element) continue;
            if (c.localName === "a") count++;
            walk(c);
        }
    }
    walk(doc.documentElement);
    return count;
}

// A client-side framework that streams/hydrates can tear down the server-rendered tree and, in this
// single-pass render, fail to rebuild it — leaving fewer links than the shell shipped with (seen with
// Next.js App Router RSC when EnableStreams lets its streaming path run). Snapshot the pre-script tree
// so the render can fall back to it if the bundle regresses the link count below the baseline.
let _baselineHtml: string | null = null;
let _baselineAnchors = 0;

function captureBaseline(): void {
    _baselineHtml = doc.documentElement ? serializeNode(doc.documentElement) : null;
    _baselineAnchors = countAnchors();
}

// Returns the restored anchor count if the live tree regressed below the baseline (and restores it),
// otherwise -1. The restore reparses the snapshot, which wireDocument swaps in wholesale.
function guardRegression(): number {
    if (_baselineHtml == null) return -1;
    if (countAnchors() >= _baselineAnchors) return -1;
    parseHTML(doc, _baselineHtml);
    return _baselineAnchors;
}

export function installCrawlerApi(global: any): void {
    global.__crawlerSetLocation = (url: string) => { applyUrl(url); };
    global.__crawlerSetViewport = (width: number, height: number) => { setViewport(width, height); };
    global.__crawlerSetCurrentScript = (src: unknown) => { setCurrentScript(src); };
    global.__crawlerLoadHtml = (html: unknown) => { parseHTML(doc, html); };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerGetBaseHref = () => getBaseHref();
    global.__crawlerCollectLinks = () => JSON.stringify(collectLinks());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerTakeResources = () => takeResources();
    global.__crawlerPendingResources = () => pendingResourceCount();
    global.__crawlerFireResourceEvent = (id: number, type: string) => { fireResourceEvent(id, type); };
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
    global.__crawlerCaptureBaseline = () => { captureBaseline(); };
    global.__crawlerGuardRegression = () => guardRegression();
    global.__crawlerEnableDomProfile = () => { enableDomProfile(); };
    global.__crawlerDomProfileDump = () => dumpDomProfile();
}
