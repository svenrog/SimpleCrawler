export class AbortSignal {
    public readonly aborted: boolean = false;
    public throwIfAborted() {}
    public addEventListener(type: object | null = null, listener: object | null = null, options: object | null = null) { }
    public removeEventListener(type: object | null = null, listener: object | null = null, options: object | null = null) { }
    public dispatchEvent(evt: object | null = null) {
        return true;
    }
}