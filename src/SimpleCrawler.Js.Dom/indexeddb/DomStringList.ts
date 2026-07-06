export class DomStringList extends Array<string> {
    contains(name: string): boolean {
        return this.indexOf(name) !== -1;
    }
    item(index: number): string | null {
        return index >= 0 && index < this.length ? this[index] : null;
    }
}