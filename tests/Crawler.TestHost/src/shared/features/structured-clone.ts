export default function mountStructuredClone(): boolean {
    const clone = structuredClone({ ok: true });
    return clone.ok === true;
}
