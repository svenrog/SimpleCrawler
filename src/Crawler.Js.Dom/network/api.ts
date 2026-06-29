import { fetch } from "./fetch";
import { Headers } from "./Headers";
import { Request } from "./Request";
import { Response } from "./Response";
import { XMLHttpRequest } from "./XMLHttpRequest";

export function installNetwork(global: any): void {
    global.Headers = global.Headers || Headers;
    global.Response = global.Response || Response;
    global.Request = global.Request || Request;
    global.fetch = global.fetch || fetch;
    global.XMLHttpRequest = global.XMLHttpRequest || XMLHttpRequest;
}