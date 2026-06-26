export default function mountIntersectionObserver(): boolean {
    const observer = new IntersectionObserver(() => {});
    observer.observe(document.documentElement);
    observer.disconnect();
    return true;
}