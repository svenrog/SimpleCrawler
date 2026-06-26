export default function mountMutationObserver(): boolean {
    const observer = new MutationObserver(() => {});
    observer.observe(document.documentElement, { childList: true });
    observer.disconnect();
    return true;
}
