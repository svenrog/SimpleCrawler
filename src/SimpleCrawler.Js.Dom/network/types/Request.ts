import { Headers } from "./Headers";

export class Request {
    url: string;
    method: string;
    headers: Headers;
    body: any;
    constructor(input: any, init?: any) {
        init = init || {};
        if (input && typeof input === "object" && "url" in input) {
            this.url = input.url; this.method = init.method || input.method || "GET";
            this.headers = new Headers(init.headers || input.headers);
            this.body = init.body !== undefined ? init.body : input.body;
        } else {
            this.url = String(input); this.method = init.method || "GET";
            this.headers = new Headers(init.headers); this.body = init.body;
        }
    }
}