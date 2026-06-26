export default function mountResizeObserver(): boolean {
    const observer = new ResizeObserver(() => {});
    observer.observe(document.documentElement);
    observer.disconnect();
    return true;
}
