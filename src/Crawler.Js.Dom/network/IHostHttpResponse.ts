// Shape returned by the host JsHttp.request POCO; the C#<->JS contract stays owned by Crawler.Js.Dom.Network.
interface IHostHttpResponse {
    ok: boolean;
    status: number;
    statusText: string;
    url: string;
    body: string;
    headersJson: string;
    error?: string;
}
