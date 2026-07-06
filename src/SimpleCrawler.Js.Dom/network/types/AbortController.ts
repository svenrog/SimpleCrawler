import { AbortSignal } from "./AbortSignal";

export class AbortController {
    public readonly signal = new AbortSignal();
    public abort(reason?: object) {}
}