// Shape returned by the host JsHttp.request POCO; the C#<->JS contract stays owned by SimpleCrawler.Js.Dom.Network.
interface IHostHttpResponse {
    ok: boolean;
    status: number;
    statusText: string;
    url: string;
    body: string;
    headersJson: string;
    error?: string;
}

// The host embeds a single variadic function that returns the response as a JSON string; installNetwork wraps
// it in the __http object the fetch/XHR shims call. It is a plain function (not a host object method) because
// ClearScript's V8 backend cannot reflectively invoke a host object's instance method under NativeAOT.
declare const __httpRequest: (url: string, method: string, headersJson: string, body: string | null) => string;

declare const __http: {
    request(url: string, method: string, headersJson: string, body: string | null): IHostHttpResponse
};
