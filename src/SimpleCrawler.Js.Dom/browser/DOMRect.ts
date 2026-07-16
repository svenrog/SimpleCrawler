// Geometry globals. Layout and animation libraries construct `new DOMRect(...)` or `DOMRectReadOnly.fromRect(...)`
// bare during init and read back the derived edges, so absence is a ReferenceError inside the mount, not a
// fallback. Layout-less here, so the values are only ever what the caller supplied.
export class DOMRectReadOnly {
    x: number;
    y: number;
    width: number;
    height: number;

    constructor(x?: number, y?: number, width?: number, height?: number) {
        this.x = +(x as any) || 0;
        this.y = +(y as any) || 0;
        this.width = +(width as any) || 0;
        this.height = +(height as any) || 0;
    }

    get top(): number { return Math.min(this.y, this.y + this.height); }
    get bottom(): number { return Math.max(this.y, this.y + this.height); }
    get left(): number { return Math.min(this.x, this.x + this.width); }
    get right(): number { return Math.max(this.x, this.x + this.width); }

    toJSON(): any {
        return {
            x: this.x, y: this.y, width: this.width, height: this.height,
            top: this.top, right: this.right, bottom: this.bottom, left: this.left,
        };
    }

    static fromRect(other?: any): DOMRectReadOnly {
        other = other || {};
        return new DOMRectReadOnly(other.x, other.y, other.width, other.height);
    }
}

export class DOMRect extends DOMRectReadOnly {
    static fromRect(other?: any): DOMRect {
        other = other || {};
        return new DOMRect(other.x, other.y, other.width, other.height);
    }
}
