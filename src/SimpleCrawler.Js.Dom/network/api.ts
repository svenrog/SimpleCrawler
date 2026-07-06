import { fetch } from "./fetch";
import { AbortController } from "./types/AbortController";
import { AbortSignal } from "./types/AbortSignal";
import { FormData } from "./types/FormData";
import { Headers } from "./types/Headers";
import { Request } from "./types/Request";
import { Response } from "./types/Response";
import { XMLHttpRequest } from "./XMLHttpRequest";
import { XMLHttpRequestEventTarget } from "./XMLHttpRequestEventTarget";

export function installNetwork(global: any): void {
    global.Headers = global.Headers || Headers;
    global.Response = global.Response || Response;
    global.Request = global.Request || Request;
    global.FormData = global.FormData || FormData;
    global.fetch = global.fetch || fetch;
    global.XMLHttpRequest = global.XMLHttpRequest || XMLHttpRequest;
    global.XMLHttpRequestEventTarget = global.XMLHttpRequestEventTarget || XMLHttpRequestEventTarget;
    global.AbortController = global.AbortController || AbortController;
    global.AbortSignal = global.AbortSignal || AbortSignal;
}