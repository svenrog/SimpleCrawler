import { doc } from "../browser/globals";
import { parseHTML } from "../html/parser";
import { serializeNode } from "../html/serializer";
import { applyUrl } from "../url/resolve";
import { pumpTasks, pendingCount } from "../scheduler/taskQueue";
import type { ScriptDescriptor } from "../types/internal";
import { NodeType } from "../types/NodeType";

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

export function installCrawlerApi(global: any): void {
    global.__crawlerSetLocation = (url: string) => { applyUrl(url); };
    global.__crawlerLoadHtml = (html: unknown) => { parseHTML(doc, html); };
    global.__crawlerCollectScripts = () => JSON.stringify(collectScripts());
    global.__crawlerPending = () => pendingCount();
    global.__crawlerPump = () => pumpTasks();
    global.__crawlerSerialize = () => doc.documentElement ? serializeNode(doc.documentElement) : "";
}
