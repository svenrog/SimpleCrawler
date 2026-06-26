export default function mountLocalStorage(): boolean {
    localStorage.setItem('crawler-probe', 'ok');
    const ok = localStorage.getItem('crawler-probe') === 'ok';
    localStorage.removeItem('crawler-probe');
    return ok;
}