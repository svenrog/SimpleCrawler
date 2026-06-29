import { installDOM } from "./browser/globals";
import { installCrawlerApi } from "./crawler/api";

installDOM(globalThis as any);
installCrawlerApi(globalThis as any);
