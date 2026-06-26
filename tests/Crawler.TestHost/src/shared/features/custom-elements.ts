export default function mountCustomElements(): boolean {
    return typeof customElements?.define === 'function';
}
