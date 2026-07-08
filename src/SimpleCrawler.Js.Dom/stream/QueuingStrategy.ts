export class CountQueuingStrategy {
    highWaterMark: number;

    constructor(init?: any) {
        this.highWaterMark = init && typeof init.highWaterMark === "number" ? init.highWaterMark : 1;
    }

    size(): number {
        return 1;
    }
}

export class ByteLengthQueuingStrategy {
    highWaterMark: number;

    constructor(init?: any) {
        this.highWaterMark = init && typeof init.highWaterMark === "number" ? init.highWaterMark : 1;
    }

    size(chunk?: any): number {
        return chunk && typeof chunk.byteLength === "number" ? chunk.byteLength : 0;
    }
}
