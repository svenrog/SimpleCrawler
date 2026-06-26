export default function mountMatchMedia(): boolean {
    if (typeof window.matchMedia !== 'function') return false;
    return typeof window.matchMedia('(min-width: 0px)').matches === 'boolean';
}
