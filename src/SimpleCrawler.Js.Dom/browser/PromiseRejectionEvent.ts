import { Event } from "./Event";

// core-js decides the native Promise needs replacing when it looks like a browser but window.PromiseRejectionEvent
// is not a callable global; it then installs its own Promise, from which a bundle that tree-shook the (natively
// present) es.promise.finally/allSettled/withResolvers add-ons is missing those methods, so `p.finally(...)`
// throws and the page's hydration dies. Providing this keeps core-js on its native-Promise path.
export class PromiseRejectionEvent extends Event {
    readonly promise: any;
    readonly reason: any;

    constructor(type: string, init?: any) {
        super(type, init);
        this.promise = init ? init.promise : undefined;
        this.reason = init ? init.reason : undefined;
    }
}
