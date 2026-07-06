export class IdbRequest {
    result: any = undefined;
    error: any = null;
    readyState = "pending";
    onsuccess: any = null;
    onerror: any = null;
    source: any = null;
    transaction: any = null;

    _succeed(value: any): void {
        this.result = value;
        this.readyState = "done";
        if (typeof this.onsuccess === "function") {
            try { this.onsuccess({ target: this, type: "success" }); } catch { /* one handler must not abort the drain */ }
        }
    }

    _fail(error: any): void {
        this.error = error;
        this.readyState = "done";
        if (typeof this.onerror === "function") {
            try { this.onerror({ target: this, type: "error" }); } catch { /* as above */ }
        }
    }
}