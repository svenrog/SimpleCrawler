import { installDOM } from "./browser/globals";
import { installConsole } from "./console/api";
import { installCrawlerApi } from "./crawler/api";

installDOM(globalThis as any);
installConsole(globalThis as any);
installCrawlerApi(globalThis as any);