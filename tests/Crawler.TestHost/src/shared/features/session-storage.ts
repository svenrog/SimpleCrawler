export default function mountSessionStorage(): boolean {
    sessionStorage.setItem('crawler-probe', 'ok');
    const ok = sessionStorage.getItem('crawler-probe') === 'ok';
    sessionStorage.removeItem('crawler-probe');
    return ok;
}
