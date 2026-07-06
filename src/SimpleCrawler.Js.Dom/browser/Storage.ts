export class Storage {
    private readonly store = new Map<string, string>();

    get length(): number {
        return this.store.size;
    }

    getItem(key: string): string | null {
        return this.store.has(key) ? this.store.get(key)! : null;
    }

    setItem(key: string, value: any): void {
        this.store.set(String(key), value == null ? "" : String(value));
    }

    removeItem(key: string): void {
        this.store.delete(String(key));
    }

    clear(): void {
        this.store.clear();
    }

    key(index: number): string | null {
        if (index < 0 || index >= this.store.size) return null;
        let i = 0;
        for (const k of this.store.keys()) {
            if (i++ === index) return k;
        }
        return null;
    }
}

export function createStorage(): Storage {
    return new Storage();
}
