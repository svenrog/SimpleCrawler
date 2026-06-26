export default function mountCryptoRandomUuid(): boolean {
    if (typeof crypto?.randomUUID !== 'function') return false;
    return typeof crypto.randomUUID() === 'string';
}
