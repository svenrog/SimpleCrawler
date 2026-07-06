type Listener = (path: string) => void;

const listeners = new Set<Listener>();
let started = false;

export function currentPath(): string {
    return window.location.pathname;
}

function emit(): void {
    const path = currentPath();
    for (const listener of listeners) listener(path);
}

export function navigate(href: string): void {
    if (href === currentPath()) return;
    window.history.pushState({}, '', href);
    emit();
}

export function subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
}

export function start(): void {
    if (started) return;
    started = true;

    window.addEventListener('popstate', emit);
    document.addEventListener('click', (event) => {
        if (event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

        const anchor = (event.target as Element | null)?.closest?.('a');
        const href = anchor?.getAttribute('href');
        if (!href || !href.startsWith('/')) return;

        event.preventDefault();
        navigate(href);
    });
}
