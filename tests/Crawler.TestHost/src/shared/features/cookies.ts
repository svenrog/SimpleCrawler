export default function mountCookies() {
    document.cookie = 'crawler-probe=ok; path=/';
    return document.cookie.includes('crawler-probe=ok');
}