function randomByte(): number {
    return Math.floor(Math.random() * 256);
}

function hex(n: number): string {
    return n < 16 ? "0" + n.toString(16) : n.toString(16);
}

export const crypto = {
    getRandomValues(arr: any): any {
        if (arr) for (let i = 0; i < arr.length; i++) arr[i] = randomByte();
        return arr;
    },
    randomUUID(): string {
        const b = new Uint8Array(16);
        for (let i = 0; i < 16; i++) b[i] = randomByte();
        b[6] = (b[6] & 0x0f) | 0x40;
        b[8] = (b[8] & 0x3f) | 0x80;
        const h: string[] = [];
        for (let i = 0; i < 16; i++) h.push(hex(b[i]));
        return h[0] + h[1] + h[2] + h[3] + "-" + h[4] + h[5] + "-" + h[6] + h[7] + "-" + h[8] + h[9] + "-" + h[10] + h[11] + h[12] + h[13] + h[14] + h[15];
    },
};
