import { doc } from "../browser/globals";
import { parseHTML } from "../html/parser";
import { buildDocumentFromTree } from "../html/treeBuilder";
import { serializeNode } from "../html/serializer";
import { applyUrl } from "../url/resolve";
import { pumpTasks, pendingCount } from "../scheduler/taskQueue";
import { takeResources, pendingResourceCount, fireResourceEvent } from "../dom/resourceLoader";
import { setViewport } from "../browser/viewport";
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

export function installCrawlerApi(global: any): void {
    global.__crawlerSetLocation = (url: string) => { applyUrl(url); };
    global.__crawlerSetViewport = (width: number, height: number) => { setViewport(width, height); };
    global.__crawlerSetCurrentScript = (src: unknown) => { setCurrentScript(src); };
    global.__crawlerLoadHtml = (html: unknown) => { parseHTML(doc, html); };
    global.__crawlerLoadTree = (json: unknown) => { buildDocumentFromTree(doc, String(json)); };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerCollectLinks = () => JSON.stringify(collectLinks());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerTakeResources = () => takeResources();
    global.__crawlerPendingResources = () => pendingResourceCount();
    global.__crawlerFireResourceEvent = (id: number, type: string) => { fireResourceEvent(id, type); };
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
}
