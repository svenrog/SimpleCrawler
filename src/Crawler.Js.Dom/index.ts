import { installDOM, snapshotBaseline } from "./browser/globals";
import { installConsole } from "./console/api";
import { installCrawlerApi } from "./crawler/api";

installDOM(globalThis as any);
installConsole(globalThis as any);
installCrawlerApi(globalThis as any);
// After all preludes install, record the clean global set so __crawlerReset can scrub bundle-added globals
// when the Jint pool reuses this realm for the next page.
snapshotBaseline(globalThis as any);
